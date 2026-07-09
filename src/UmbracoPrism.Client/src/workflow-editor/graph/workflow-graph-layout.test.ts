import { hydrateWorkflowDefinition, type AuthoredWorkflow } from '../types.js';
import { PLANNING_WORKFLOW, cloneAuthoredWorkflow } from '../fixtures/index.js';
import {
  GATEWAY_PILL_HEIGHT,
  GATEWAY_SIZE,
  LANE_HEADER_OFFSET,
  TOP_PADDING,
  computeWorkflowGraphLayout,
  laneForPosition,
  parseGraphNodeId,
  rowBandCenter,
  stageNodeId,
} from './workflow-graph-layout.js';

type RawWorkflow = Record<string, unknown>;

export type LayoutTestFixtures = {
  paymentDemo: RawWorkflow;
  moneyModeller: RawWorkflow;
};

// Approve/reject review loop: the Join's reject route targets the initial
// stage again, closing a cycle the layout must break with a backward edge.
// No current seed ships a Join loop-back, so the shape is authored inline.
const REVIEW_LOOP_WORKFLOW: RawWorkflow = {
  definitionKey: 'review-loop',
  displayName: 'Review Loop',
  version: 1,
  initialState: 'draft',
  instancePolicy: 'single',
  queues: [
    { key: 'web-user', displayName: 'Applicant' },
    { key: 'admin', displayName: 'Reviewer' },
  ],
  states: [
    {
      stateKey: 'draft',
      displayName: 'Draft',
      queueKey: 'web-user',
      routes: [{ id: 'draft--submit--submit-gw', target: 'submit-gw', trigger: 'submit' }],
    },
    {
      stateKey: 'review',
      displayName: 'Review',
      queueKey: 'admin',
      routes: [{ id: 'review--decide--decision-gw', target: 'decision-gw', trigger: 'decide' }],
    },
    { stateKey: 'done', displayName: 'Done', queueKey: 'web-user', routes: [] },
  ],
  gateways: [
    {
      key: 'submit-gw',
      displayName: 'Submit',
      gatewayType: 'Split',
      queueKey: 'web-user',
      routes: [{ id: 'submit-gw--submit--review', target: 'review', trigger: 'submit' }],
    },
    {
      key: 'decision-gw',
      displayName: 'Decision',
      gatewayType: 'Join',
      queueKey: 'admin',
      routes: [
        { id: 'decision-gw--approve--done', target: 'done', trigger: 'approve' },
        { id: 'decision-gw--reject--draft', target: 'draft', trigger: 'reject' },
      ],
    },
  ],
};

let failures = 0;

function check(name: string, condition: boolean, detail?: string) {
  if (condition) {
    console.log(`  ok  ${name}`);
  } else {
    failures += 1;
    console.error(`FAIL  ${name}${detail ? ` — ${detail}` : ''}`);
  }
}

function hydrate(raw: RawWorkflow): AuthoredWorkflow {
  return hydrateWorkflowDefinition(JSON.parse(JSON.stringify(raw)) as AuthoredWorkflow);
}

function assertCommonInvariants(name: string, workflow: AuthoredWorkflow, options: { strictRanks: boolean }) {
  // Queue labels resolve from the workflow's own queues; the standalone
  // availableQueues list is exercised by the host component, not here.
  const { topology, layout } = computeWorkflowGraphLayout(workflow, []);

  check(`${name}: every topology node gets a placement`,
    topology.nodes.every(node => layout.placements.has(node.id)),
    `${layout.placements.size}/${topology.nodes.length} placed`);

  check(`${name}: all placements are finite`,
    [...layout.placements.values()].every(p =>
      Number.isFinite(p.x) && Number.isFinite(p.y) && p.width > 0 && p.height > 0));

  check(`${name}: every node sits inside its lane band`,
    [...layout.placements.values()].every(p => {
      const lane = layout.lanes.find(candidate => candidate.key === p.queueKey);
      return lane !== undefined && p.x >= lane.x && p.x + p.width <= lane.x + lane.width;
    }));

  const laneXs = layout.lanes.map(lane => lane.x);
  check(`${name}: lanes are packed left-to-right without overlap`,
    laneXs.every((x, index) => index === 0
      || x >= layout.lanes[index - 1].x + layout.lanes[index - 1].width));

  const byQueueRank = new Map<string, { x: number; width: number }[]>();
  layout.placements.forEach(p => {
    const key = `${p.queueKey}#${p.rowRank}`;
    byQueueRank.set(key, [...(byQueueRank.get(key) ?? []), { x: p.x, width: p.width }]);
  });
  check(`${name}: same-band siblings never overlap horizontally`,
    [...byQueueRank.values()].every(items => {
      const sorted = [...items].sort((left, right) => left.x - right.x);
      return sorted.every((item, index) => index === 0
        || item.x >= sorted[index - 1].x + sorted[index - 1].width);
    }));

  check(`${name}: node Y follows its row band centre`,
    [...layout.placements.values()].every(p =>
      Math.abs((p.y + p.height / 2) - rowBandCenter(p.rowRank)) < 0.001));

  if (options.strictRanks) {
    check(`${name}: forward edges always flow to a strictly higher rank`,
      topology.edges.filter(edge => !edge.backward).every(edge => {
        const fromRank = topology.ranks.get(edge.fromId) ?? 0;
        const toRank = topology.ranks.get(edge.toId) ?? 0;
        return toRank > fromRank;
      }));
  }

  check(`${name}: bounds contain every placement`,
    [...layout.placements.values()].every(p =>
      p.x >= 0 && p.y >= 0
      && p.x + p.width <= layout.bounds.width
      && p.y + p.height <= layout.bounds.height));

  return { topology, layout };
}

export function run(fixtures: LayoutTestFixtures): number {
  failures = 0;

  // Planning: single-queue linear applicant flow.
  {
    const workflow = cloneAuthoredWorkflow(PLANNING_WORKFLOW);
    const { topology, layout } = assertCommonInvariants('planning', workflow, { strictRanks: true });

    check('planning: exactly one lane', layout.lanes.length === 1,
      `lanes: ${layout.lanes.map(lane => lane.key).join(', ')}`);

    const initial = layout.placements.get(stageNodeId(workflow.initialState));
    check('planning: initial state sits on the first row band',
      initial !== undefined && initial.y === TOP_PADDING + LANE_HEADER_OFFSET
        && initial.rowRank === 0);

    check('planning: no backward edges in a linear flow',
      topology.edges.every(edge => !edge.backward));
  }

  // Payment demo: two queues, Split + Join gateways.
  {
    const workflow = hydrate(fixtures.paymentDemo);
    const { topology, layout } = assertCommonInvariants('payment-demo', workflow, { strictRanks: true });

    check('payment-demo: two lanes in first-appearance order',
      layout.lanes.length === 2 && layout.lanes[0].key === 'web-user',
      `lanes: ${layout.lanes.map(lane => lane.key).join(', ')}`);

    const gatewayNodes = topology.nodes.filter(node => node.kind === 'gateway');
    check('payment-demo: both gateways are placed and ranked between their stages',
      gatewayNodes.length === 2 && gatewayNodes.every(node => layout.placements.has(node.id)));

    check('payment-demo: gateway sizes follow the pill/diamond rule',
      gatewayNodes.every(node => {
        const placement = layout.placements.get(node.id)!;
        const routeCount = (node.gateway.routes ?? []).length;
        return node.gateway.gatewayType === 'Split' && routeCount === 1
          ? placement.height === GATEWAY_PILL_HEIGHT
          : placement.height === GATEWAY_SIZE;
      }));

    check('payment-demo: every transition binding resolves to a hosting edge',
      topology.transitionBindings.every(binding => binding.edgeKey !== null
        && topology.edges.some(edge => edge.key === binding.edgeKey)));

    const lane = laneForPosition(layout.lanes, layout.lanes[1].x + 5);
    check('payment-demo: laneForPosition finds the containing lane',
      lane !== null && lane.key === layout.lanes[1].key);
  }

  // Review loop: the Join's reject route heads back upstream.
  {
    const workflow = hydrate(REVIEW_LOOP_WORKFLOW);
    const { topology } = assertCommonInvariants('review-loop', workflow, { strictRanks: true });

    const backward = topology.edges.filter(edge => edge.backward);
    check('review-loop: the reject loop is flagged as a backward edge',
      backward.length === 1
        && backward[0].fromId === 'gateway:decision-gw'
        && backward[0].toId === 'stage:draft',
      `backward: ${backward.map(edge => edge.key).join(', ') || '(none)'}`);
    check('review-loop: backward edges point to an equal-or-lower rank',
      backward.every(edge =>
        (topology.ranks.get(edge.toId) ?? 0) <= (topology.ranks.get(edge.fromId) ?? 0)));
    check('review-loop: backward edges leave Join gateways only',
      backward.every(edge => parseGraphNodeId(edge.fromId).kind === 'gateway'));
    check('review-loop: the approve route still flows forward to done',
      topology.edges.some(edge => !edge.backward
        && edge.fromId === 'gateway:decision-gw' && edge.toId === 'stage:done'));
  }

  // Money modeller: calculations block, recalculate self-loop, quote fan-out.
  // The self-loop cycles through a Split gateway, so ranks are only asserted
  // to be finite (matching the original canvas behaviour).
  {
    const workflow = hydrate(fixtures.moneyModeller);
    const { topology } = assertCommonInvariants('money-modeller', workflow, { strictRanks: false });

    check('money-modeller: every rank is a finite number',
      [...topology.ranks.values()].every(rank => Number.isFinite(rank)));
    check('money-modeller: all six states and six gateways are in the topology',
      topology.nodes.filter(node => node.kind === 'stage').length === 6
      && topology.nodes.filter(node => node.kind === 'gateway').length === 6);
  }

  // Empty workflow: never throws, produces an empty single-lane-width canvas.
  {
    const { topology, layout } = computeWorkflowGraphLayout(null, []);
    check('empty: no nodes, no lanes, non-zero bounds',
      topology.nodes.length === 0 && layout.lanes.length === 0
      && layout.bounds.width > 0 && layout.bounds.height > 0);
  }

  if (failures > 0) {
    console.error(`\n${failures} graph layout check(s) failed.`);
  } else {
    console.log('\nAll graph layout checks passed.');
  }
  return failures;
}
