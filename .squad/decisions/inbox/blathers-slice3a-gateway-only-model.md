---
author: blathers
date: 2026-05-30T12:35:00+01:00
status: applied
area: workflow-editor-authoring
confidence: high
commit: a251bcd
branch: squad/82-named-lanes-editor-slice
---

# Decision: Slice 3a — gateway-only authoring model locked on the server

Per Jonny's 2026-05-30T11:05 directive answers and Tom Nook's scope-reset plan, the C# authoring contract is now stages + gateways only. This drop summarises the new validator rules, the `AuthoredTransition` rename, and migration guidance for any remaining callers.

## Validator rules now in force

The schema validator (`AuthoredWorkflowSchemaValidator.Validate`) enforces the canonical model with three numbered rules in the PROJ14x band:

| Code | Trigger | Message |
|------|---------|---------|
| **PROJ140** | Stage carries the retired `"Waiting"` / `"StatusTimeline"` type token (case-insensitive) **or** any stage-level `"waiting"` payload on disk. | `Stage '{key}' cannot author waiting state. Waiting belongs on join gateways.` |
| **PROJ141** | `transition.source` and `transition.target` are both stage keys. | `Transition '{src}' → '{dst}' is invalid. Route through a gateway instead of linking stages directly.` |
| **PROJ142** *(new)* | `transition.source` is a gateway key **and** `transition.target` is a gateway whose `Kind == Split`. | `Transition '{src}' → '{dst}' is invalid. Gateways may only transition to a stage or to a join gateway.` |

PROJ140 fires at the **JSON boundary**, not the typed object boundary: the `StageKind` enum no longer has `Waiting` or `StatusTimeline` members (Jonny's directive), so anything authored against them is deserialised as `StageKind.Question` and the original raw token is preserved on `AuthoredStage.LegacyKindRaw` for the validator to inspect. This means:

- In-process construction `new AuthoredStage { Kind = StageKind.Waiting }` will **fail to compile** — there is no such enum value anymore. This is intentional.
- JSON documents on disk with `"type": "Waiting"` still parse (no `JsonException`), but are guaranteed to produce PROJ140 and block projection.

## `AuthoredTransition` rename

Field rename (Jonny's directive: triggers/conditions live on the source gateway's outgoing route; transitions are an emergent property of routing):

| Old | New |
|-----|-----|
| `FromStage` | `Source` |
| `ToStage` | `Target` |
| `Action` | `Trigger` |

Three migration shims live on `AuthoredTransition`, all `[JsonIgnore]` and `[Obsolete("Use Source/Target/Trigger. Removed in next major.", error: false)]`:

- `string FromStage` → wraps `Source`
- `string ToStage` → wraps `Target`
- `string Action` → wraps `Trigger`

JSON read-side shims (`[JsonPropertyName("fromStage")]`, `("toStage")`, `("action")`) **remain in place** for forward compatibility with older authored documents on disk. The JSON write side now emits `source`/`target`/`trigger`.

## Migration guidance for callers

If you maintain code that touches `AuthoredTransition`:

1. **Rename property access.** `t.FromStage` → `t.Source`, `t.ToStage` → `t.Target`, `t.Action` → `t.Trigger`. The shims still work but produce `CS0618` warnings; treat them as breakage on next major.
2. **Object initialisers:** `new AuthoredTransition { FromStage = "a", ToStage = "b", Action = "submit" }` keeps compiling (init-only shim setters) but will eventually disappear. Switch to `Source`/`Target`/`Trigger`.
3. **JSON documents on disk:** no action required. The reader still accepts `"fromStage"`/`"toStage"`/`"action"` JSON properties. New documents will write the new names.
4. **DO NOT touch `WorkflowTransitionFile.Action`** in `UmbracoPrism.Shared` — that is the *runtime* transition contract and keeps its existing field names. The rename only applies to the *authoring* type.
5. **`AuthoredHandoff.FromStage`/`ToStage` are unrelated** — that record models cross-actor handoffs and was not renamed.

## Drops on the floor

- `WaitingMetadata` survives but is now **join-gateway-only** (`AuthoredGateway.WaitingInfo`). The `AuthoredStage.Waiting` property is gone.
- `WorkflowProjector.EmitWaitingComponents` is deleted. `WaitingComponent` itself stays in the Shared runtime package and is still emitted via the join-gateway path; only the stage-level shell route is removed.
- `EmitUnknownKind` (which warns PROJ005 and defaults to a fieldset) is the catch-all for any unexpected `StageKind` value at projection time. This effectively never fires post-slice because the enum is now closed, but the safety net stays.

## Simulator behaviour

`WorkflowSimulationService` walks through `Split` gateways transparently (one author "step" = stage → split → next stage) and pauses at `Join` gateways with `StopReason = "waiting-gateway"`. Tested in the new `WorkflowSimulationServiceTests.cs`.

## Verification

- `dotnet build UmbracoPrism.sln`: 0W / 0E
- `dotnet test ... --filter ~UmbracoPrism.Core.Tests`: **845 passed**, 0 failed, 0 skipped
- Grep for `StageKind.Waiting` / `StageKind.StatusTimeline` in `src/`: zero hits
- Grep for `.FromStage` / `.ToStage` / `.Action` on `AuthoredTransition` outside the shim definitions: zero hits

## Open follow-ups (not blocking this slice)

- Frontend types in `src/UmbracoPrism.Client/src/workflow-editor/` (Isabelle's lane) still need the matching rename to drop `fromStage`/`toStage`/`action` on the TS side. Tracked in her concurrent inspector/outline slice (`stash@{1}` at directive-time, popped concurrently).
- Authoring-fixture `Handoff` records still carry `FromStage`/`ToStage` (different type, intentional).
- Removing the `[Obsolete]` shims is a "next major" task — coordinate with any downstream consumers before deletion.
