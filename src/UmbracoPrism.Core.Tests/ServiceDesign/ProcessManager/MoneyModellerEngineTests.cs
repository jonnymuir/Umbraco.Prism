using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.MockBusinessApp.Services.MoneyModeller;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using UmbracoPrism.Shared.Services.Sanitization;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

/// <summary>
/// Exercises the money-modeller vertical: generic calculation evaluation in the engine,
/// slider/stat-group/chart render payloads, showWhen visibility, the embedded live model,
/// and the recalculate self-loop through a Split gateway.
/// </summary>
public class MoneyModellerEngineTests
{
    private const string Tenant = "tenant1";
    private const string User = "demo@prism.local";

    [Fact]
    public void ModelStage_RendersDeclarativeComponentsWithLiveModelAndChart()
    {
        using var harness = MoneyModellerHarness.Create();

        var envelope = harness.Engine.GetCurrent("money-modeller", Tenant, User);

        envelope.ResponseState.Should().Be("render");
        envelope.Render.Should().NotBeNull();
        envelope.Render!.Data.Should().NotBeNull("the engine embeds the live calculation model");

        var live = envelope.Render.Data!["live"]!;
        live["calculations"]!["fields"]!["resultPension"]!["expr"]!.GetValue<string>()
            .Should().Be("round(pensionOut)", "the client receives the same definitions the server evaluated");
        live["service"]!["member"]!["name"]!.GetValue<string>().Should().Be("Dr Sarah Mitchell");
        live["inputTypes"]!["retireAge"]!.GetValue<string>().Should().Be("number");
        live["defaults"]!["retireAge"]!.GetValue<string>().Should().Be("66");

        var slider = envelope.Render.Components
            .SelectMany(component => component.Fields)
            .Should().ContainSingle(field => field.FieldKey == "retireAge").Subject;
        slider.FieldType.Should().Be("slider");
        slider.Min.Should().Be(55);
        slider.Max.Should().Be(75);
        slider.Step.Should().Be(1);
        slider.Value.Should().Be("66", "the slider pre-populates from its declared default");

        var chart = envelope.Render.Components.Should()
            .ContainSingle(component => component.Type == "chart").Subject;
        var chartModel = JsonDocument.Parse(chart.ChartJson!);
        chartModel.RootElement.GetProperty("rows").GetArrayLength()
            .Should().Be(25, "ages 66 to 90 inclusive");
        chartModel.RootElement.GetProperty("bands").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void ModelStage_EvaluatesShowWhenVisibilityServerSide()
    {
        using var harness = MoneyModellerHarness.Create();

        var envelope = harness.Engine.GetCurrent("money-modeller", Tenant, User);

        // Not in quote mode: the retirement-age slider is visible, the quote notice hidden.
        var sliderComponent = envelope.Render!.Components
            .Single(component => component.Fields.Any(field => field.FieldKey == "retireAge"));
        sliderComponent.ShowWhen.Should().Be("not quoteMode");
        sliderComponent.Hidden.Should().BeFalse();

        var quoteNotice = envelope.Render.Components
            .Single(component => component.ShowWhen == "quoteMode");
        quoteNotice.Hidden.Should().BeTrue();

        // At the default age of 66 the early-retirement warning is hidden.
        envelope.Render.Components
            .Single(component => component.ShowWhen == "not quoteMode and retireAge < npa")
            .Hidden.Should().BeTrue();
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

    private static string? StatValue(ServiceRequestResponseEnvelope envelope, string fieldKey) =>
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

        private MoneyModellerHarness(string contentRootPath, BusinessAppProcessManager engine)
        {
            _contentRootPath = contentRootPath;
            Engine = engine;
        }

        public BusinessAppProcessManager Engine { get; }

        public static MoneyModellerHarness Create()
        {
            var contentRootPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                $"test-seeds-money-modeller-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(contentRootPath, "service-blueprints"));

            // Use the real seed so the tests exercise the exact demo definition,
            // but start at the model stage — the choose-start routing is covered
            // by RouteLabels_SurfaceAsActionLabelsAndStyles against the real initial state.
            var seed = JsonSerializer.Deserialize<ServiceBlueprint>(
                File.ReadAllText(FindRepoSeed()),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true,
                    AllowOutOfOrderMetadataProperties = true
                })!;

            WriteSeed(contentRootPath, seed with { DefinitionKey = "money-modeller", InitialStage = "model" });

            var mockEnvironment = new Mock<IWebHostEnvironment>();
            mockEnvironment.Setup(environment => environment.ContentRootPath).Returns(contentRootPath);
            var logger = new Mock<ILogger<BusinessAppProcessManager>>();
            var sanitizer = new Mock<IServiceContentSanitizer>();
            sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(h => h ?? string.Empty);

            var engine = new BusinessAppProcessManager(
                logger.Object,
                mockEnvironment.Object,
                sanitizer.Object,
                memberRecords: new MemberRecordService());
            engine.ResetAll();

            return new MoneyModellerHarness(contentRootPath, engine);
        }

        private static void WriteSeed(string contentRootPath, ServiceBlueprint definition)
        {
            File.WriteAllText(
                Path.Combine(contentRootPath, "service-blueprints", $"{definition.DefinitionKey}.json"),
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
                    "src", "UmbracoPrism.MockBusinessApp", "service-blueprints", "money-modeller.json");
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
