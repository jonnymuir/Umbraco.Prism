using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Seeds the "Juggling Licence Decision" Umbraco Automate automation, so the config-only webhook
/// support system (<c>appsettings.json</c> -> <c>Wayfinder:SupportSystems</c>) has a real
/// automation ready and waiting, with no manual canvas build — TestSite's own copy of the pattern
/// <c>Wayfinder.Umbraco.ReferenceApp/AutomateCoachingStandardsSeeder.cs</c> establishes. Built
/// entirely in code via <see cref="IAutomationService"/>: a webhook trigger, an If branch on the
/// applicant's own declared licence type, a push notification + resolve step per outcome, and a
/// human approval step (Automate's own built-in <c>umbracoAutomate.requestApproval</c>) for the
/// two licence types that don't fast-track.
/// <para/>
/// Runs as a background service, not a startup notification handler: Automate has no default
/// workspace on a fresh install and stands one up lazily, so this polls (and creates one itself
/// if needed) before seeding. Create-or-update then publish on every boot, so a stale automation
/// signed with a previous run's key is refreshed. Never throws: a demo automation failing to seed
/// must not stop the site.
/// </summary>
public sealed class JugglingLicenceDecisionAutomationSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IRuntimeState runtimeState,
    ILogger<JugglingLicenceDecisionAutomationSeeder> logger)
    : BackgroundService
{
    // Must match the automation guid in appsettings.json's Wayfinder:SupportSystems[0].endpoint.url.
    private static readonly Guid AutomationId = new("6a99b1ce-0000-0000-0000-00000000c0de");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait for Umbraco to reach Run (past unattended install) and for Automate's own
            // schema migration to finish, then seed (creating a workspace first if none exists).
            for (var attempt = 0; attempt < 60 && !stoppingToken.IsCancellationRequested; attempt++)
            {
                if (runtimeState.Level == RuntimeLevel.Run && await TrySeedAsync(stoppingToken))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }

            logger.LogWarning("JUGGLING LICENCE DECISION SEEDER: gave up seeding the automation.");
        }
        catch (OperationCanceledException)
        {
            // Host shutting down.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JUGGLING LICENCE DECISION SEEDER: could not seed the automation.");
        }
    }

    private async Task<bool> TrySeedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        // Mirror whatever the config-driven WebhookSupportSystemClient will actually send, so the
        // seeded trigger's authenticator and the client's outbound signature always agree.
        const string authSection = "Wayfinder:SupportSystems:0:endpoint:auth";
        var signingKey = string.Equals(configuration[$"{authSection}:type"], "hmac-sha256", StringComparison.OrdinalIgnoreCase)
            ? configuration[configuration[$"{authSection}:secretRef"] ?? "JUGGLING_LICENCE_SIGNING_KEY"]
            : null;

        // Automate has no default workspace on a fresh install; an automation must belong to one,
        // and publishing requires the workspace to have a service-account user with the sections
        // its trigger and actions need. Reuse the first workspace if one already exists (Vinyl
        // Vault/mobile demos never create one, so on a fresh TestSite this is the first Automate
        // feature to run), otherwise stand up a plain one whose service account is TestSite's own
        // unattended admin (an Administrator, so it has every section — a deliberate shortcut for
        // a single-tenant demo, not a pattern for a real multi-user host).
        Workspace workspace;
        try
        {
            var (workspaces, _) = await workspaceService.GetWorkspacesPagedAsync(take: 1, cancellationToken: ct);
            workspace = workspaces.FirstOrDefault()
                ?? await workspaceService.CreateWorkspaceAsync(
                    new Workspace { Alias = "default", Name = "Default" }, cancellationToken: ct);

            if (workspace.ServiceAccountKey == Guid.Empty)
            {
                var adminEmail = configuration["Umbraco:CMS:Unattended:UnattendedUserEmail"] ?? "admin@prism.local";
                var admin = userService.GetByEmail(adminEmail);
                if (admin is null)
                {
                    return false;
                }

                workspace.ServiceAccountKey = admin.Key;
                await workspaceService.UpdateWorkspaceAsync(workspace, cancellationToken: ct);
            }
        }
        catch
        {
            // Automate's own schema/services not ready yet — try again next tick.
            return false;
        }

        var automation = BuildAutomation(workspace.Id, signingKey);

        var existing = await automationService.GetAutomationAsync(AutomationId, ct);
        if (existing is null)
        {
            await automationService.CreateAutomationAsync(automation, cancellationToken: ct);
            logger.LogInformation("JUGGLING LICENCE DECISION SEEDER: created automation {Id}.", AutomationId);
        }
        else
        {
            existing.Name = automation.Name;
            existing.Description = automation.Description;
            existing.WorkspaceId = automation.WorkspaceId;
            existing.Trigger = automation.Trigger;
            existing.Steps = automation.Steps;
            existing.Connections = automation.Connections;
            await automationService.UpdateAutomationAsync(existing, cancellationToken: ct);
            logger.LogInformation("JUGGLING LICENCE DECISION SEEDER: refreshed automation {Id}.", AutomationId);
        }

        await automationService.PublishAutomationAsync(AutomationId, cancellationToken: ct);
        logger.LogInformation("JUGGLING LICENCE DECISION SEEDER: published automation {Id}.", AutomationId);
        return true;
    }

    private static Automation BuildAutomation(Guid workspaceId, string? signingKey)
    {
        var resolveOwner = Guid.NewGuid();
        var checkLicenceType = Guid.NewGuid();
        var autoNotify = Guid.NewGuid();
        var autoResolve = Guid.NewGuid();
        var requestApproval = Guid.NewGuid();
        var approvedNotify = Guid.NewGuid();
        var approvedResolve = Guid.NewGuid();
        var referredNotify = Guid.NewGuid();
        var referredResolve = Guid.NewGuid();

        // The webhook envelope (docs/guides/support-systems.md) carries no member identity, only
        // instanceId — resolve the owner once, up front, so every later notification step can
        // bind to its output rather than each re-deriving it.
        var resolveOwnerStep = new StepConfiguration
        {
            Id = resolveOwner,
            ActionAlias = "wayfinder.resolveInstanceOwner",
            Name = "Resolve applicant",
            Position = new StepPosition { X = -400, Y = 0 },
            Settings = new() { ["instanceId"] = "${trigger.body.instanceId}" },
        };

        static StepConfiguration Notify(Guid id, string name, string title, string body, Guid resolveOwnerStepId, double x, double y) => new()
        {
            Id = id,
            ActionAlias = "prism.sendPushNotification",
            Name = name,
            Position = new StepPosition { X = x, Y = y },
            Settings = new()
            {
                // A step's own outputs sit directly under its GUID in the "steps" binding scope
                // (Umbraco.Automate.Core.Execution.BindingDataBuilder.AddStepEntry) — no
                // intermediate "output" segment, unlike this file's own earlier (wrong) guess.
                ["userId"] = $"${{steps.{resolveOwnerStepId}.userId}}",
                ["tenantId"] = $"${{steps.{resolveOwnerStepId}.tenantId}}",
                ["title"] = title,
                ["body"] = body,
                // Tapping the notification should land the applicant back on their (single,
                // auto-resumed) application instead of just opening the app to wherever it last was.
                ["deepLinkPath"] = TestSiteSeedContract.JugglingLicencePageUrl,
            },
        };

        // Resolves the invocation via the in-process ResolveWayfinderSupportSystemOutcomeAction
        // rather than an HTTP Request step: Automate's built-in HttpRequestAction blocks loopback
        // (SSRF protection), so an automation on the same box as Wayfinder cannot call the site
        // back over HTTP.
        static StepConfiguration Resolve(Guid id, string name, string outcome, string note, double x, double y) => new()
        {
            Id = id,
            ActionAlias = "wayfinder.resolveSupportSystemOutcome",
            Name = name,
            Position = new StepPosition { X = x, Y = y },
            Settings = new()
            {
                ["invocationId"] = "${trigger.body.invocationId}",
                ["outcomeKey"] = outcome,
                ["resultPayload"] = $$"""
                    {"applicationDecisionNote":"{{note}}"}
                    """,
            },
        };

        var steps = new List<StepConfiguration>
        {
            resolveOwnerStep,
            new()
            {
                Id = checkLicenceType,
                ActionAlias = "umbracoAutomate.if",
                Name = "Fast-track licence type?",
                Position = new StepPosition { X = 0, Y = 0 },
                Settings = new()
                {
                    ["conditions"] = new ConditionSet
                    {
                        Groups =
                        [
                            new ConditionGroup
                            {
                                Conditions =
                                [
                                    new Condition
                                    {
                                        LeftOperand = "${trigger.body.inputs.licenceType}",
                                        Operator = ConditionOperator.Equals,
                                        RightOperand = "Recreational",
                                    },
                                ],
                            },
                        ],
                    },
                },
            },
            Notify(autoNotify, "Notify: approved (auto)", "Juggling licence approved",
                "Good news — your recreational juggling licence has been approved automatically.", resolveOwner, 200, -160),
            Resolve(autoResolve, "Resolve: approved (auto)", "approved",
                "Automatically approved — recreational licences fast-track with no review.", 400, -160),
            new()
            {
                Id = requestApproval,
                ActionAlias = "umbracoAutomate.requestApproval",
                Name = "Approve competitive/professional licence?",
                Position = new StepPosition { X = 200, Y = 160 },
                Settings = new()
                {
                    ["prompt"] = "A competitive/professional juggling licence application needs a decision. Licence type: ${trigger.body.inputs.licenceType}.",
                    ["timeoutHours"] = 72,
                },
            },
            Notify(approvedNotify, "Notify: approved (reviewed)", "Juggling licence approved",
                "Good news — your juggling licence application has been approved.", resolveOwner, 400, 80),
            Resolve(approvedResolve, "Resolve: approved (reviewed)", "approved",
                "Approved after review.", 600, 80),
            Notify(referredNotify, "Notify: referred", "Juggling licence needs more information",
                "We need to take a closer look at your juggling licence application — we'll be in touch.", resolveOwner, 400, 240),
            Resolve(referredResolve, "Resolve: referred", "referred",
                "Referred for further review by the licensing team.", 600, 240),
        };

        // SourceHandle is the canvas node's output handle id; for a branching step it is the same
        // string as Outcome. Setting only Outcome routes the engine correctly but draws every
        // edge from the node's first handle in the editor, so a later "Save and publish" from the
        // canvas would re-serialise them all onto that one handle. Set both.
        var connections = new List<StepConnection>
        {
            // The trigger is step Guid.Empty in the graph; the compiler BFSes from it to find the
            // entry step, so the first real step must be wired to it or nothing is reachable.
            new() { SourceStepId = Guid.Empty, TargetStepId = resolveOwner },
            new() { SourceStepId = resolveOwner, TargetStepId = checkLicenceType },
            new() { SourceStepId = checkLicenceType, TargetStepId = autoNotify, SourceHandle = "true", Outcome = "true" },
            new() { SourceStepId = autoNotify, TargetStepId = autoResolve },
            new() { SourceStepId = checkLicenceType, TargetStepId = requestApproval, SourceHandle = "false", Outcome = "false" },
            new() { SourceStepId = requestApproval, TargetStepId = approvedNotify, SourceHandle = "approved", Outcome = "approved" },
            new() { SourceStepId = approvedNotify, TargetStepId = approvedResolve },
            new() { SourceStepId = requestApproval, TargetStepId = referredNotify, SourceHandle = "rejected", Outcome = "rejected" },
            new() { SourceStepId = referredNotify, TargetStepId = referredResolve },
        };

        var trigger = new TriggerConfiguration
        {
            TriggerAlias = "umbracoAutomate.webhook",
            Settings = new()
            {
                ["allowedMethod"] = "POST",
                // A signed webhook when the host supplies a key; otherwise a plain shared secret
                // equal to the key name's absence is meaningless, so fall back to an
                // unauthenticated webhook for a bare `dotnet run` (trusted-loopback demo only).
                ["authenticator"] = string.IsNullOrEmpty(signingKey)
                    ? new Dictionary<string, object?> { ["alias"] = "plain-secret", ["settings"] = new Dictionary<string, object?> { ["secret"] = "" } }
                    : new Dictionary<string, object?> { ["alias"] = "hmac-sha256", ["settings"] = new Dictionary<string, object?> { ["signingKey"] = signingKey } },
            },
        };

        return new Automation
        {
            Id = AutomationId,
            Alias = "juggling-licence-decision",
            Name = "Juggling Licence Decision",
            Description = "Resolves the juggling-licence-decision support system (appsettings.json Wayfinder:SupportSystems). Seeded by JugglingLicenceDecisionAutomationSeeder.",
            Status = AutomationStatus.Draft,
            WorkspaceId = workspaceId,
            Trigger = trigger,
            Steps = steps,
            Connections = connections,
        };
    }
}
