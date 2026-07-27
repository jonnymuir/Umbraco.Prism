using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UmbracoPrism.MockBusinessApp.Services;
using UmbracoPrism.MockBusinessApp.Services.Actions;
using UmbracoPrism.Shared.Models.ServiceDesign;
using UmbracoPrism.Shared.Models.ServiceDesign.Components;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.MockBusinessApp.Services.Actions.ActionCatalog;
using UmbracoPrism.ProcessManager.Models;

namespace UmbracoPrism.Core.Tests.ServiceDesign.ProcessManager;

public class WorkflowActionRegistryTests
{
    [Fact]
    public void Registry_ReturnsCatalogAndResolvesBuiltInHandlers()
    {
        var registry = CreateRegistry();

        registry.GetCatalog().Should().Contain(entry =>
            entry.Type == "case.assign"
            && entry.Status == ActionCatalogStatuses.Available
            && entry.RuntimeImplementation == "reference-business-app");

        registry.Resolve("forms.load").Should().BeOfType<FormsLoadWorkflowActionHandler>();
        registry.Resolve("forms.submit").Should().BeOfType<FormsSubmitWorkflowActionHandler>();
        registry.Resolve("case.assign").Should().BeOfType<CaseAssignWorkflowActionHandler>();
        registry.Resolve("notifications.send-email").Should().BeOfType<NotificationsSendEmailWorkflowActionHandler>();
        registry.Resolve("missing.action").Should().BeNull();
    }

    [Fact]
    public async Task BuiltInHandler_ExecutesWithContextAndParameters()
    {
        var registry = CreateRegistry();
        var handler = registry.Resolve("case.assign");

        var result = await handler!.ExecuteAsync(
            new ActionDefinition
            {
                Type = "case.assign",
                Timing = "OnTransition",
                Parameters =
                {
                    ["assigneeType"] = "queue",
                    ["assigneeValue"] = "planning-triage",
                    ["overwriteExisting"] = true
                }
            },
            CreateContext(),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Outputs["assigneeType"].Should().Be("queue");
        result.Outputs["assigneeValue"].Should().Be("planning-triage");
        result.Outputs["overwriteExisting"].Should().Be(true);
    }

    [Fact]
    public void ServiceRegistration_ExposesRuntimeRegistryAsCatalogSource()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IParameterWidgetMapper, DefaultParameterWidgetMapper>();
        services.AddSingleton<BuiltInActionCatalogProvider>();
        services.AddSingleton<IActionCatalogProvider>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddSingleton<IActionCatalogSource>(sp => sp.GetRequiredService<BuiltInActionCatalogProvider>());
        services.AddBusinessAppActions();

        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IWorkflowActionRegistry>();
        provider.GetRequiredService<IActionCatalogSource>().Should().BeSameAs(registry);
    }

    private static ActionRegistry CreateRegistry() =>
        new(
            new BuiltInActionCatalogProvider(new DefaultParameterWidgetMapper()),
            new IWorkflowActionHandler[]
            {
                new FormsLoadWorkflowActionHandler(),
                new FormsSaveWorkflowActionHandler(),
                new FormsSubmitWorkflowActionHandler(),
                new CaseAssignWorkflowActionHandler(),
                new CaseEnqueueWorkflowActionHandler(),
                new CaseSetStatusWorkflowActionHandler(),
                new CaseAddNoteWorkflowActionHandler(),
                new NotificationsSendEmailWorkflowActionHandler(),
                new NotificationsSendSmsWorkflowActionHandler()
            });

    private static WorkflowActionExecutionContext CreateContext() =>
        new()
        {
            Definition = new ServiceBlueprint
            {
                DefinitionKey = "planning",
                DisplayName = "Planning",
                Version = 1,
                InitialStage = "start",
                Stages = [
                    new StageDefinition { StageKey = "start", DisplayName = "Start", Components = [] },
                    new StageDefinition { StageKey = "review", DisplayName = "Review", Components = [] }
                ],
                Transitions =
                [
                    new RouteFile { FromState = "start", ToState = "review", Action = "submit" }
                ]
            },
            Instance = new ServiceRequest
            {
                InstanceId = "instance-1",
                BlueprintKey = "planning",
                TenantId = "tenant-1",
                UserId = "user-1",
                CurrentStage = "review",
                StateVersion = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            SourceState = new StageDefinition { StageKey = "start", DisplayName = "Start", Components = [] },
            TargetState = new StageDefinition { StageKey = "review", DisplayName = "Review", Components = [] },
            Transition = new RouteFile { FromState = "start", ToState = "review", Action = "submit" },
            TriggerAction = "submit",
            FieldValues = new Dictionary<string, object?>
            {
                ["reference"] = "ABC123"
            }
        };
}

public class BusinessAppWorkflowEngineActionExecutionTests : IDisposable
{
    private readonly string _testSeedDir;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<ILogger<BusinessAppProcessManager>> _logger = new();
    private readonly Mock<IServiceContentSanitizer> _sanitizer = new();

    public BusinessAppWorkflowEngineActionExecutionTests()
    {
        _testSeedDir = Path.Combine(Directory.GetCurrentDirectory(), $"workflow-action-seeds-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_testSeedDir, "service-blueprints"));
        SeedWorkflow();

        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_testSeedDir);
        _sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns<string?>(value => value ?? string.Empty);
    }

    [Fact]
    public void Advance_ExecutesStageAndTransitionActionsInOrder()
    {
        var registry = new RecordingWorkflowActionRegistry();
        var engine = new BusinessAppProcessManager(
            _logger.Object,
            _mockEnv.Object,
            _sanitizer.Object,
            actionRegistry: registry);

        var current = engine.GetCurrent("workflow-actions", "tenant-1", "user-1");
        registry.Invocations.Select(invocation => invocation.ActionType).Should().Equal("forms.load");
        registry.Invocations.Clear();

        var response = engine.Advance(
            current.InstanceId,
            "tenant-1",
            "user-1",
            "submit",
            current.StateVersion,
            new Dictionary<string, object?>
            {
                ["case-reference"] = "CASE-42"
            });

        response.ResponseState.Should().Be("complete");
        registry.Invocations.Select(invocation => invocation.ActionType).Should().Equal(
            "case.add-note",
            "forms.submit",
            "notifications.send-email");
        registry.Invocations.Should().AllSatisfy(invocation =>
        {
            invocation.TriggerAction.Should().Be("submit");
            invocation.SourceState.Should().Be("draft");
            invocation.TargetState.Should().Be("submitted");
            invocation.FieldValues.Should().ContainKey("case-reference").WhoseValue.Should().Be("CASE-42");
        });
    }

    [Fact]
    public void GetCurrent_ExecutesInitialEntryActionsOnce()
    {
        var registry = new RecordingWorkflowActionRegistry();
        var engine = new BusinessAppProcessManager(
            _logger.Object,
            _mockEnv.Object,
            _sanitizer.Object,
            actionRegistry: registry);

        var first = engine.GetCurrent("workflow-actions", "tenant-1", "user-1");
        var resumed = engine.GetCurrent("workflow-actions", "tenant-1", "user-1");

        first.InstanceId.Should().Be(resumed.InstanceId);
        registry.Invocations.Should().ContainSingle();
        registry.Invocations[0].ActionType.Should().Be("forms.load");
        registry.Invocations[0].SourceState.Should().BeNull();
        registry.Invocations[0].TargetState.Should().Be("draft");
        registry.Invocations[0].TriggerAction.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testSeedDir))
        {
            Directory.Delete(_testSeedDir, recursive: true);
        }
    }

    private void SeedWorkflow()
    {
        var workflow = new ServiceBlueprint
        {
            DefinitionKey = "workflow-actions",
            DisplayName = "Workflow Actions",
            Version = 1,
            InitialStage = "draft",
            RequestPolicy = "single",
            Stages = [
                new StageDefinition
                {
                    StageKey = "draft",
                    DisplayName = "Draft",
                    Components = [],
                    Metadata = new StageMetadata
                    {
                        Actions =
                        [
                            new ActionDefinition
                            {
                                Type = "forms.load",
                                Timing = "OnEntry",
                                Parameters =
                                {
                                    ["formDefinitionId"] = "planning-application"
                                }
                            },
                            new ActionDefinition
                            {
                                Type = "case.add-note",
                                Timing = "OnExit",
                                Parameters =
                                {
                                    ["note"] = "Leaving draft",
                                    ["visibility"] = "internal"
                                }
                            }
                        ]
                    }
                },
                new StageDefinition
                {
                    StageKey = "submitted",
                    DisplayName = "Submitted",
                    Components = [new PanelComponent { Heading = "Done" }],
                    Metadata = new StageMetadata
                    {
                        Actions =
                        [
                            new ActionDefinition
                            {
                                Type = "notifications.send-email",
                                Timing = "OnEntry",
                                Parameters =
                                {
                                    ["templateId"] = "submitted",
                                    ["recipientEmail"] = "applicant@example.com"
                                }
                            }
                        ]
                    }
                }
            ],
            Transitions =
            [
                new RouteFile
                {
                    FromState = "draft",
                    ToState = "submitted",
                    Action = "submit",
                    Metadata = new RouteMetadata
                    {
                        Actions =
                        [
                            new ActionDefinition
                            {
                                Type = "forms.submit",
                                Timing = "OnTransition",
                                Parameters =
                                {
                                    ["formDefinitionId"] = "planning-application"
                                }
                            }
                        ]
                    }
                }
            ]
        };

        File.WriteAllText(
            Path.Combine(_testSeedDir, "service-blueprints", "workflow-actions.json"),
            JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class RecordingWorkflowActionRegistry : IWorkflowActionRegistry
    {
        private readonly RecordingWorkflowActionHandler _handler = new();

        public List<RecordedInvocation> Invocations { get; } = [];

        public IReadOnlyList<ActionCatalogEntry> GetCatalog() => [];

        public IWorkflowActionHandler? Resolve(string actionType)
        {
            _handler.ActionTypeToCapture = actionType;
            _handler.Invocations = Invocations;
            return _handler;
        }
    }

    private sealed class RecordingWorkflowActionHandler : IWorkflowActionHandler
    {
        public string ActionType => ActionTypeToCapture;

        public string ActionTypeToCapture { get; set; } = string.Empty;

        public List<RecordedInvocation> Invocations { get; set; } = [];

        public Task<WorkflowActionExecutionResult> ExecuteAsync(
            ActionDefinition action,
            WorkflowActionExecutionContext context,
            CancellationToken cancellationToken)
        {
            Invocations.Add(new RecordedInvocation(
                action.Type,
                context.TriggerAction,
                context.SourceState?.StageKey,
                context.TargetState.StageKey,
                new Dictionary<string, object?>(context.FieldValues)));

            return Task.FromResult(WorkflowActionExecutionResult.Success(action.Type));
        }
    }

    private sealed record RecordedInvocation(
        string ActionType,
        string? TriggerAction,
        string? SourceState,
        string TargetState,
        IReadOnlyDictionary<string, object?> FieldValues);
}
