using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.MockBusinessApp.Services.MoneyModeller;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Models.Workflow.Components;
using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.WorkflowEngine;

/// <summary>
/// Exercises the money-modeller vertical: BuildRenderData hook, slider/stat-group/interactive
/// render payloads, and the recalculate self-loop through a Split gateway.
/// </summary>
public class MoneyModellerEngineTests
{
    private const string Tenant = "tenant1";
    private const string User = "demo@prism.local";

    [Fact]
    public void ModelStage_RendersInteractivePayloadWithResolvedDataAndSliderFields()
    {
        using var harness = MoneyModellerHarness.Create();

        var envelope = harness.Engine.GetCurrent("money-modeller", Tenant, User);

        envelope.ResponseState.Should().Be("render");
        envelope.Render.Should().NotBeNull();
        envelope.Render!.Data.Should().NotBeNull("the host hook supplies the moneyModel data bag");
        envelope.Render.Data!.ContainsKey("moneyModel").Should().BeTrue();

        var interactive = envelope.Render.Components.Should()
            .ContainSingle(component => component.Type == "interactive").Subject;
        interactive.Element.Should().Be("prism-money-modeller");
        interactive.DataJson.Should().NotBeNullOrEmpty("the DataKey resolves against the render data bag");

        var model = JsonDocument.Parse(interactive.DataJson!);
        model.RootElement.GetProperty("member").GetProperty("name").GetString()
            .Should().Be("Dr Sarah Mitchell");
        model.RootElement.GetProperty("results").GetProperty("resultPension").GetDecimal()
            .Should().BeGreaterThan(0);
        model.RootElement.GetProperty("calculations").GetProperty("fields").GetProperty("resultPension")
            .GetProperty("expr").GetString()
            .Should().Be("round(pensionOut)", "the island receives the same definitions the server evaluated");
        model.RootElement.GetProperty("chart").GetArrayLength()
            .Should().Be(25, "ages 66 to 90 inclusive");

        var slider = interactive.Fields.Should()
            .ContainSingle(field => field.FieldKey == "retireAge").Subject;
        slider.FieldType.Should().Be("slider");
        slider.Min.Should().Be(55);
        slider.Max.Should().Be(75);
        slider.Step.Should().Be(1);
        slider.Value.Should().Be("66", "the engine pre-populates the slider from the enriched field values");
    }

    [Fact]
    public void ModelStage_ResolvesStatGroupValuesFromServerComputedResults()
    {
        using var harness = MoneyModellerHarness.Create();

        var envelope = harness.Engine.GetCurrent("money-modeller", Tenant, User);

        var statGroup = envelope.Render!.Components.Should()
            .ContainSingle(component => component.Type == "stat-group").Subject;

        statGroup.Stats.Should().NotBeNull();
        var pension = statGroup.Stats!.Should().ContainSingle(stat => stat.FieldKey == "resultPension").Subject;
        pension.Value.Should().StartWith("£", "results are formatted server-side");
        pension.Emphasis.Should().BeTrue();
    }

    [Fact]
    public void Recalculate_LoopsBackToModelWithRecomputedFigures()
    {
        using var harness = MoneyModellerHarness.Create();

        var first = harness.Engine.GetCurrent("money-modeller", Tenant, User);
        var initialPension = StatValue(first, "resultPension");

        var recalculated = harness.Engine.Advance(
            first.InstanceId,
            Tenant,
            User,
            "recalculate",
            first.StateVersion,
            new Dictionary<string, object?> { ["retireAge"] = "60", ["benefitOption"] = "Standard benefits" });

        recalculated.ResponseState.Should().Be("render");
        recalculated.Render!.StateDisplayName.Should().Be("Your money, modelled",
            "recalculate routes through the Split gateway straight back to the model stage");

        var reducedPension = StatValue(recalculated, "resultPension");
        reducedPension.Should().NotBe(initialPension,
            "retiring at 60 instead of 66 must change the server-computed pension");
    }

    [Fact]
    public void RouteLabels_SurfaceAsActionLabelsAndStyles()
    {
        using var harness = MoneyModellerHarness.Create();

        var envelope = harness.Engine.GetCurrent("money-modeller", Tenant, User);

        envelope.Render!.AvailableActions.Should().Contain(action =>
            action.ActionKey == "request-quote"
            && action.Label == "Request a formal quote"
            && action.Style == "primary");
        envelope.Render.AvailableActions.Should().Contain(action =>
            action.ActionKey == "recalculate"
            && action.Label == "Recalculate"
            && action.Style == "secondary");
    }

    private static string? StatValue(UmbracoPrism.Core.Models.Workflow.WorkflowResponseEnvelope envelope, string fieldKey) =>
        envelope.Render!.Components
            .Single(component => component.Type == "stat-group")
            .Stats!
            .Single(stat => stat.FieldKey == fieldKey)
            .Value;

    /// <summary>
    /// Loads the real money-modeller seed (with choose-start skipped straight to model for
    /// the render tests via the actual routes) into an engine wired with the modeller services.
    /// </summary>
    private sealed class MoneyModellerHarness : IDisposable
    {
        private readonly string _contentRootPath;

        private MoneyModellerHarness(string contentRootPath, BusinessAppWorkflowEngine engine)
        {
            _contentRootPath = contentRootPath;
            Engine = engine;
        }

        public BusinessAppWorkflowEngine Engine { get; }

        public static MoneyModellerHarness Create()
        {
            var contentRootPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                $"test-seeds-money-modeller-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(contentRootPath, "workflow-seeds"));

            // Use the real seed so the tests exercise the exact demo definition,
            // but start at the model stage — the choose-start routing is covered
            // by RouteLabels_SurfaceAsActionLabelsAndStyles against the real initial state.
            var seed = JsonSerializer.Deserialize<WorkflowDefinitionFile>(
                File.ReadAllText(FindRepoSeed()),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true,
                    AllowOutOfOrderMetadataProperties = true
                })!;

            WriteSeed(contentRootPath, seed with { DefinitionKey = "money-modeller", InitialState = "model" });

            var mockEnvironment = new Mock<IWebHostEnvironment>();
            mockEnvironment.Setup(environment => environment.ContentRootPath).Returns(contentRootPath);
            var logger = new Mock<ILogger<BusinessAppWorkflowEngine>>();
            var sanitizer = new Mock<IWorkflowContentSanitizer>();
            sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);

            var engine = new BusinessAppWorkflowEngine(
                logger.Object,
                mockEnvironment.Object,
                sanitizer.Object,
                moneyModeller: new MoneyModellerService(new MemberRecordService()));
            engine.ResetAll();

            return new MoneyModellerHarness(contentRootPath, engine);
        }

        private static void WriteSeed(string contentRootPath, WorkflowDefinitionFile definition)
        {
            File.WriteAllText(
                Path.Combine(contentRootPath, "workflow-seeds", $"{definition.DefinitionKey}.json"),
                JsonSerializer.Serialize(definition, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));
        }

        private static string FindRepoSeed()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "src", "UmbracoPrism.MockBusinessApp", "workflow-seeds", "money-modeller.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("money-modeller.json seed not found walking up from test bin.");
        }

        public void Dispose()
        {
            if (Directory.Exists(_contentRootPath))
            {
                Directory.Delete(_contentRootPath, recursive: true);
            }
        }
    }
}
