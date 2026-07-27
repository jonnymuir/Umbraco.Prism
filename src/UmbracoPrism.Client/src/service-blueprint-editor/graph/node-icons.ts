import type { AuthoredGateway, AuthoredStage, GatewayKind, StageKind } from '../types.js';

export type NodeIconDef = { viewBox: string; paths: string[] };

/**
 * Curated icon set for stage/gateway cards — deliberately small and
 * hand-authored (thin-stroke, 24x24 outline style) rather than pulling in
 * an external icon library. The editor is runtime-only (see README) and
 * must not depend on the app's `uui-icon`/`uui-icon-registry-essential`,
 * which is backoffice-only tooling.
 */
export const NODE_ICONS: Record<string, NodeIconDef> = {
  form: {
    viewBox: '0 0 24 24',
    paths: [
      'M6 3.5h9l3 3V20a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V4.5a1 1 0 0 1 1-1Z',
      'M14.5 3.5V7h3.5',
      'M8 12h8',
      'M8 15.5h8',
      'M8 9h4',
    ],
  },
  checklist: {
    viewBox: '0 0 24 24',
    paths: [
      'M5 4.5h14a1 1 0 0 1 1 1V19a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5.5a1 1 0 0 1 1-1Z',
      'M7.5 8.5l1.5 1.5 2.5-2.5',
      'M13.5 8.75h4',
      'M7.5 14.5l1.5 1.5 2.5-2.5',
      'M13.5 14.75h4',
    ],
  },
  flagCheck: {
    viewBox: '0 0 24 24',
    paths: [
      'M6 3v18',
      'M6 4h11l-2.5 3.5L17 11H6',
    ],
  },
  list: {
    viewBox: '0 0 24 24',
    paths: [
      'M8.5 6h10',
      'M8.5 12h10',
      'M8.5 18h10',
      'M5 6h.01',
      'M5 12h.01',
      'M5 18h.01',
    ],
  },
  split: {
    viewBox: '0 0 24 24',
    paths: [
      'M6 4v6',
      'M6 10c0 3 2 3.5 4.5 3.5H15',
      'M6 10c0 3-2 3.5-4.5 3.5',
      'M13.5 11 17 13.5l-3.5 2.5v-5Z',
    ],
  },
  join: {
    viewBox: '0 0 24 24',
    paths: [
      'M18 4v6',
      'M18 10c0 3-2 3.5-4.5 3.5H9',
      'M18 10c0-3 2-3.5 4.5-3.5',
      'M10.5 11 7 13.5l3.5 2.5v-5Z',
    ],
  },
  mail: {
    viewBox: '0 0 24 24',
    paths: [
      'M4.5 5.5h15a1 1 0 0 1 1 1V17a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1V6.5a1 1 0 0 1 1-1Z',
      'M4 6.5l8 6.5 8-6.5',
    ],
  },
  flag: {
    viewBox: '0 0 24 24',
    paths: [
      'M6 3v18',
      'M6 4.5h12l-3 4 3 4H6',
    ],
  },
};

export type NodeIconName = keyof typeof NODE_ICONS;

const STAGE_KIND_ICON: Record<StageKind, NodeIconName> = {
  Question: 'form',
  CheckAnswers: 'checklist',
  Confirmation: 'flagCheck',
  TaskList: 'list',
};

const GATEWAY_KIND_ICON: Record<GatewayKind, NodeIconName> = {
  Split: 'split',
  Join: 'join',
};

export function defaultIconForStage(stage: Pick<AuthoredStage, 'kind'>): NodeIconName {
  return (stage.kind && STAGE_KIND_ICON[stage.kind]) || 'form';
}

export function defaultIconForGateway(gateway: Pick<AuthoredGateway, 'gatewayType' | 'kind'>): NodeIconName {
  const kind = gateway.gatewayType ?? gateway.kind;
  return (kind && GATEWAY_KIND_ICON[kind]) || 'split';
}

export function iconForStage(stage: Pick<AuthoredStage, 'icon' | 'kind'>): NodeIconDef {
  return NODE_ICONS[stage.icon ?? ''] ?? NODE_ICONS[defaultIconForStage(stage)];
}

export function iconForGateway(gateway: Pick<AuthoredGateway, 'icon' | 'gatewayType' | 'kind'>): NodeIconDef {
  return NODE_ICONS[gateway.icon ?? ''] ?? NODE_ICONS[defaultIconForGateway(gateway)];
}
