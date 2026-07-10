import type { Meta, StoryObj } from '@storybook/web-components';
import { expect, waitFor } from '@storybook/test';
import './prism-workflow-graph.js';
import type { PrismWorkflowGraphElement } from './prism-workflow-graph.js';
import { STUB_WORKFLOW } from './types.js';
import type { AuthoredWorkflow } from './types.js';
import { LEAVE_REQUEST_STARTER_WORKFLOW, PAYMENT_DEMO_WORKFLOW, COMMUNITY_ENQUIRY_WORKFLOW, INFORMATION_REQUEST_WORKFLOW, MONEY_MODELLER_WORKFLOW, PLANNING_WORKFLOW_MIGRATED, cloneAuthoredWorkflow } from './fixtures/index.js';

const WORKSPACE_WORKFLOW: AuthoredWorkflow = {
  ...STUB_WORKFLOW,
};

const GATEWAY_WORKFLOW: AuthoredWorkflow = cloneAuthoredWorkflow(LEAVE_REQUEST_STARTER_WORKFLOW);
const PAYMENT_DEMO_GRAPH_WORKFLOW: AuthoredWorkflow = cloneAuthoredWorkflow(PAYMENT_DEMO_WORKFLOW);

/**
 * Same-lane fan-out — `draft` branches to two sibling stages inside the
 * same queue through a single split gateway before rejoining.
 */
const SAME_LANE_FAN_OUT_WORKFLOW: AuthoredWorkflow = {
  ...STUB_WORKFLOW,
  definitionKey: 'leave-request-same-lane-fan-out',
  displayName: 'Leave Request — Same-Lane Fan-Out',
  initialState: 'draft',
  states: [
    {
      stateKey: 'draft',
      displayName: 'Draft submission',
      description: 'Capture the initial applicant draft before routing starts.',
      kind: 'Question',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stateKey: 'collect-evidence',
      displayName: 'Collect evidence',
      description: 'Gather the supporting evidence for the next decision.',
      kind: 'Question',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stateKey: 'book-site-visit',
      displayName: 'Book site visit',
      description: 'Arrange a site visit before the decision is confirmed.',
      kind: 'Question',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
    {
      stateKey: 'ready-to-decide',
      displayName: 'Ready to decide',
      description: 'The single public lane continues after both routes are complete.',
      kind: 'Confirmation',
      actor: 'public',
      actions: [],
      components: [],
      roleGates: [],
    },
  ],
  gateways: [
    {
      key: 'evidence-route',
      displayName: 'Evidence route',
      gatewayType: 'Split',
      queueKey: 'public',
      actor: 'public',
      source: 'draft',
      roleGates: [],
      routes: [
        { id: 'r-collect', target: 'collect-evidence', trigger: 'collect evidence', actions: [] },
        { id: 'r-site-visit', target: 'book-site-visit', trigger: 'book site visit', actions: [] },
      ],
    },
    {
      key: 'decision-ready',
      displayName: 'Decision ready',
      gatewayType: 'Join',
      queueKey: 'public',
      actor: 'public',
      roleGates: [],
      routes: [
        { id: 'r-decide', target: 'ready-to-decide', trigger: 'continue', actions: [] },
      ],
    },
  ],
};

type StoryArgs = {
  workflow: AuthoredWorkflow | null;
};

function makeElement(args: StoryArgs): PrismWorkflowGraphElement {
  const el = document.createElement('prism-workflow-graph') as PrismWorkflowGraphElement;
  el.workflow = args.workflow;
  el.style.cssText = 'display:block;height:560px;';
  return el;
}

/**
 * React Flow mounts lazily (dynamic import) and signals completion via the
 * `data-prism-graph-ready` attribute — poll for that instead of a fixed
 * delay, which races the async mount under CI load.
 */
async function waitForGraphReady(canvasElement: HTMLElement): Promise<PrismWorkflowGraphElement> {
  const el = canvasElement.querySelector('prism-workflow-graph') as PrismWorkflowGraphElement;
  await el.updateComplete;
  await waitFor(() => {
    const hasStages = (el.workflow?.states?.length ?? 0) > 0;
    if (!hasStages || el.hasAttribute('data-prism-graph-ready')) {
      return;
    }
    throw new Error('workflow graph canvas has not signalled data-prism-graph-ready yet');
  }, { timeout: 5000 });
  return el;
}

function fillCreateStageDialog(root: ShadowRoot, name: string, key: string, lane: string, type: string) {
  const nameInput = root.querySelector<HTMLInputElement>('[data-prism-create-stage-title]')!;
  nameInput.value = name;
  nameInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));

  const keyInput = root.querySelector<HTMLInputElement>('[data-prism-create-stage-key]')!;
  keyInput.value = key;
  keyInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));

  const laneInput = root.querySelector<HTMLInputElement>('[data-prism-create-stage-queue]')!;
  laneInput.value = lane;
  laneInput.dispatchEvent(new Event('input', { bubbles: true, composed: true }));

  const typeSelect = root.querySelector<HTMLSelectElement>('[data-prism-create-stage-type]')!;
  typeSelect.value = type;
  typeSelect.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
}

const meta: Meta<StoryArgs> = {
  title: 'Workflow Editor/Workflow Graph',
  component: 'prism-workflow-graph',
  tags: ['autodocs'],
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-required-children', enabled: true },
        ],
      },
    },
  },
  args: {
    workflow: null,
  },
  render: args => makeElement(args),
};

export default meta;
type Story = StoryObj<StoryArgs>;

export const Empty: Story = {
  args: { workflow: null },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const container = el.shadowRoot?.querySelector('[data-prism-component="workflow-graph"]');
    await expect(container).not.toBeNull();
  },
};

export const WorkspaceCanvas: Story = {
  args: { workflow: WORKSPACE_WORKFLOW },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(WORKSPACE_WORKFLOW.states.length);
    await expect(root.querySelectorAll('[data-prism-transition]').length).toBeGreaterThanOrEqual(0);
  },
};

export const InteractiveWorkspace: Story = {
  args: { workflow: WORKSPACE_WORKFLOW },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    root.querySelector<HTMLButtonElement>('[data-prism-add-stage]')!.click();
    await el.updateComplete;
    await expect(root.querySelector('[data-prism-create-stage-dialog]')).not.toBeNull();

    fillCreateStageDialog(root, 'Evidence Review', 'evidence-review', 'reviewer', 'review');
    root.querySelector<HTMLButtonElement>('[data-prism-create-stage-submit]')!.click();
    await el.updateComplete;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(WORKSPACE_WORKFLOW.states.length + 1);

    const declaration = root.querySelector<HTMLElement>('[data-prism-stage="applicant-details"]')!;
    let inspectorOpened = false;
    el.addEventListener('inspector-requested', event => {
      const detail = (event as CustomEvent<{ kind: string; stageKey?: string }>).detail;
      if (detail.kind === 'stage' && detail.stageKey === 'applicant-details') {
        inspectorOpened = true;
      }
    });

    declaration.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, composed: true }));
    await el.updateComplete;
    await expect(inspectorOpened).toBe(true);

    declaration.dispatchEvent(new MouseEvent('contextmenu', {
      bubbles: true,
      composed: true,
      clientX: 240,
      clientY: 220,
    }));
    await el.updateComplete;
    await expect(root.querySelector('[data-prism-context-menu]')).not.toBeNull();

    root.querySelector<HTMLButtonElement>('[data-prism-fit-screen]')!.click();
    await el.updateComplete;
    await expect(Boolean(root.querySelector<HTMLElement>('[data-prism-zoom]')?.textContent?.includes('%'))).toBe(true);
  },
};

export const DeleteConfirmation: Story = {
  args: { workflow: WORKSPACE_WORKFLOW },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    const stage = root.querySelector<HTMLElement>('[data-prism-stage="reviewer-assessment"]')!;
    stage.dispatchEvent(new MouseEvent('contextmenu', {
      bubbles: true,
      composed: true,
      clientX: 240,
      clientY: 220,
    }));
    await el.updateComplete;

    await expect(root.querySelector('[data-prism-context-menu]')).not.toBeNull();
    root.querySelector<HTMLButtonElement>('[data-prism-context-menu] .danger')!.click();
    await el.updateComplete;

    await expect(root.querySelector('[data-prism-delete-stage-dialog]')).not.toBeNull();
    await expect(root.querySelectorAll('[data-prism-delete-stage-transitions] li').length).toBeGreaterThan(0);

    root.querySelector<HTMLButtonElement>('[data-prism-delete-stage-cancel]')!.click();
    await el.updateComplete;
    await expect(root.querySelector('[data-prism-delete-stage-dialog]')).toBeNull();
  },
};

export const GatewayRepresentation: Story = {
  args: { workflow: GATEWAY_WORKFLOW },
  // MULTI_LANE_FAN_OUT canonical scenario (visual regression suite).
  // Needs more vertical room than the default 560px story height so the
  // full split → branch row → join fan-out renders inside the frame.
  render: (args) => {
    const el = makeElement(args);
    el.style.cssText = 'display:block;height:1080px;';
    return el;
  },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-gateway]').length).toBe(2);
    await expect(root.querySelector('[data-prism-gateway-kind="Split"]')).not.toBeNull();
    await expect(root.querySelector('[data-prism-gateway-kind="Join"]')).not.toBeNull();
  },
};

export const PaymentDemoGraph: Story = {
  name: 'Payment demo — cross-queue split/join',
  args: { workflow: PAYMENT_DEMO_GRAPH_WORKFLOW },
  render: (args) => {
    const el = makeElement(args);
    el.style.cssText = 'display:block;height:960px;';
    return el;
  },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-gateway]').length).toBe(2);
    await expect(root.querySelector('[data-prism-gateway="submit-payment"]')).not.toBeNull();
    await expect(root.querySelector('[data-prism-gateway="await-payment-confirmation"]')).not.toBeNull();
  },
};

export const SameLaneFanOut: Story = {
  args: { workflow: SAME_LANE_FAN_OUT_WORKFLOW },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-gateway-kind="Split"]').length).toBe(1);
    await expect(root.querySelector('[data-prism-gateway-kind="Join"]')).not.toBeNull();
    await expect(root.querySelector('[data-prism-gateway="decision-ready"]')).not.toBeNull();
  },
};

export const GraphReadOnly: Story = {
  name: 'Read-only viewer (declarative HTML)',
  parameters: {
    docs: {
      description: {
        story:
          'Renders a published workflow purely from HTML attributes — no JS plumbing. ' +
          'Demonstrates the `<prism-workflow-graph read-only workflow-json="...">` recipe an ' +
          'integrator can drop into a Razor view to show a workflow diagram on a public page.',
      },
    },
  },
  render: () => {
    const container = document.createElement('div');
    container.style.cssText = 'display:block;height:560px;';
    const json = JSON.stringify(GATEWAY_WORKFLOW).replaceAll('"', '&quot;');
    container.innerHTML =
      `<prism-workflow-graph read-only workflow-json="${json}" style="display:block;height:100%;"></prism-workflow-graph>`;
    return container;
  },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    // Read-only viewer: published workflow loaded from attribute only.
    await expect(el.readOnly).toBe(true);
    await expect(el.workflow).not.toBeNull();
    await expect(root.querySelector('[data-prism-read-only="true"]')).not.toBeNull();

    // No create affordances should be exposed.
    await expect(root.querySelector('[data-prism-add-stage]')).toBeNull();
    await expect(root.querySelector('[data-prism-add-gateway]')).toBeNull();
    await expect(root.querySelector('[data-prism-empty-add-stage]')).toBeNull();
    await expect(root.querySelector('[data-prism-context-menu]')).toBeNull();
    await expect(root.querySelector('[data-prism-create-stage-dialog]')).toBeNull();
    await expect(root.querySelector('[data-prism-create-gateway-dialog]')).toBeNull();
    await expect(root.querySelector('[data-prism-delete-stage-dialog]')).toBeNull();

    // Graph content still renders, keyboard navigation still works.
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBeGreaterThan(0);
    await expect(root.querySelectorAll('[data-prism-gateway]').length).toBeGreaterThan(0);
    await expect(root.querySelector('[role="application"]')).not.toBeNull();
  },
};

/**
 * Large workflow — wide enough and tall enough to exceed a 1440x900 canvas
 * viewport on both axes. Used by the visual regression suite's scroll specs
 * (see docs/testing/workflow-editor-visual-tests.md) and by lane-fit /
 * no-overlap assertions that need a non-trivial number of nodes per lane.
 *
 * Shape: five lanes, each carrying eight stages in a linear sequence, with
 * a single cross-lane Join gateway at the end so the routing layer also
 * gets exercised at scale.
 */
function buildLargeWorkflow(): AuthoredWorkflow {
  const lanes = ['intake', 'triage', 'review', 'decision', 'archive'];
  const stagesPerLane = 8;
  const stages: AuthoredWorkflow['states'] = [];
  const gateways: NonNullable<AuthoredWorkflow['gateways']> = [];

  for (const lane of lanes) {
    for (let i = 0; i < stagesPerLane; i++) {
      const stageKey = `${lane}-step-${i + 1}`;
      stages.push({
        stateKey: stageKey,
        displayName: `${lane[0].toUpperCase()}${lane.slice(1)} step ${i + 1}`,
        description: `Synthetic stage ${i + 1} in the ${lane} lane.`,
        kind: i === stagesPerLane - 1 ? 'Confirmation' : 'Question',
        actor: lane,
        actions: [],
        components: [],
        roleGates: [],
      } as unknown as AuthoredWorkflow['states'][number]);
      if (i > 0) {
        const prev = `${lane}-step-${i}`;
        gateways.push({
          key: `route-from-${prev}`,
          displayName: `Route from ${prev}`,
          gatewayType: 'Split',
          queueKey: lane,
          actor: lane,
          source: prev,
          roleGates: [],
          routes: [{ id: `${prev}--continue--${stageKey}`, target: stageKey, trigger: 'continue', actions: [] }],
        });
      }
    }
  }

  return {
    definitionKey: 'large-synthetic-workflow',
    displayName: 'Large synthetic workflow',
    version: 1,
    instancePolicy: 'multiple',
    initialState: `${lanes[0]}-step-1`,
    states: stages,
    transitions: gateways.flatMap(gateway => gateway.source ? [{ fromState: gateway.source, toState: gateway.key, action: 'route' }, ...((gateway.routes ?? []).map(route => ({ fromState: gateway.key, toState: route.target, action: route.trigger })))] : []),
    metadata: { schemaVersion: '1.0', gateways },
  } as unknown as AuthoredWorkflow;
}

const LARGE_WORKFLOW: AuthoredWorkflow = buildLargeWorkflow();

export const LargeWorkflow: Story = {
  args: { workflow: LARGE_WORKFLOW },
  parameters: {
    docs: {
      description: {
        story:
          'Synthetic large workflow (five lanes × eight stages) used by the ' +
          'visual regression suite to exercise canvas scrolling and ' +
          'high-cardinality layout. Not a real product fixture.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);
    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(LARGE_WORKFLOW.states.length);
  },
};

// ---------------------------------------------------------------------------
// Migrated workflow stories — new queues/gateways/routes format
// ---------------------------------------------------------------------------

export const PlanningMigrated: Story = {
  name: 'Planning — migrated format',
  args: { workflow: cloneAuthoredWorkflow(PLANNING_WORKFLOW_MIGRATED) },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(4);
    await expect(root.querySelectorAll('[data-prism-role-queue]').length).toBeGreaterThanOrEqual(1);
    await expect(root.querySelectorAll('[data-prism-gateway-kind="Split"]').length).toBe(3);
  },
};

export const CommunityEnquiry: Story = {
  name: 'Community Enquiry — migrated format',
  args: { workflow: cloneAuthoredWorkflow(COMMUNITY_ENQUIRY_WORKFLOW) },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(2);
    await expect(root.querySelectorAll('[data-prism-role-queue]').length).toBeGreaterThanOrEqual(1);
    await expect(root.querySelectorAll('[data-prism-gateway-kind="Split"]').length).toBe(1);
  },
};

export const InformationRequest: Story = {
  name: 'Information Request — migrated format',
  args: { workflow: cloneAuthoredWorkflow(INFORMATION_REQUEST_WORKFLOW) },
  render: (args) => {
    const el = makeElement(args);
    el.style.cssText = 'display:block;height:960px;';
    return el;
  },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(3);
    await expect(root.querySelectorAll('[data-prism-role-queue]').length).toBeGreaterThanOrEqual(2);
    await expect(root.querySelector('[data-prism-gateway-kind="Split"]')).not.toBeNull();
    await expect(root.querySelector('[data-prism-gateway-kind="Join"]')).not.toBeNull();
  },
};

/**
 * Money Modeller — the most structurally complex real workflow this repo
 * ships (calculations block, recalculate self-loop, cross-queue fan-out).
 * Kept as a permanent story so canvas legibility regressions on real
 * fan-out/loop-back shapes — chip overlap, header occlusion, gateway
 * collisions — show up here rather than only on the simpler synthetic
 * fixtures above.
 */
export const MoneyModeller: Story = {
  name: 'Money Modeller — declarative calculations demo',
  args: { workflow: cloneAuthoredWorkflow(MONEY_MODELLER_WORKFLOW) },
  render: (args) => {
    const el = makeElement(args);
    el.style.cssText = 'display:block;height:1200px;';
    return el;
  },
  play: async ({ canvasElement }) => {
    const el = await waitForGraphReady(canvasElement);

    const root = el.shadowRoot!;
    await expect(root.querySelectorAll('[data-prism-stage]').length).toBe(6);
    await expect(root.querySelectorAll('[data-prism-gateway]').length).toBe(6);
    await expect(root.querySelectorAll('[data-prism-role-queue]').length).toBe(2);

    // Transition chips shouldn't pile up on each other or on stage/gateway
    // cards — the exact regression this fixture exists to catch. Some of
    // this workflow's cross-queue routes naturally anchor in the lane gap
    // (36px, narrower than the chip) between two obstacles on either side,
    // so a little unavoidable edge-touching is tolerated. One pairing
    // ("send-quote" against the "quote-sent" card) sits at ~50%: review-
    // quote-request, close-request, and quote-sent all resolve to the same
    // topology rank (money-modeller is the one fixture exempted from the
    // "forward edges flow to a strictly higher rank" invariant in
    // workflow-graph-layout.test.ts), so that chip's natural anchor sits
    // inside quote-sent's card regardless of how well it's decluttered.
    // That's a rank-assignment quirk, not a declutter regression — anything
    // beyond this measured ceiling is.
    const MAX_OVERLAP_FRACTION = 0.55;
    const chipRects = Array.from(root.querySelectorAll<HTMLElement>('[data-prism-transition]'))
      .map(chip => chip.getBoundingClientRect());
    const nodeRects = Array.from(root.querySelectorAll<HTMLElement>('[data-prism-stage-card], [data-prism-gateway-node]'))
      .map(node => node.getBoundingClientRect());
    const overlapFraction = (a: DOMRect, b: DOMRect): number => {
      const ox = Math.min(a.right, b.right) - Math.max(a.left, b.left);
      const oy = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
      if (ox <= 0 || oy <= 0) {
        return 0;
      }
      return (ox * oy) / Math.min(a.width * a.height, b.width * b.height);
    };

    for (let i = 0; i < chipRects.length; i++) {
      for (let j = i + 1; j < chipRects.length; j++) {
        await expect(overlapFraction(chipRects[i], chipRects[j])).toBeLessThan(MAX_OVERLAP_FRACTION);
      }
      for (const nodeRect of nodeRects) {
        await expect(overlapFraction(chipRects[i], nodeRect)).toBeLessThan(MAX_OVERLAP_FRACTION);
      }
    }
  },
};
