/**
 * Client-side serviceBlueprint definition types aligned with Prism's queue-only
 * authored contract. Canonical JSON serialises top-level queues plus routes
 * owned by stages and gateways.
 */

// ---------------------------------------------------------------------------
// Canonical serviceBlueprint definition
// ---------------------------------------------------------------------------

export interface AuthoredServiceBlueprint {
  definitionKey: string;
  displayName: string;
  version: number;
  initialStage: string;
  requestPolicy: string;
  description?: string;
  schemaVersion?: string;
  queues?: QueueDefinition[];
  stages: AuthoredStage[];
  gateways?: AuthoredGateway[];
  calculations?: ServiceBlueprintCalculationsBlock;
  parameterSchemas?: AuthoredParameterSchema[];
  layout?: ServiceBlueprintLayoutBlock;
  metadata?: ServiceBlueprintDefinitionMetadata;
  transitions?: AuthoredTransition[];
  authorNote?: string;
}

/**
 * Editor canvas layout hints — manually arranged node positions keyed by
 * prefixed node id (`stage:<stateKey>` / `gateway:<key>`). Owned by the
 * editor; the service blueprint runtime never reads it. Nodes without an entry fall
 * back to the derived auto-layout, and queue membership (queueKey) stays
 * authoritative for which swim lane a node belongs to.
 */
export interface ServiceBlueprintLayoutBlock {
  nodes?: Record<string, ServiceBlueprintNodePosition>;
}

export interface ServiceBlueprintNodePosition {
  x: number;
  y: number;
}

/**
 * The definition's declarative calculations block (tables + fields + series).
 * The editor does not author this yet — it must round-trip it untouched; the
 * authoritative schema lives in UmbracoPrism.Shared (ServiceBlueprintCalculationSet).
 */
export interface ServiceBlueprintCalculationsBlock {
  tables?: Record<string, { interpolate?: string; values: Record<string, number> }>;
  fields: Record<string, { expr?: string; source?: string; format?: string }>;
  series?: Record<string, { over: string; from: string; to: string; values: Record<string, string> }>;
}

export interface ServiceBlueprintDefinitionMetadata {
  authoredServiceBlueprintId?: string;
  description?: string;
  schemaVersion?: string;
  gateways?: AuthoredGateway[];
  handoffs?: ServiceBlueprintHandoffDefinition[];
  tags?: Record<string, string>;
}

export interface QueueDefinition {
  key: string;
  displayName: string;
  description?: string;
  actor?: string;
  roleGates?: string[];
  tags?: Record<string, string>;
  queueName?: string;
}

export interface ServiceBlueprintHandoffDefinition {
  id: string;
  fromState: string;
  toState: string;
  label: string;
  actorChange?: string;
}

// ---------------------------------------------------------------------------
// Stages
// ---------------------------------------------------------------------------

export interface AuthoredStage {
  stateKey: string;
  displayName: string;
  components?: AuthoredComponent[];
  description?: string;
  kind?: StageKind;
  actor?: string;
  queueKey?: string;
  routes?: AuthoredRoute[];
  actions?: AuthoredAction[];
  roleGates?: string[];
  editorComment?: string;
  metadata?: ServiceBlueprintStateMetadata;
  /** Curated icon-set key (see graph/node-icons.ts). Falls back to a kind-based default when unset. */
  icon?: string;
}

export interface ServiceBlueprintStateMetadata {
  description?: string;
  stageType?: StageKind;
  actor?: string;
  queueKey?: string;
  queueName?: string;
  roleGates?: string[];
  actions?: AuthoredAction[];
  editorComment?: string;
  waiting?: WaitingMetadata;
}

// ---------------------------------------------------------------------------
// Gateways / transitions
// ---------------------------------------------------------------------------

export interface AuthoredGateway {
  key: string;
  displayName: string;
  description?: string;
  gatewayType: GatewayKind;
  kind?: GatewayKind;
  queueKey?: string;
  actor?: string;
  roleGates?: string[];
  routes?: AuthoredRoute[];
  actions?: AuthoredAction[];
  waitingContent?: string;
  waitingExpectedSeconds?: number;
  waitingPollIntervalMs?: number;
  waitingAllowDefer?: boolean;
  waitingDeferMessage?: string;
  requiredIncomingQueues?: string[];
  gatewayKey?: string;
  queueName?: string;
  source?: string;
  waiting?: WaitingMetadata;
  /** Curated icon-set key (see graph/node-icons.ts). Falls back to a kind-based default when unset. */
  icon?: string;
}

export interface AuthoredTransition {
  fromState: string;
  toState: string;
  action: string;
  requiresRole?: string;
  metadata?: ServiceBlueprintTransitionMetadata;
  target?: string;
  trigger?: string;
  condition?: string;
  actions?: AuthoredAction[];
  editorComment?: string;
}

export interface ServiceBlueprintTransitionMetadata {
  conditions?: ServiceBlueprintConditionDefinition[];
  actions?: AuthoredAction[];
}

export interface ServiceBlueprintConditionDefinition {
  kind: string;
  expression: string;
  description?: string;
}

// Closed union — mirrors the C# StageKind enum exactly.
export type StageKind =
  | 'Question'
  | 'CheckAnswers'
  | 'Confirmation'
  | 'TaskList';

export type GatewayKind = 'Split' | 'Join';

export type EditorStageType =
  | 'form'
  | 'review'
  | 'decision'
  | 'confirmation';

export function stageKindToEditorStageType(kind: StageKind): EditorStageType {
  switch (kind) {
    case 'CheckAnswers':
      return 'review';
    case 'TaskList':
      return 'decision';
    case 'Confirmation':
      return 'confirmation';
    case 'Question':
    default:
      return 'form';
  }
}

export function editorStageTypeToStageKind(type: EditorStageType): StageKind {
  switch (type) {
    case 'review':
      return 'CheckAnswers';
    case 'decision':
      return 'TaskList';
    case 'confirmation':
      return 'Confirmation';
    case 'form':
    default:
      return 'Question';
  }
}

export type EditorActor = 'public' | 'member' | 'reviewer' | 'system';

export function actorToEditorActor(actor?: string): EditorActor {
  const normalised = actor?.trim().toLowerCase() ?? '';

  if (!normalised || ['public', 'applicant', 'resident', 'citizen', 'customer'].includes(normalised)) {
    return 'public';
  }

  if (normalised === 'member') {
    return 'member';
  }

  if (['reviewer', 'caseworker', 'officer', 'administrator', 'admin'].includes(normalised)) {
    return 'reviewer';
  }

  if (normalised === 'system') {
    return 'system';
  }

  return normalised.includes('review') || normalised.includes('case') ? 'reviewer' : 'public';
}

export function editorActorToActor(actor: EditorActor): string {
  switch (actor) {
    case 'member':
      return 'member';
    case 'reviewer':
      return 'reviewer';
    case 'system':
      return 'system';
    case 'public':
    default:
      return 'public';
  }
}

export interface WaitingMetadata {
  content?: string;
  expectedWaitSeconds?: number;
  pollIntervalMs?: number;
  allowDefer: boolean;
  deferMessage?: string;
}

export function serviceBlueprintStages(serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages'> | null | undefined): AuthoredStage[] {
  return serviceBlueprint?.stages ?? [];
}

export function serviceBlueprintTransitions(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'> | null | undefined
): AuthoredTransition[] {
  return buildLegacyTransitions(serviceBlueprint);
}

export function serviceBlueprintMetadata(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'metadata'> | null | undefined
): ServiceBlueprintDefinitionMetadata | undefined {
  return serviceBlueprint?.metadata;
}

export function serviceBlueprintGateways(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'gateways'> | Pick<AuthoredServiceBlueprint, 'metadata'> | null | undefined
): AuthoredGateway[] {
  return (serviceBlueprint as AuthoredServiceBlueprint | null | undefined)?.gateways
    ?? (serviceBlueprint as AuthoredServiceBlueprint | null | undefined)?.metadata?.gateways
    ?? [];
}

export function serviceBlueprintQueues(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'queues'> | null | undefined
): QueueDefinition[] {
  return (serviceBlueprint as AuthoredServiceBlueprint | null | undefined)?.queues ?? [];
}

export function stageActions(stage: Pick<AuthoredStage, 'actions' | 'metadata'>): AuthoredAction[] {
  return stage.actions ?? stage.metadata?.actions ?? [];
}

export function stageRoleGates(stage: Pick<AuthoredStage, 'roleGates' | 'metadata'>): string[] {
  return stage.roleGates ?? stage.metadata?.roleGates ?? [];
}

export function stageLane(stage: Pick<AuthoredStage, 'queueKey' | 'metadata'>): string | undefined {
  return stage.queueKey ?? stage.metadata?.queueKey ?? stage.metadata?.queueName;
}

export function stageActor(stage: Pick<AuthoredStage, 'actor' | 'metadata'>): string | undefined {
  return stage.actor ?? stage.metadata?.actor;
}

export function stageKind(stage: Pick<AuthoredStage, 'kind' | 'metadata'>): StageKind {
  return stage.kind ?? stage.metadata?.stageType ?? 'Question';
}

export function stageDescription(stage: Pick<AuthoredStage, 'description' | 'metadata'>): string | undefined {
  return stage.description ?? stage.metadata?.description;
}

export function stageEditorComment(stage: Pick<AuthoredStage, 'editorComment' | 'metadata'>): string | undefined {
  return stage.editorComment ?? stage.metadata?.editorComment;
}

export function stageWaiting(stage: Pick<AuthoredStage, 'metadata'>): WaitingMetadata | undefined {
  return stage.metadata?.waiting;
}

export function withStageMetadata(stage: AuthoredStage, metadata: ServiceBlueprintStateMetadata): AuthoredStage {
  return hydrateStage({
    ...stage,
    description: metadata.description ?? stage.description,
    kind: metadata.stageType ?? stage.kind,
    actor: metadata.actor ?? stage.actor,
    queueKey: metadata.queueKey ?? metadata.queueName ?? stage.queueKey,
    actions: metadata.actions ?? stage.actions,
    roleGates: metadata.roleGates ?? stage.roleGates,
    editorComment: metadata.editorComment ?? stage.editorComment,
  });
}

export function withStageKind(stage: AuthoredStage, nextKind: StageKind): AuthoredStage {
  return hydrateStage({ ...stage, kind: nextKind });
}

export function withStageAssignment(stage: AuthoredStage, queueKey: string, actor?: string, roleGates: string[] = []): AuthoredStage {
  return hydrateStage({
    ...stage,
    queueKey,
    actor,
    roleGates,
  });
}

export function withStageKey(stage: AuthoredStage, stateKey: string): AuthoredStage {
  return hydrateStage({ ...stage, stateKey });
}

export function gatewayKey(gateway: Pick<AuthoredGateway, 'key'>): string {
  return gateway.key;
}

export function gatewayKind(gateway: Pick<AuthoredGateway, 'gatewayType' | 'kind'>): GatewayKind {
  return gateway.kind ?? gateway.gatewayType;
}

export function gatewayRoleGates(gateway: Pick<AuthoredGateway, 'roleGates'>): string[] {
  return gateway.roleGates ?? [];
}

export function transitionActions(transition: Pick<AuthoredTransition, 'metadata' | 'actions'>): AuthoredAction[] {
  return transition.actions ?? transition.metadata?.actions ?? [];
}

export function transitionConditions(
  transition: Pick<AuthoredTransition, 'metadata' | 'condition'>
): ServiceBlueprintConditionDefinition[] {
  if (transition.metadata?.conditions?.length) {
    return transition.metadata.conditions;
  }
  return transition.condition
    ? [{ kind: 'expression', expression: transition.condition }]
    : [];
}

function defineCompatGetter(target: object, key: string, getter: () => unknown) {
  if (Object.prototype.hasOwnProperty.call(target, key)) {
    return;
  }
  Object.defineProperty(target, key, {
    configurable: true,
    enumerable: false,
    get: getter,
  });
}

export function hydrateServiceBlueprintDefinition<T extends AuthoredServiceBlueprint>(serviceBlueprint: T): T {
  const root = serviceBlueprint as unknown as Record<string, unknown>;
  const metadata = asRecord(root.metadata);
  const rawStates = asArray<Record<string, unknown>>(root.stages);
  const rawGateways = asArray<Record<string, unknown>>(root.gateways ?? metadata.gateways);
  const rawTransitions = asArray<Record<string, unknown>>(root.transitions);
  const rawQueues = dedupeByKey(
    asArray<Record<string, unknown>>(root.queues)
      .map(normaliseQueueDefinition).filter((queue): queue is QueueDefinition => Boolean(queue)),
    queue => queue.key
  );
  const queueLookup = buildQueueLookup(rawQueues, rawStates, rawGateways);
  const normalisedGateways = rawGateways.map(rawGateway => hydrateGateway(normaliseGateway(rawGateway, queueLookup, rawTransitions)));
  const normalisedStates = rawStates.map(rawStage => hydrateStage(normaliseStage(rawStage, queueLookup, rawTransitions, rawGateways)));

  const normalisedServiceBlueprint = {
    definitionKey: typeof root.definitionKey === 'string' ? root.definitionKey : '',
    displayName: typeof root.displayName === 'string' ? root.displayName : '',
    version: typeof root.version === 'number' ? root.version : 1,
    initialStage: firstString(root.initialStage) ?? normalisedStates[0]?.stateKey ?? '',
    requestPolicy: typeof root.requestPolicy === 'string' ? root.requestPolicy : 'single',
    description: firstString(root.description, metadata.description, root.authorNote),
    schemaVersion: firstString(root.schemaVersion, metadata.schemaVersion),
    queues: rawQueues,
    stages: normalisedStates,
    gateways: normalisedGateways,
    calculations: root.calculations && typeof root.calculations === 'object' && !Array.isArray(root.calculations)
      ? root.calculations as ServiceBlueprintCalculationsBlock
      : undefined,
    parameterSchemas: asArray<AuthoredParameterSchema>(root.parameterSchemas),
    layout: sanitiseLayoutBlock(root.layout),
  } as AuthoredServiceBlueprint;

  const legacyMetadata: ServiceBlueprintDefinitionMetadata = {
    authoredServiceBlueprintId: typeof metadata.authoredServiceBlueprintId === 'string' ? metadata.authoredServiceBlueprintId : undefined,
    handoffs: asArray<ServiceBlueprintHandoffDefinition>(metadata.handoffs),
    tags: asRecord(metadata.tags) as Record<string, string>,
  };

  defineCompatGetter(normalisedServiceBlueprint, 'authorNote', () => normalisedServiceBlueprint.description);
  defineCompatGetter(normalisedServiceBlueprint, 'metadata', () => legacyMetadata);
  defineCompatGetter(legacyMetadata, 'description', () => normalisedServiceBlueprint.description);
  defineCompatGetter(legacyMetadata, 'schemaVersion', () => normalisedServiceBlueprint.schemaVersion);
  defineCompatGetter(legacyMetadata, 'gateways', () => normalisedServiceBlueprint.gateways);
  defineCompatGetter(normalisedServiceBlueprint, 'transitions', () => buildLegacyTransitions(normalisedServiceBlueprint));

  return normalisedServiceBlueprint as T;
}

function sanitiseLayoutBlock(value: unknown): ServiceBlueprintLayoutBlock | undefined {
  const record = asRecord(value);
  const nodes = asRecord(record.nodes);
  const entries: Record<string, ServiceBlueprintNodePosition> = {};
  for (const [key, raw] of Object.entries(nodes)) {
    const position = asRecord(raw);
    if (
      typeof position.x === 'number' && Number.isFinite(position.x)
      && typeof position.y === 'number' && Number.isFinite(position.y)
    ) {
      entries[key] = { x: position.x, y: position.y };
    }
  }
  return Object.keys(entries).length > 0 ? { nodes: entries } : undefined;
}

function firstString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
  }
  return undefined;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function asArray<T>(value: unknown): T[] {
  return Array.isArray(value) ? value as T[] : [];
}

function asStringArray(value: unknown): string[] {
  return asArray<unknown>(value)
    .filter((entry): entry is string => typeof entry === 'string' && entry.trim().length > 0)
    .map(entry => entry.trim());
}

function dedupeByKey<T>(items: T[], keyFor: (item: T) => string): T[] {
  const seen = new Set<string>();
  return items.filter(item => {
    const key = keyFor(item);
    if (!key || seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function normaliseQueueDefinition(rawQueue: Record<string, unknown>): QueueDefinition | null {
  const key = firstString(rawQueue.key, rawQueue.queueName);
  if (!key) {
    return null;
  }
  return {
    key,
    queueName: key,
    displayName: firstString(rawQueue.displayName, rawQueue.title, rawQueue.key, rawQueue.queueName) ?? key,
    description: firstString(rawQueue.description),
    actor: firstString(rawQueue.actor),
    roleGates: asStringArray(rawQueue.roleGates),
    tags: asRecord(rawQueue.tags) as Record<string, string>,
  };
}

function buildQueueLookup(
  queues: QueueDefinition[],
  rawStates: Array<Record<string, unknown>>,
  rawGateways: Array<Record<string, unknown>>
): Map<string, string> {
  const lookup = new Map<string, string>();
  queues.forEach(queue => {
    lookup.set(queue.key, queue.key);
    if (queue.queueName) {
      lookup.set(queue.queueName, queue.key);
    }
  });

  const registerQueueKey = (rawNode: Record<string, unknown>) => {
    const queueKey = firstString(
      rawNode.queueKey,
      rawNode.queueName,
      asRecord(rawNode.metadata).queueKey,
      asRecord(rawNode.metadata).queueName
    );
    if (queueKey) {
      lookup.set(queueKey, queueKey);
    }
  };

  rawStates.forEach(registerQueueKey);
  rawGateways.forEach(registerQueueKey);
  return lookup;
}

function resolveQueueKey(rawNode: Record<string, unknown>, queueLookup: Map<string, string>): string | undefined {
  const candidates = [
    rawNode.queueKey,
    rawNode.queueName,
    asRecord(rawNode.metadata).queueKey,
    asRecord(rawNode.metadata).queueName,
  ];
  for (const candidate of candidates) {
    if (typeof candidate === 'string' && candidate.trim()) {
      return queueLookup.get(candidate.trim()) ?? candidate.trim();
    }
  }
  return undefined;
}

function routeId(sourceKey: string, trigger: string, targetKey: string) {
  return `${sourceKey || 'unknown'}--${trigger || 'continue'}--${targetKey || 'unknown'}`;
}

function normaliseLegacyTransitionRoute(
  sourceKey: string,
  transition: Record<string, unknown>
): AuthoredRoute {
  const trigger = firstString(transition.action, transition.trigger) ?? 'continue';
  return {
    id: firstString(transition.id) ?? routeId(sourceKey, trigger, firstString(transition.toState, transition.target) ?? ''),
    target: firstString(transition.toState, transition.target) ?? '',
    trigger,
    condition: firstString(
      transition.condition,
      asRecord(transition.metadata).conditions && Array.isArray(asRecord(transition.metadata).conditions)
        ? (asRecord(asArray(asRecord(transition.metadata).conditions)[0]).expression as string | undefined)
        : undefined
    ),
    requiresRole: firstString(transition.requiresRole),
    actions: transitionActions(transition as unknown as AuthoredTransition),
    editorComment: firstString(transition.editorComment),
  };
}

function normaliseRoute(rawRoute: Record<string, unknown>, sourceKey: string): AuthoredRoute {
  const trigger = firstString(rawRoute.trigger, rawRoute.action) ?? 'continue';
  return {
    id: firstString(rawRoute.id) ?? routeId(sourceKey, trigger, firstString(rawRoute.target, rawRoute.toState) ?? ''),
    target: firstString(rawRoute.target, rawRoute.toState) ?? '',
    trigger,
    label: firstString(rawRoute.label),
    style: firstString(rawRoute.style),
    condition: firstString(rawRoute.condition),
    requiresRole: firstString(rawRoute.requiresRole),
    actions: asArray<AuthoredAction>(rawRoute.actions),
    editorComment: firstString(rawRoute.editorComment),
  };
}

function normaliseStage(
  rawStage: Record<string, unknown>,
  queueLookup: Map<string, string>,
  rawTransitions: Array<Record<string, unknown>>,
  rawGateways: Array<Record<string, unknown>>
): AuthoredStage {
  const metadata = asRecord(rawStage.metadata);
  const stateKey = firstString(rawStage.stageKey, rawStage.stateKey, rawStage.key) ?? '';
  const transitionRoutes = rawTransitions
    .filter(transition => firstString(transition.fromState) === stateKey)
    .map(transition => normaliseLegacyTransitionRoute(stateKey, transition));
  const sourcedGatewayRoutes = rawGateways
    .filter(rawGateway => firstString(rawGateway.source) === stateKey)
    .map(rawGateway => {
      const gatewayKey = firstString(rawGateway.key, rawGateway.gatewayKey) ?? '';
      const firstGatewayRoute = asArray<Record<string, unknown>>(rawGateway.routes)[0];
      const trigger = firstString(firstGatewayRoute?.trigger, firstGatewayRoute?.action) ?? 'continue';
      return {
        id: routeId(stateKey, trigger, gatewayKey),
        target: gatewayKey,
        trigger,
      } satisfies AuthoredRoute;
    });
  const routes = dedupeByKey(
    [
      ...asArray<Record<string, unknown>>(rawStage.routes).map(route => normaliseRoute(route, stateKey)),
      ...transitionRoutes,
      ...sourcedGatewayRoutes,
    ],
    route => route.id
  );

  return hydrateStage({
    stateKey,
    displayName: firstString(rawStage.displayName, rawStage.title) ?? stateKey,
    components: asArray<AuthoredComponent>(rawStage.components),
    description: firstString(rawStage.description, metadata.description),
    kind: firstString(rawStage.stageType, rawStage.kind, rawStage.type, metadata.stageType) as StageKind | undefined,
    actor: firstString(rawStage.actor, metadata.actor),
    queueKey: resolveQueueKey(rawStage, queueLookup),
    routes,
    actions: asArray<AuthoredAction>(rawStage.actions ?? metadata.actions),
    roleGates: asStringArray(rawStage.roleGates ?? metadata.roleGates),
    editorComment: firstString(rawStage.editorComment, metadata.editorComment),
    icon: firstString(rawStage.icon),
  });
}

function normaliseGateway(
  rawGateway: Record<string, unknown>,
  queueLookup: Map<string, string>,
  rawTransitions: Array<Record<string, unknown>>
): AuthoredGateway {
  const key = firstString(rawGateway.key, rawGateway.gatewayKey) ?? '';
  const metadata = asRecord(rawGateway.metadata);
  const transitionRoutes = rawTransitions
    .filter(transition => firstString(transition.fromState) === key)
    .map(transition => normaliseLegacyTransitionRoute(key, transition));
  return hydrateGateway({
    key,
    displayName: firstString(rawGateway.displayName, rawGateway.title) ?? key,
    description: firstString(rawGateway.description, metadata.description),
    gatewayType: firstString(rawGateway.gatewayType, rawGateway.kind, rawGateway.type) as GatewayKind ?? 'Split',
    kind: firstString(rawGateway.kind, rawGateway.gatewayType, rawGateway.type) as GatewayKind ?? 'Split',
    queueKey: resolveQueueKey(rawGateway, queueLookup),
    actor: firstString(rawGateway.actor, metadata.actor),
    roleGates: asStringArray(rawGateway.roleGates ?? metadata.roleGates),
    routes: dedupeByKey(
      [
        ...asArray<Record<string, unknown>>(rawGateway.routes).map(route => normaliseRoute(route, key)),
        ...transitionRoutes,
      ],
      route => route.id
    ),
    waitingContent: firstString(rawGateway.waitingContent, asRecord(rawGateway.waiting).content, asRecord(rawGateway.waitingInfo).content),
    waitingExpectedSeconds: typeof rawGateway.waitingExpectedSeconds === 'number'
      ? rawGateway.waitingExpectedSeconds
      : typeof asRecord(rawGateway.waiting).expectedWaitSeconds === 'number'
        ? asRecord(rawGateway.waiting).expectedWaitSeconds as number
        : typeof asRecord(rawGateway.waitingInfo).expectedWaitSeconds === 'number'
          ? asRecord(rawGateway.waitingInfo).expectedWaitSeconds as number
          : undefined,
    waitingPollIntervalMs: typeof rawGateway.waitingPollIntervalMs === 'number'
      ? rawGateway.waitingPollIntervalMs
      : typeof asRecord(rawGateway.waiting).pollIntervalMs === 'number'
        ? asRecord(rawGateway.waiting).pollIntervalMs as number
        : typeof asRecord(rawGateway.waitingInfo).pollIntervalMs === 'number'
          ? asRecord(rawGateway.waitingInfo).pollIntervalMs as number
          : undefined,
    waitingAllowDefer: typeof rawGateway.waitingAllowDefer === 'boolean'
      ? rawGateway.waitingAllowDefer
      : typeof asRecord(rawGateway.waiting).allowDefer === 'boolean'
        ? asRecord(rawGateway.waiting).allowDefer as boolean
        : typeof asRecord(rawGateway.waitingInfo).allowDefer === 'boolean'
          ? asRecord(rawGateway.waitingInfo).allowDefer as boolean
          : undefined,
    waitingDeferMessage: firstString(rawGateway.waitingDeferMessage, asRecord(rawGateway.waiting).deferMessage, asRecord(rawGateway.waitingInfo).deferMessage),
    requiredIncomingQueues: asStringArray(rawGateway.requiredIncomingQueues)
      .map(queueKey => queueLookup.get(queueKey) ?? queueKey),
    icon: firstString(rawGateway.icon),
  });
}

function hydrateStage(stage: AuthoredStage): AuthoredStage {
  const hydrated = {
    ...stage,
    components: stage.components ?? [],
    kind: stage.kind ?? 'Question',
    actions: stage.actions ?? [],
    roleGates: stage.roleGates ?? [],
    routes: stage.routes ?? [],
  } as AuthoredStage;

  defineCompatGetter(hydrated, 'stageKey', () => hydrated.stateKey);
  defineCompatGetter(hydrated, 'metadata', () => ({
    description: hydrated.description,
    stageType: hydrated.kind,
    actor: hydrated.actor,
    queueKey: hydrated.queueKey,
    queueName: hydrated.queueKey,
    roleGates: hydrated.roleGates,
    actions: hydrated.actions,
    editorComment: hydrated.editorComment,
  } satisfies ServiceBlueprintStateMetadata));

  return hydrated;
}

function hydrateGateway(gateway: AuthoredGateway): AuthoredGateway {
  const hydrated = {
    ...gateway,
    gatewayType: gateway.gatewayType ?? gateway.kind ?? 'Split',
    kind: gateway.kind ?? gateway.gatewayType ?? 'Split',
    routes: gateway.routes ?? [],
    roleGates: gateway.roleGates ?? [],
    requiredIncomingQueues: gateway.requiredIncomingQueues ?? [],
  } as AuthoredGateway;

  defineCompatGetter(hydrated, 'gatewayKey', () => hydrated.key);
  defineCompatGetter(hydrated, 'queueName', () => hydrated.queueKey);
  defineCompatGetter(hydrated, 'waiting', () => ({
    content: hydrated.waitingContent,
    expectedWaitSeconds: hydrated.waitingExpectedSeconds,
    pollIntervalMs: hydrated.waitingPollIntervalMs,
    allowDefer: hydrated.waitingAllowDefer ?? false,
    deferMessage: hydrated.waitingDeferMessage,
  } satisfies WaitingMetadata));

  return hydrated;
}

function buildLegacyTransitions(
  serviceBlueprint: Pick<AuthoredServiceBlueprint, 'stages' | 'gateways'> | null | undefined
): AuthoredTransition[] {
  if (!serviceBlueprint) {
    return [];
  }

  const stageTransitions = serviceBlueprint.stages.flatMap(stage =>
    (stage.routes ?? []).map(route => {
      const metadata: ServiceBlueprintTransitionMetadata = {
        conditions: route.condition
          ? [{ kind: 'expression', expression: route.condition }]
          : undefined,
        actions: route.actions ?? [],
      };
      const transition: AuthoredTransition = {
        fromState: stage.stateKey,
        toState: route.target,
        action: route.trigger,
        requiresRole: route.requiresRole,
        metadata,
        condition: route.condition,
        actions: route.actions ?? [],
        editorComment: route.editorComment,
      };
      defineCompatGetter(transition, 'target', () => transition.toState);
      defineCompatGetter(transition, 'trigger', () => transition.action);
      return transition;
    })
  );

  const gatewayTransitions = (serviceBlueprint.gateways ?? []).flatMap(gateway =>
    (gateway.routes ?? []).map(route => {
      const metadata: ServiceBlueprintTransitionMetadata = {
        conditions: route.condition
          ? [{ kind: 'expression', expression: route.condition }]
          : undefined,
        actions: route.actions ?? [],
      };
      const transition: AuthoredTransition = {
        fromState: gateway.key,
        toState: route.target,
        action: route.trigger,
        requiresRole: route.requiresRole,
        metadata,
        condition: route.condition,
        actions: route.actions ?? [],
        editorComment: route.editorComment,
      };
      defineCompatGetter(transition, 'target', () => transition.toState);
      defineCompatGetter(transition, 'trigger', () => transition.action);
      return transition;
    })
  );

  return [...stageTransitions, ...gatewayTransitions];
}

// ---------------------------------------------------------------------------
// Route view
// ---------------------------------------------------------------------------
//
// Editor surfaces still render routes as a flattened view. The view is derived
// from the canonical transition list plus metadata.gateways.

/**
 * Read-only flattening of a transition into a route/editor view.
 */
export interface AuthoredRoute {
  id: string;
  target: string;
  trigger: string;
  label?: string;
  style?: string;
  condition?: string;
  requiresRole?: string;
  actions?: AuthoredAction[];
  editorComment?: string;
}

export interface RouteView {
  fromStage: string;
  toStage: string;
  action: string;
  actions?: AuthoredAction[];
  requiresRole?: string;
  condition?: string;
  editorComment?: string;
  fromGateway?: string;
  toGateway?: string;
  gatewayKey?: string;
  key?: string;
  routeIndex: number;
  routeId: string;
}

// ---------------------------------------------------------------------------
// Authored Action Catalog
// ---------------------------------------------------------------------------

export type ActionTiming = 'OnEntry' | 'OnExit' | 'OnTransition';

export interface AuthoredAction {
  type: string;
  timing: ActionTiming;
  params?: Record<string, unknown>;
  parameterSchemaKey?: string;
  summary?: string;
}

export type ParameterValueKind = 'String' | 'Number' | 'Integer' | 'Boolean' | 'Object' | 'Array' | 'Null';

export interface AuthoredParameterDefinition {
  key: string;
  title: string;
  description?: string;
  valueKind: ParameterValueKind;
  format?: string;
  editor?: string;
  allowedValues?: string[];
  defaultValue?: unknown;
  properties?: AuthoredParameterDefinition[];
  items?: AuthoredParameterDefinition | null;
}

export interface AuthoredParameterSchema {
  key: string;
  title: string;
  description?: string;
  appliesTo?: string[];
  valueKind?: ParameterValueKind;
  allowAdditionalProperties?: boolean;
  properties?: AuthoredParameterDefinition[];
  required?: string[];
}

export interface ActionCatalogEntry {
  type: string;
  label: string;
  summary: string;
  appliesTo: string[];
  paramsSchema: AuthoredParameterSchema;
  parameterWidgets?: Record<string, string>;
  defaultParams?: Record<string, unknown>;
  status?: string;
  runtimeImplementation?: string;
}

// ---------------------------------------------------------------------------
// Authored Components
// ---------------------------------------------------------------------------
//
// Stages carry a tree of `AuthoredComponent` instances directly — the same
// polymorphic hierarchy the runtime renders. Containers (fieldset, accordion,
// summary-list, panel), inputs (text, email, textarea, number, decimal,
// select, radio, checkboxlist, date, boolean), and content (body, heading,
// inset-text, warning-text, details, notification-banner, waiting,
// task-list) are all peers in this tree. The projector hands the tree
// straight through to the runtime.

interface AuthoredComponentBase {
  type: string;
}

export interface AuthoredInputComponent extends AuthoredComponentBase {
  type:
    | 'text'
    | 'number'
    | 'decimal'
    | 'select'
    | 'radio'
    | 'checkboxlist'
    | 'date'
    | 'email'
    | 'textarea'
    | 'boolean';
  fieldKey: string;
  label: string;
  hint?: string;
  required: boolean;
  conditionalOn?: string | null;
  visibleWhen?: string | null;
  options?: string[];
  minLength?: number | null;
  maxLength?: number | null;
  pattern?: string | null;
  prefix?: string | null;
  min?: number | null;
  max?: number | null;
}

export interface AuthoredFieldsetComponent extends AuthoredComponentBase {
  type: 'fieldset';
  children: AuthoredComponent[];
  legend?: string | null;
  legendSize?: string | null;
}

export interface AuthoredAccordionComponent extends AuthoredComponentBase {
  type: 'accordion';
  sections: Array<{
    heading: string;
    summary?: string | null;
    children: AuthoredComponent[];
  }>;
}

export interface AuthoredPanelComponent extends AuthoredComponentBase {
  type: 'panel';
  heading: string;
}

export interface AuthoredWaitingComponent extends AuthoredComponentBase {
  type: 'waiting';
  content: string;
  expectedWaitSeconds: number;
  pollIntervalMs: number;
  allowDefer: boolean;
  deferMessage?: string;
}

export interface AuthoredSummaryListComponent extends AuthoredComponentBase {
  type: 'summary-list';
  children: AuthoredComponent[];
  changeStateKey?: string | null;
  title?: string | null;
}

export interface AuthoredTaskListComponent extends AuthoredComponentBase {
  type: 'task-list';
  sections?: Array<{
    heading: string;
    tasks: Array<{ label: string; stateKey?: string | null; href?: string | null }>;
  }> | null;
}

export interface AuthoredContentComponent extends AuthoredComponentBase {
  type: 'body' | 'heading' | 'inset-text' | 'warning-text' | 'details' | 'notification-banner';
  content?: string;
  heading?: string;
  level?: number;
  bannerType?: string;
}

export type AuthoredComponent =
  | AuthoredInputComponent
  | AuthoredFieldsetComponent
  | AuthoredAccordionComponent
  | AuthoredPanelComponent
  | AuthoredWaitingComponent
  | AuthoredSummaryListComponent
  | AuthoredTaskListComponent
  | AuthoredContentComponent;

export type ActionFormFieldType = 'text' | 'number' | 'textarea' | 'select' | 'radio' | 'date';

export interface ActionFormFieldConfig {
  fieldKey: string;
  label: string;
  type: ActionFormFieldType;
  required: boolean;
  hintText?: string;
  validationPattern?: string;
  defaultValue?: string;
  options: string[];
}


// ---------------------------------------------------------------------------
// Stub data for Storybook / development
// ---------------------------------------------------------------------------

export const STUB_ACTION_CATALOG: ActionCatalogEntry[] = [
  {
    type: 'forms.load',
    label: 'Load form',
    summary: 'Load a forms-engine definition when a stage opens.',
    appliesTo: ['stage.onEntry'],
    paramsSchema: {
      key: 'forms.form-reference',
      title: 'Forms engine reference',
      valueKind: 'Object',
      properties: [
        {
          key: 'formDefinitionId',
          title: 'Form definition id',
          valueKind: 'String',
          editor: 'text',
        },
      ],
      required: ['formDefinitionId'],
    },
    defaultParams: { formDefinitionId: '' },
    status: 'available',
    runtimeImplementation: 'reference-business-app',
  },
  {
    type: 'forms.save',
    label: 'Save form',
    summary: 'Persist the current forms-engine payload before leaving a stage.',
    appliesTo: ['stage.onExit'],
    paramsSchema: {
      key: 'forms.form-reference',
      title: 'Forms engine reference',
      valueKind: 'Object',
      properties: [
        {
          key: 'formDefinitionId',
          title: 'Form definition id',
          valueKind: 'String',
          editor: 'text',
        },
      ],
      required: ['formDefinitionId'],
    },
    defaultParams: { formDefinitionId: '' },
    status: 'available',
    runtimeImplementation: 'reference-business-app',
  },
  {
    type: 'forms.submit',
    label: 'Submit form',
    summary: 'Validate and submit a forms-engine definition while taking a transition.',
    appliesTo: ['transition'],
    paramsSchema: {
      key: 'forms.form-reference',
      title: 'Forms engine reference',
      valueKind: 'Object',
      properties: [
        {
          key: 'formDefinitionId',
          title: 'Form definition id',
          valueKind: 'String',
          editor: 'text',
        },
      ],
      required: ['formDefinitionId'],
    },
    defaultParams: { formDefinitionId: '' },
    status: 'available',
    runtimeImplementation: 'reference-business-app',
  },
  {
    type: 'case.assign',
    label: 'Assign case',
    summary: 'Assign the current case to a role, queue, or named user.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'case.assign',
      title: 'Case assignment',
      valueKind: 'Object',
      properties: [
        {
          key: 'assigneeType',
          title: 'Assignment target type',
          valueKind: 'String',
          editor: 'select',
          allowedValues: ['role', 'queue', 'user'],
          defaultValue: 'role',
        },
        { key: 'assigneeValue', title: 'Assignment target', valueKind: 'String', editor: 'text' },
        {
          key: 'overwriteExisting',
          title: 'Overwrite existing assignment',
          valueKind: 'Boolean',
          editor: 'toggle',
          defaultValue: false,
        },
      ],
      required: ['assigneeType', 'assigneeValue'],
    },
    defaultParams: { assigneeType: 'role', assigneeValue: '', overwriteExisting: false },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'case.enqueue',
    label: 'Enqueue case',
    summary: 'Place the case into a named queue with an optional priority.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'case.enqueue',
      title: 'Queue placement',
      valueKind: 'Object',
      properties: [
        { key: 'queue', title: 'Queue', valueKind: 'String', editor: 'text' },
        {
          key: 'priority',
          title: 'Priority',
          valueKind: 'String',
          editor: 'select',
          allowedValues: ['low', 'normal', 'high'],
          defaultValue: 'normal',
        },
      ],
      required: ['queue'],
    },
    defaultParams: { queue: '', priority: 'normal' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'case.set-status',
    label: 'Set case status',
    summary: 'Update the case status shown to staff and applicants.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'case.set-status',
      title: 'Case status',
      valueKind: 'Object',
      properties: [
        { key: 'status', title: 'Status', valueKind: 'String', editor: 'text' },
        { key: 'reason', title: 'Reason', valueKind: 'String', editor: 'textarea' },
      ],
      required: ['status'],
    },
    defaultParams: { status: '', reason: '' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'case.add-note',
    label: 'Add case note',
    summary: 'Attach an internal or public note to the current case.',
    appliesTo: ['stage.onExit', 'transition'],
    paramsSchema: {
      key: 'case.add-note',
      title: 'Case note',
      valueKind: 'Object',
      properties: [
        { key: 'note', title: 'Note', valueKind: 'String', editor: 'textarea' },
        {
          key: 'visibility',
          title: 'Visibility',
          valueKind: 'String',
          editor: 'select',
          allowedValues: ['internal', 'public'],
          defaultValue: 'internal',
        },
      ],
      required: ['note'],
    },
    defaultParams: { note: '', visibility: 'internal' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'notifications.send-email',
    label: 'Send email',
    summary: 'Queue an email notification using a named template.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'notifications.send-email',
      title: 'Email notification',
      valueKind: 'Object',
      properties: [
        { key: 'templateId', title: 'Template id', valueKind: 'String', editor: 'text' },
        {
          key: 'recipientEmail',
          title: 'Recipient email',
          valueKind: 'String',
          format: 'email',
          editor: 'text',
        },
        { key: 'subject', title: 'Subject override', valueKind: 'String', editor: 'text' },
      ],
      required: ['templateId', 'recipientEmail'],
    },
    defaultParams: { templateId: '', recipientEmail: '', subject: '' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'notifications.send-sms',
    label: 'Send SMS',
    summary: 'Queue an SMS notification using a named template.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'notifications.send-sms',
      title: 'SMS notification',
      valueKind: 'Object',
      properties: [
        { key: 'templateId', title: 'Template id', valueKind: 'String', editor: 'text' },
        { key: 'recipientNumber', title: 'Recipient number', valueKind: 'String', editor: 'text' },
      ],
      required: ['templateId', 'recipientNumber'],
    },
    defaultParams: { templateId: '', recipientNumber: '' },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
  {
    type: 'forms.request-evidence',
    label: 'Request evidence form',
    summary: 'Ask the applicant for supporting evidence using a configured response form.',
    appliesTo: ['stage.onEntry', 'transition'],
    paramsSchema: {
      key: 'forms.request-evidence',
      title: 'Evidence request',
      valueKind: 'Object',
      properties: [
        { key: 'title', title: 'Prompt title', valueKind: 'String', editor: 'text' },
        { key: 'helpText', title: 'Intro help text', valueKind: 'String', editor: 'textarea' },
        { key: 'dueDate', title: 'Due date', valueKind: 'String', format: 'date', editor: 'date' },
        {
          key: 'fields',
          title: 'Fields',
          valueKind: 'Array',
          editor: 'collection',
          items: {
            key: 'field',
            title: 'Field',
            valueKind: 'Object',
            properties: [
              { key: 'fieldKey', title: 'Field key', valueKind: 'String', editor: 'text' },
              { key: 'label', title: 'Label', valueKind: 'String', editor: 'text' },
              {
                key: 'type',
                title: 'Field type',
                valueKind: 'String',
                editor: 'select',
                allowedValues: ['text', 'number', 'textarea', 'select', 'radio', 'date'],
                defaultValue: 'text',
              },
              { key: 'required', title: 'Required', valueKind: 'Boolean', editor: 'toggle', defaultValue: false },
              { key: 'hintText', title: 'Help text', valueKind: 'String', editor: 'textarea' },
              { key: 'validationPattern', title: 'Validation pattern', valueKind: 'String', editor: 'text' },
              { key: 'defaultValue', title: 'Default value', valueKind: 'String', editor: 'text' },
              {
                key: 'options',
                title: 'Options',
                valueKind: 'Array',
                editor: 'collection',
                items: {
                  key: 'option',
                  title: 'Option',
                  valueKind: 'String',
                  editor: 'text',
                },
              },
            ],
          },
        },
      ],
      required: ['title', 'fields'],
    },
    defaultParams: {
      title: 'Request supporting evidence',
      helpText: 'Explain what evidence the applicant should upload or complete.',
      dueDate: '',
      fields: [
        {
          fieldKey: 'supporting-evidence',
          label: 'Supporting evidence',
          type: 'select',
          required: true,
          hintText: 'Choose the evidence the applicant needs to provide.',
          validationPattern: '',
          defaultValue: '',
          options: ['Site photos', 'Ownership certificate', 'Tree survey'],
        },
      ],
    },
    status: 'planned',
    runtimeImplementation: 'planned',
  },
];

export const STUB_SERVICE_BLUEPRINT: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition(({
  definitionKey: 'planning-permission',
  displayName: 'Planning Permission Application',
  version: 1,
  requestPolicy: 'single',
  initialStage: 'applicant-details',
  stages: [
    {
      stateKey: 'applicant-details',
      displayName: 'Applicant Details',
      description: 'Collect applicant details and site context.',
      kind: 'Question',
      actor: 'public',
      actions: [
        {
          type: 'forms.load',
          timing: 'OnEntry',
          parameterSchemaKey: 'forms.form-reference',
          params: { formDefinitionId: 'planning-applicant-details' },
          summary: 'Load the applicant details form.',
        },
        {
          type: 'notifications.send-email',
          timing: 'OnEntry',
          parameterSchemaKey: 'notifications.send-email',
          params: {
            templateId: 'planning-started',
            recipientEmail: 'planning.officers@council.example',
            subject: 'Planning application started',
          },
          summary: 'Send email to Planning Officers',
        },
      ],
      components: [],
      roleGates: [],
    },
    {
      stateKey: 'check-answers',
      displayName: 'Check Your Answers',
      description: 'Review the captured answers before submission.',
      kind: 'CheckAnswers',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stateKey: 'reviewer-assessment',
      displayName: 'Reviewer Assessment',
      description: 'Internal assessment and decision making.',
      kind: 'Question',
      actor: 'reviewer',
      actions: [
        {
          type: 'case.assign',
          timing: 'OnEntry',
          parameterSchemaKey: 'case.assign',
          params: { assigneeType: 'role', assigneeValue: 'reviewer', overwriteExisting: false },
          summary: 'Assign the case to a reviewer.',
        },
        {
          type: 'forms.request-evidence',
          timing: 'OnEntry',
          parameterSchemaKey: 'forms.request-evidence',
          params: {
            title: 'Request supporting evidence',
            helpText: 'Capture any extra evidence the reviewer needs before deciding.',
            dueDate: '',
            fields: [
              {
                fieldKey: 'decision-note',
                label: 'Decision note',
                type: 'textarea',
                required: true,
                hintText: 'Explain why the reviewer is requesting more evidence.',
                validationPattern: '',
                defaultValue: '',
                options: [],
              },
            ],
          },
          summary: 'Request evidence form: 1 field',
        },
      ],
      components: [],
      roleGates: ['reviewer'],
    },
    {
      stateKey: 'confirmation',
      displayName: 'Application Submitted',
      description: 'Confirm the application has been submitted.',
      kind: 'Confirmation',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
  ],
  transitions: [
    { fromState: 'applicant-details', toState: 'route-check-answers', action: 'route' },
    { fromState: 'route-check-answers', toState: 'check-answers', action: 'submit', metadata: { actions: [{ type: 'forms.submit', timing: 'OnTransition', parameterSchemaKey: 'forms.form-reference', params: { formDefinitionId: 'planning-applicant-details' }, summary: 'Submit the applicant details form.' }] } },
    { fromState: 'check-answers', toState: 'route-reviewer-assessment', action: 'route' },
    { fromState: 'route-reviewer-assessment', toState: 'reviewer-assessment', action: 'submit' },
    { fromState: 'reviewer-assessment', toState: 'route-reviewer-decision', action: 'route' },
    { fromState: 'route-reviewer-decision', toState: 'confirmation', action: 'approve', requiresRole: 'reviewer' },
    { fromState: 'route-reviewer-decision', toState: 'applicant-details', action: 'reject', requiresRole: 'reviewer' },
  ],
  metadata: { schemaVersion: '1.0', gateways: [
    {
      key: 'route-check-answers',
      displayName: 'Route to check answers',
      gatewayType: 'Split',
      source: 'applicant-details',
      queueKey: 'public',
      roleGates: [],
      routes: [
        {
          id: 'applicant-details--submit--check-answers',
          target: 'check-answers',
          trigger: 'submit',
          actions: [
            {
              type: 'forms.submit',
              timing: 'OnTransition',
              parameterSchemaKey: 'forms.form-reference',
              params: { formDefinitionId: 'planning-applicant-details' },
              summary: 'Submit the applicant details form.',
            },
          ],
        },
      ],
    },
    {
      key: 'route-reviewer-assessment',
      displayName: 'Route to reviewer assessment',
      gatewayType: 'Split',
      source: 'check-answers',
      queueKey: 'public',
      roleGates: [],
      routes: [
        {
          id: 'check-answers--submit--reviewer-assessment',
          target: 'reviewer-assessment',
          trigger: 'submit',
          actions: [],
        },
      ],
    },
    {
      key: 'route-reviewer-decision',
      displayName: 'Route from reviewer assessment',
      gatewayType: 'Split',
      source: 'reviewer-assessment',
      queueKey: 'reviewer',
      roleGates: [],
      routes: [
        {
          id: 'reviewer-assessment--approve--confirmation',
          target: 'confirmation',
          trigger: 'approve',
          requiresRole: 'reviewer',
          actions: [],
        },
        {
          id: 'reviewer-assessment--reject--applicant-details',
          target: 'applicant-details',
          trigger: 'reject',
          requiresRole: 'reviewer',
          actions: [],
        },
      ],
    },
  ],
  }} as unknown as AuthoredServiceBlueprint));
