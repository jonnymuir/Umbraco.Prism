import type { Locator, Page } from '@playwright/test';
import { expect } from '@playwright/test';

/**
 * Shared helpers for the service-blueprint-editor visual regression suite.
 *
 * Documented in `docs/testing/workflow-editor-visual-tests.md`. Every
 * spec in `service-blueprint-canvas-*.spec.ts` and `service-blueprint-editor-ergonomics.spec.ts`
 * leans on the data-attribute contract listed there — keep this file in sync
 * if the contract changes.
 */

/** Pinned viewport for the entire visual suite. */
export const VISUAL_VIEWPORT = { width: 1440, height: 900 } as const;

/** Canonical scenarios exposed as Storybook stories. */
export type CanonicalScenario = {
  /** Stable identifier used in test names. */
  readonly id: string;
  /** Storybook story id (matches the `iframe.html?id=` URL parameter). */
  readonly storyId: string;
  /** Whether the scenario contains any gateways. */
  readonly hasGateways: boolean;
  /** Whether the scenario is intentionally larger than the viewport. */
  readonly oversize: boolean;
};

export const CANONICAL_SCENARIOS: readonly CanonicalScenario[] = [
  {
    id: 'SINGLE_LANE_LINEAR',
    storyId: 'service-blueprint-editor-service-blueprint-graph--workspace-canvas',
    hasGateways: false,
    oversize: false,
  },
  {
    id: 'MULTI_LANE_FAN_OUT',
    storyId: 'service-blueprint-editor-service-blueprint-graph--gateway-representation',
    hasGateways: true,
    oversize: false,
  },
  {
    id: 'SAME_LANE_FAN_OUT',
    storyId: 'service-blueprint-editor-service-blueprint-graph--same-lane-fan-out',
    hasGateways: true,
    oversize: false,
  },
  {
    id: 'LARGE_SERVICE_BLUEPRINT',
    storyId: 'service-blueprint-editor-service-blueprint-graph--large-service-blueprint',
    hasGateways: false,
    oversize: true,
  },
] as const;

export function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

/** Locator for the canvas custom element. */
export function graphLocator(page: Page): Locator {
  return page.locator('prism-service-blueprint-graph');
}

/** Navigate to a story and wait until the graph has measurable layout. */
export async function gotoCanonicalScenario(
  page: Page,
  scenario: CanonicalScenario,
): Promise<void> {
  await page.setViewportSize({ ...VISUAL_VIEWPORT });
  await page.goto(storyUrl(scenario.storyId));
  const graph = graphLocator(page);
  await expect(graph).toBeVisible({ timeout: 10_000 });

  // The React Flow canvas loads lazily and sets data-prism-graph-ready on the
  // host once nodes and edges are committed to the DOM. Empty service
  // blueprints render the Lit empty state instead and never mount the canvas.
  await expect(
    page.locator(
      'prism-service-blueprint-graph[data-prism-graph-ready="true"], prism-service-blueprint-graph [data-prism-empty-state]',
    ).first(),
  ).toBeAttached({ timeout: 15_000 });
}

export type LaneBox = {
  key: string;
  left: number;
  right: number;
  top: number;
  bottom: number;
  width: number;
  height: number;
};

export type NodeBox = {
  kind: 'stage' | 'gateway';
  key: string;
  laneAttr: string | null;
  laneByCentre: string | null;
  label: string;
  scrollWidth: number;
  clientWidth: number;
  left: number;
  right: number;
  top: number;
  bottom: number;
  width: number;
  height: number;
};

export type RouteEndpoints = {
  key: string;
  from: string;
  to: string;
  start: { x: number; y: number };
  end: { x: number; y: number };
};

export type GraphGeometry = {
  scene: { left: number; top: number; width: number; height: number };
  lanes: LaneBox[];
  nodes: NodeBox[];
  routes: RouteEndpoints[];
};

/**
 * Measure the rendered canvas: lanes, nodes (stages + gateways), and SVG
 * route endpoint coordinates. All coordinates are relative to the React Flow
 * viewport (the transformed scene container) so they are stable across
 * panning; the visual suite runs at the default zoom of 1, where viewport
 * coordinates equal flow coordinates.
 */
export async function measureGraph(page: Page): Promise<GraphGeometry> {
  return graphLocator(page).evaluate((graphElement) => {
    const root = (graphElement as HTMLElement).shadowRoot;
    if (!root) throw new Error('Graph shadow root not found');
    const scene = root.querySelector<HTMLElement>('.react-flow__viewport');
    if (!scene) throw new Error('.react-flow__viewport not found');
    const sceneRect = scene.getBoundingClientRect();
    const rel = (rect: DOMRect) => ({
      left: rect.left - sceneRect.left,
      right: rect.right - sceneRect.left,
      top: rect.top - sceneRect.top,
      bottom: rect.bottom - sceneRect.top,
      width: rect.width,
      height: rect.height,
    });

    const lanes = Array.from(
      root.querySelectorAll<HTMLElement>('[data-prism-queue-container]'),
    ).map((lane) => ({
      key: lane.getAttribute('data-prism-queue-container') ?? '',
      ...rel(lane.getBoundingClientRect()),
    }));

    const inferLane = (left: number, right: number) => {
      const centre = (left + right) / 2;
      return (
        lanes.find((lane) => centre >= lane.left && centre <= lane.right)?.key ??
        null
      );
    };

    const measureNode = (
      shell: HTMLElement,
      kind: 'stage' | 'gateway',
      shellAttr: string,
      buttonSelector: string,
    ) => {
      const r = rel(shell.getBoundingClientRect());
      const button = shell.querySelector<HTMLElement>(buttonSelector);
      const labelEl = button?.querySelector<HTMLElement>('.node-label');
      const label = labelEl?.textContent?.trim() ?? '';
      return {
        kind,
        key: shell.getAttribute(shellAttr) ?? '',
        laneAttr: button?.getAttribute('data-prism-queue') ?? null,
        laneByCentre: inferLane(r.left, r.right),
        label,
        scrollWidth: labelEl?.scrollWidth ?? 0,
        clientWidth: labelEl?.clientWidth ?? 0,
        ...r,
      };
    };

    const stages = Array.from(
      root.querySelectorAll<HTMLElement>('[data-prism-stage-card]'),
    ).map((shell) =>
      measureNode(shell, 'stage', 'data-prism-stage-card', '[data-prism-stage]'),
    );

    const gateways = Array.from(
      root.querySelectorAll<HTMLElement>('[data-prism-gateway-node]'),
    ).map((shell) =>
      measureNode(
        shell,
        'gateway',
        'data-prism-gateway-node',
        '[data-prism-gateway]',
      ),
    );

    const routes = Array.from(
      root.querySelectorAll<SVGPathElement>('[data-prism-route-path]'),
    )
      .map((path) => {
        const length = (path as SVGPathElement).getTotalLength?.() ?? 0;
        if (!length) return null;
        const start = path.getPointAtLength(0);
        const end = path.getPointAtLength(length);
        const svg = path.ownerSVGElement;
        const svgRect = svg?.getBoundingClientRect();
        const offsetX = (svgRect?.left ?? 0) - sceneRect.left;
        const offsetY = (svgRect?.top ?? 0) - sceneRect.top;
        return {
          key: path.getAttribute('data-prism-route-path') ?? '',
          from: path.getAttribute('data-prism-route-from') ?? '',
          to: path.getAttribute('data-prism-route-to') ?? '',
          start: { x: start.x + offsetX, y: start.y + offsetY },
          end: { x: end.x + offsetX, y: end.y + offsetY },
        };
      })
      .filter((route): route is RouteEndpoints => route !== null);

    return {
      scene: {
        left: 0,
        top: 0,
        width: sceneRect.width,
        height: sceneRect.height,
      },
      lanes,
      nodes: [...stages, ...gateways],
      routes,
    };
  });
}

/** Centre point of a node, in scene coordinates. */
export function nodeCentre(node: NodeBox): { x: number; y: number } {
  return {
    x: (node.left + node.right) / 2,
    y: (node.top + node.bottom) / 2,
  };
}

/** Rectangle overlap, allowing for a small numerical tolerance. */
export function rectanglesOverlap(
  a: { left: number; right: number; top: number; bottom: number },
  b: { left: number; right: number; top: number; bottom: number },
  tolerance = 1,
): boolean {
  return (
    a.left + tolerance < b.right &&
    b.left + tolerance < a.right &&
    a.top + tolerance < b.bottom &&
    b.top + tolerance < a.bottom
  );
}
