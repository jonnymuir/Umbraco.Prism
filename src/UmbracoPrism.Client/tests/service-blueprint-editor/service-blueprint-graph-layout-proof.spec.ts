import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';
// TODO Slice E: re-cert after gateway-pill rendering + simulation reshape. See .squad/decisions/inbox/copilot-slice-d-close-out.md.

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

type MeasuredLane = {
  key: string;
  left: number;
  right: number;
  top: number;
  bottom: number;
};

type MeasuredNode = {
  key: string;
  kind: 'stage' | 'gateway';
  gatewayKind: string | null;
  label: string;
  lane: string;
  left: number;
  right: number;
  top: number;
  bottom: number;
  width: number;
  height: number;
};

type MeasuredRoute = {
  key: string;
  from: string;
  to: string;
  d: string;
};

type MeasuredGraph = {
  lanes: MeasuredLane[];
  nodes: MeasuredNode[];
  routes: MeasuredRoute[];
};

async function measureGraph(page: Page): Promise<MeasuredGraph> {
  return page.locator('prism-service-blueprint-graph').evaluate(graphElement => {
    const graph = graphElement as HTMLElement;
    const shadowRoot = graph.shadowRoot;
    if (!shadowRoot) {
      throw new Error('Graph shadow root not found');
    }

    const scene = shadowRoot.querySelector<HTMLElement>('.react-flow__viewport');
    if (!scene) {
      throw new Error('Graph scene not found');
    }

    const sceneRect = scene.getBoundingClientRect();
    const lanes = Array.from(shadowRoot.querySelectorAll<HTMLElement>('[data-prism-role-queue]')).map(lane => {
      const rect = lane.getBoundingClientRect();
      return {
        key: lane.getAttribute('data-prism-role-queue') ?? '',
        left: rect.left - sceneRect.left,
        right: rect.right - sceneRect.left,
        top: rect.top - sceneRect.top,
        bottom: rect.bottom - sceneRect.top,
      };
    });

    const inferLane = (left: number, right: number) => {
      const centre = (left + right) / 2;
      return lanes.find(lane => centre >= lane.left && centre <= lane.right)?.key ?? '';
    };

    const stageNodes = Array.from(shadowRoot.querySelectorAll<HTMLElement>('[data-prism-stage]')).map(stage => {
      const rect = stage.getBoundingClientRect();
      const left = rect.left - sceneRect.left;
      const right = rect.right - sceneRect.left;
      return {
        key: stage.getAttribute('data-prism-stage') ?? '',
        kind: 'stage' as const,
        gatewayKind: null,
        label: stage.querySelector('.node-label')?.textContent?.trim() ?? '',
        lane: inferLane(left, right),
        left,
        right,
        top: rect.top - sceneRect.top,
        bottom: rect.bottom - sceneRect.top,
        width: rect.width,
        height: rect.height,
      };
    });

    const gatewayNodes = Array.from(shadowRoot.querySelectorAll<HTMLElement>('[data-prism-gateway]')).map(gateway => {
      const rect = gateway.getBoundingClientRect();
      return {
        key: gateway.getAttribute('data-prism-gateway') ?? '',
        kind: 'gateway' as const,
        gatewayKind: gateway.getAttribute('data-prism-gateway-kind'),
        label: gateway.querySelector('.node-label')?.textContent?.trim() ?? '',
        lane: gateway.getAttribute('data-prism-queue') ?? '',
        left: rect.left - sceneRect.left,
        right: rect.right - sceneRect.left,
        top: rect.top - sceneRect.top,
        bottom: rect.bottom - sceneRect.top,
        width: rect.width,
        height: rect.height,
      };
    });

    const routes = Array.from(shadowRoot.querySelectorAll<SVGPathElement>('[data-prism-route-path]')).map(path => ({
      key: path.getAttribute('data-prism-route-path') ?? '',
      from: path.getAttribute('data-prism-route-from') ?? '',
      to: path.getAttribute('data-prism-route-to') ?? '',
      d: path.getAttribute('d') ?? '',
    }));

    return {
      lanes,
      nodes: [...stageNodes, ...gatewayNodes],
      routes,
    };
  });
}

function findNode(graph: MeasuredGraph, key: string): MeasuredNode {
  const node = graph.nodes.find(candidate => candidate.key === key);
  expect(node, `Expected graph node "${key}"`).toBeDefined();
  return node!;
}

function findRoute(graph: MeasuredGraph, from: string, to: string): MeasuredRoute {
  const route = graph.routes.find(candidate => candidate.from === from && candidate.to === to);
  expect(route, `Expected route ${from} → ${to}`).toBeDefined();
  return route!;
}

function overlapArea(left: MeasuredNode, right: MeasuredNode): number {
  const overlapX = Math.max(0, Math.min(left.right, right.right) - Math.max(left.left, right.left));
  const overlapY = Math.max(0, Math.min(left.bottom, right.bottom) - Math.max(left.top, right.top));
  return overlapX * overlapY;
}

function centreY(node: MeasuredNode): number {
  return node.top + node.height / 2;
}

function centreX(node: MeasuredNode): number {
  return node.left + node.width / 2;
}

function parseRoutePoints(path: string): Array<{ x: number; y: number }> {
  const matches = path.match(/[ML]\s*(-?\d+(?:\.\d+)?)\s*(-?\d+(?:\.\d+)?)/g) ?? [];
  return matches.map(segment => {
    const [, x, y] = /[ML]\s*(-?\d+(?:\.\d+)?)\s*(-?\d+(?:\.\d+)?)/.exec(segment)!;
    return { x: Number(x), y: Number(y) };
  });
}

function assertNoNodeOverlaps(graph: MeasuredGraph, label: string): void {
  for (let index = 0; index < graph.nodes.length; index += 1) {
    for (let nextIndex = index + 1; nextIndex < graph.nodes.length; nextIndex += 1) {
      const left = graph.nodes[index];
      const right = graph.nodes[nextIndex];
      expect(
        overlapArea(left, right),
        `${label}: ${left.label} and ${right.label} should not sit on top of each other`
      ).toBe(0);
    }
  }
}

test.describe('ServiceBlueprint canvas slot-matrix layout proof', () => {
  test('renders lanes as vertical columns and keeps service flow moving top to bottom', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--gateway-representation'));

    const graphElement = page.locator('prism-service-blueprint-graph');
    await expect(graphElement).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const graph = await measureGraph(page);
    expect(graph.lanes.length).toBeGreaterThanOrEqual(2);

    for (let index = 0; index < graph.lanes.length - 1; index += 1) {
      const currentLane = graph.lanes[index];
      const nextLane = graph.lanes[index + 1];
      expect(
        currentLane.right,
        `${currentLane.key} should be a separate vertical column before ${nextLane.key}`
      ).toBeLessThan(nextLane.left);
      expect(
        currentLane.bottom - currentLane.top,
        `${currentLane.key} should read as a vertical lane column, not a short horizontal band`
      ).toBeGreaterThan(currentLane.right - currentLane.left);
    }

    const start = findNode(graph, 'start-request');
    const reviewSplit = findNode(graph, 'review-split');
    const applicantAmendments = findNode(graph, 'applicant-amendments');

    expect(reviewSplit.lane).toBe('applicant');
    expect(applicantAmendments.lane).toBe('applicant');
    expect(
      centreY(reviewSplit),
      'the split gateway should sit below the opening stage'
    ).toBeGreaterThan(start.bottom);
    expect(
      centreY(applicantAmendments),
      'service flow should continue top to bottom inside the lane column'
    ).toBeGreaterThan(reviewSplit.bottom);
    expect(
      centreX(start),
      'same-lane service flow should stay inside its lane column'
    ).toBeGreaterThanOrEqual(graph.lanes[0].left);
  });

  test.fixme('keeps same-lane routing choices in separate slots instead of stacking them together', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--same-lane-fan-out'));

    const graphElement = page.locator('prism-service-blueprint-graph');
    await expect(graphElement).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const graph = await measureGraph(page);
    const draft = findNode(graph, 'draft');
    const evidenceRoute = findNode(graph, 'evidence-route');
    const siteVisitRoute = findNode(graph, 'site-visit-route');

    expect(evidenceRoute.lane).toBe('public');
    expect(siteVisitRoute.lane).toBe('public');
    expect(centreY(evidenceRoute)).toBeGreaterThan(draft.bottom);
    expect(centreY(siteVisitRoute)).toBeGreaterThan(draft.bottom);
    expect(
      Math.abs(centreY(evidenceRoute) - centreY(siteVisitRoute)),
      'same-lane routing choices should read as sibling slots at the same next level'
    ).toBeLessThanOrEqual(24);
    expect(
      overlapArea(evidenceRoute, siteVisitRoute),
      'same-lane routing choices should not overlap each other'
    ).toBe(0);

    const evidenceRail = findRoute(graph, 'draft', 'evidence-route');
    const siteVisitRail = findRoute(graph, 'draft', 'site-visit-route');
    const evidencePoints = parseRoutePoints(evidenceRail.d);
    const siteVisitPoints = parseRoutePoints(siteVisitRail.d);
    expect(
      evidencePoints[0]?.x,
      'same-lane routing choices should leave the stage through separate slot corridors'
    ).not.toBe(siteVisitPoints[0]?.x);
  });

  test('keeps cross-lane fan-out readable as stage, gateway, branch row, join, then next stage', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--gateway-representation'));

    const graphElement = page.locator('prism-service-blueprint-graph');
    await expect(graphElement).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const graph = await measureGraph(page);
    const start = findNode(graph, 'start-request');
    const split = findNode(graph, 'review-split');
    const applicant = findNode(graph, 'applicant-amendments');
    const reviewer = findNode(graph, 'reviewer-assessment');
    const join = findNode(graph, 'decision-join');
    const decision = findNode(graph, 'decision-confirmed');

    expect(centreY(split), 'the split gateway should come after the opening stage').toBeGreaterThan(start.bottom);
    expect(
      centreY(split),
      'the split gateway should sit before the branch work starts'
    ).toBeLessThan(Math.min(applicant.top, reviewer.top));
    expect(
      Math.abs(applicant.top - reviewer.top),
      'cross-lane branch work should stay in one readable branch row'
    ).toBeLessThanOrEqual(24);
    expect(centreY(join), 'the join gateway should sit below both branch stages').toBeGreaterThan(Math.max(applicant.bottom, reviewer.bottom));
    expect(centreY(join), 'the join gateway should appear before the next downstream stage').toBeLessThan(decision.top);

    const applicantToJoin = findRoute(graph, 'applicant-amendments', 'decision-join');
    const joinToDecision = findRoute(graph, 'decision-join', 'decision-confirmed');
    const applicantJoinPoints = parseRoutePoints(applicantToJoin.d);
    const joinDecisionPoints = parseRoutePoints(joinToDecision.d);

    expect(
      applicantJoinPoints.at(-1)?.y,
      'incoming branch rails should stop at the join boundary instead of running through the join body'
    ).toBeLessThan(join.bottom);
    expect(
      joinDecisionPoints[0]?.y,
      'the downstream trunk should start at or below the join attachment'
    ).toBeGreaterThan(join.top);
  });

  test('renders payment-demo split/join flow with correct top-to-bottom Y ordering', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--payment-demo-graph'));

    const graphElement = page.locator('prism-service-blueprint-graph');
    await expect(graphElement).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const graph = await measureGraph(page);

    const enterDetails = findNode(graph, 'enter-details');
    const submitPayment = findNode(graph, 'submit-payment');
    const confirmPayment = findNode(graph, 'confirm-payment-received');
    const awaitConfirmation = findNode(graph, 'await-payment-confirmation');
    const paymentComplete = findNode(graph, 'payment-complete');

    // All five nodes must exist
    expect(enterDetails).toBeDefined();
    expect(submitPayment).toBeDefined();
    expect(confirmPayment).toBeDefined();
    expect(awaitConfirmation).toBeDefined();
    expect(paymentComplete).toBeDefined();

    // enter-details → submit-payment (split) → confirm-payment-received (branch)
    // → await-payment-confirmation (join) → payment-complete
    expect(
      centreY(submitPayment),
      'submit-payment split gateway must sit below enter-details'
    ).toBeGreaterThan(enterDetails.bottom);
    expect(
      centreY(confirmPayment),
      'confirm-payment-received branch stage must sit below the split gateway'
    ).toBeGreaterThan(submitPayment.bottom);
    expect(
      centreY(awaitConfirmation),
      'await-payment-confirmation join gateway must sit below the branch stage'
    ).toBeGreaterThan(confirmPayment.bottom);
    expect(
      centreY(paymentComplete),
      'payment-complete must sit below the join gateway'
    ).toBeGreaterThan(awaitConfirmation.bottom);

    // No overlaps allowed
    assertNoNodeOverlaps(graph, 'payment-demo canvas');
  });

  test('keeps stages and gateways from overlapping across the canvas stories', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });

    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--gateway-representation'));
    await expect(page.locator('prism-service-blueprint-graph')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });
    assertNoNodeOverlaps(await measureGraph(page), 'cross-lane canvas');

    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--same-lane-fan-out'));
    await expect(page.locator('prism-service-blueprint-graph')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });
    assertNoNodeOverlaps(await measureGraph(page), 'same-lane canvas');
  });

  test('keeps every node inside a lane column so the canvas still reads lane by lane', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--gateway-representation'));

    const graphElement = page.locator('prism-service-blueprint-graph');
    await expect(graphElement).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('prism-service-blueprint-graph[data-prism-graph-ready="true"]')).toBeAttached({ timeout: 15_000 });

    const graph = await measureGraph(page);
    for (const node of graph.nodes) {
      const lane = graph.lanes.find(candidate => candidate.key === node.lane);
      expect(lane, `Expected a lane for ${node.label}`).toBeDefined();
      expect(
        node.left,
        `${node.label} should stay inside its lane column`
      ).toBeGreaterThanOrEqual((lane?.left ?? 0) - 2);
      expect(
        node.right,
        `${node.label} should stay inside its lane column`
      ).toBeLessThanOrEqual((lane?.right ?? 0) + 2);
    }
  });
});
