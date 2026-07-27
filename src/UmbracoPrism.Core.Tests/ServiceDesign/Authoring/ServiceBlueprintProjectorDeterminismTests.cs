using System.Text.Json;
using FluentAssertions;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using UmbracoPrism.ServiceBlueprintEditor.Authoring;

namespace UmbracoPrism.Core.Tests.ServiceDesign.Authoring;

/// <summary>
/// Locks the determinism guarantee: <see cref="ServiceBlueprintProjector.Project"/> must produce
/// byte-identical output and an identical SHA-256 checksum on every invocation for the same input.
/// </summary>
public class ServiceBlueprintProjectorDeterminismTests
{
    private readonly ServiceBlueprintProjector _projector = new();

    [Fact]
    public void Project_SameInput_ProducesByteIdenticalOutput()
    {
        var authored = BuildDeterministicWorkflow();

        var result1 = _projector.Project(authored);
        var result2 = _projector.Project(authored);

        result1.Checksum.Should().Be(result2.Checksum,
            "identical inputs must produce identical SHA-256 checksums");

        var bytes1 = JsonSerializer.SerializeToUtf8Bytes(result1.File, ServiceBlueprintProjector.CanonicalOptions);
        var bytes2 = JsonSerializer.SerializeToUtf8Bytes(result2.File, ServiceBlueprintProjector.CanonicalOptions);

        bytes1.Should().Equal(bytes2,
            "identical inputs must produce byte-identical projected JSON");
    }

    [Fact]
    public void Project_ChecksumIsHexEncodedSha256()
    {
        var result = _projector.Project(BuildDeterministicWorkflow());

        result.Checksum.Should().MatchRegex("^[0-9a-f]{64}$",
            "checksum must be a lowercase hex-encoded SHA-256 digest");
    }

    [Fact]
    public async Task Project_PlanningFixture_IsDeterministic()
    {
        var fixturesPath = GetFixturesPath();
        var authored = await AuthoredServiceBlueprintFixtureLoader.LoadAsync(fixturesPath, "planning");
        authored.Should().NotBeNull("planning fixture must exist");

        var result1 = _projector.Project(authored!);
        var result2 = _projector.Project(authored!);

        result1.Checksum.Should().Be(result2.Checksum);
    }

    [Fact]
    public void Project_NormalisesStageOrder_BeforeEmitting()
    {
        var authored = new AuthoredServiceBlueprint
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000001"),
            DefinitionKey = "order-test",
            DisplayName = "Order Test",
            Version = 1,
            InitialTouchpointKey = "alpha",
            Touchpoints =
            [
                new AuthoredTouchpoint { TouchpointKey = "zeta", DisplayName = "Zeta", Kind = TouchpointKind.Confirmation },
                new AuthoredTouchpoint { TouchpointKey = "alpha", DisplayName = "Alpha", Kind = TouchpointKind.Question }
            ]
        };

        var result = _projector.Project(authored);

        result.File.Touchpoints.Select(s => s.TouchpointKey)
            .Should().ContainInOrder(new[] { "alpha", "zeta" },
                because: "stages must be emitted in ordinal StageKey order regardless of authored order");
    }

    [Fact]
    public void Project_NormalisesTransitionOrder_BeforeEmitting()
    {
        // Two gateways with different source stages each emit one route; verify that the
        // projected runtime transitions are sorted by (source, target, trigger).
        var authored = new AuthoredServiceBlueprint
        {
            Id = new Guid("aaaabbbb-0000-0000-0000-000000000002"),
            DefinitionKey = "transition-order-test",
            DisplayName = "Transition Order Test",
            Version = 1,
            InitialTouchpointKey = "a",
            Queues = [new AuthoredQueue { Key = "applicant", DisplayName = "Applicant" }],
            Touchpoints =
            [
                new AuthoredTouchpoint
                {
                    TouchpointKey = "a",
                    DisplayName = "A",
                    Kind = TouchpointKind.Question,
                    QueueKey = "applicant",
                    Routes =
                    [
                        new AuthoredRoute { Id = "route-a", Target = "out-of-a", Trigger = "continue" }
                    ]
                },
                new AuthoredTouchpoint { TouchpointKey = "b", DisplayName = "B", Kind = TouchpointKind.Confirmation, QueueKey = "applicant" },
                new AuthoredTouchpoint { TouchpointKey = "c", DisplayName = "C", Kind = TouchpointKind.Confirmation, QueueKey = "applicant" }
            ],
            Gateways =
            [
                new AuthoredGateway
                {
                    GatewayKey = "out-of-a",
                    DisplayName = "Out of A",
                    Kind = GatewayKind.Split,
                    QueueKey = "applicant",
                    Routes =
                    [
                        new AuthoredRoute { Id = "to-c", Target = "c", Trigger = "continue" },
                        new AuthoredRoute { Id = "to-b", Target = "b", Trigger = "continue" }
                    ]
                }
            ]
        };

        var result = _projector.Project(authored);

        result.File.Transitions!.Select(t => t.ToState)
            .Should().ContainInOrder(new[] { "out-of-a", "b", "c" },
                because: "transitions must be emitted sorted by (source, target, trigger)");
    }

    private static AuthoredServiceBlueprint BuildDeterministicWorkflow() => new()
    {
        Id = new Guid("12345678-1234-1234-1234-123456789012"),
        DefinitionKey = "determinism-test",
        DisplayName = "Determinism Test",
        Version = 1,
        Description = "Stable workflow used by determinism tests.",
        SchemaVersion = "1.0",
        InitialTouchpointKey = "collect",
        RequestPolicy = "single",
        Queues = [new AuthoredQueue { Key = "applicant", DisplayName = "Applicant" }],
        Touchpoints =
        [
            new AuthoredTouchpoint
            {
                TouchpointKey = "collect",
                DisplayName = "Collect details",
                Kind = TouchpointKind.Question,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "collect-continue", Target = "after-collect", Trigger = "continue" }],
                Components =
                [
                    new FieldsetComponent
                    {
                        Children =
                        [
                            new EmailComponent { FieldKey = "email", Label = "Email address", Required = true },
                            new TextInputComponent { FieldKey = "name", Label = "Full name", Required = true }
                        ]
                    }
                ]
            },
            new AuthoredTouchpoint
            {
                TouchpointKey = "review",
                DisplayName = "Check your answers",
                Kind = TouchpointKind.CheckAnswers,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "review-submit", Target = "after-review", Trigger = "submit" }]
            },
            new AuthoredTouchpoint
            {
                TouchpointKey = "done",
                DisplayName = "Application submitted",
                Kind = TouchpointKind.Confirmation,
                QueueKey = "applicant"
            }
        ],
        Gateways =
        [
            new AuthoredGateway
            {
                GatewayKey = "after-collect",
                DisplayName = "After collect",
                Kind = GatewayKind.Split,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "to-review", Target = "review", Trigger = "continue" }]
            },
            new AuthoredGateway
            {
                GatewayKey = "after-review",
                DisplayName = "After review",
                Kind = GatewayKind.Split,
                QueueKey = "applicant",
                Routes = [new AuthoredRoute { Id = "to-done", Target = "done", Trigger = "submit" }]
            }
        ],
        Handoffs = [],
        Metadata = new Dictionary<string, string> { ["env"] = "test" }
    };

    private static string GetFixturesPath() =>
        Path.Combine(AppContext.BaseDirectory, "ServiceDesign", "Authoring", "Fixtures");
}
