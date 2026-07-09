import reactFlowCss from '@xyflow/react/dist/style.css?inline';

/**
 * React Flow ships its stylesheet for global injection, which never reaches a
 * shadow root — so it is adopted as a constructable stylesheet instead,
 * alongside the handful of overrides that adapt the existing canvas classes
 * (defined in the Lit component's static styles) to React Flow's DOM.
 */
const GRAPH_CANVAS_OVERRIDES = `
  .graph-react-host {
    position: relative;
    flex: 1;
    min-height: 0;
    width: 100%;
    height: 100%;
  }

  .graph-react-host .react-flow {
    background: transparent;
    font: inherit;
  }

  /* React Flow disables pointer events on nodes it considers non-interactive
     (not draggable/selectable at the RF level). Selection lives on our own
     inner buttons, so force events back on. */
  .graph-react-host .react-flow__node {
    pointer-events: all !important;
  }

  /* Node shells fill the React Flow node wrapper instead of positioning themselves. */
  .react-flow__node .stage-node-shell,
  .react-flow__node .gateway-node-shell {
    position: relative;
    width: 100%;
    height: 100%;
  }

  .react-flow__node .stage-node,
  .react-flow__node .gateway-node {
    width: 100%;
    height: 100%;
  }

  /* Invisible connection anchors until drag-to-connect ships. */
  .graph-handle,
  .react-flow__handle.graph-handle {
    opacity: 0;
    width: 8px;
    height: 8px;
    min-width: 0;
    min-height: 0;
    border: none;
    background: transparent;
    pointer-events: none;
  }

  /* Per-transition overlay paths ride on top of the base rail; only their
     selected/simulation/branch colouring should be visible interaction-wise. */
  .edge-path.transition-overlay {
    pointer-events: none;
  }
`;

let sheets: CSSStyleSheet[] | null = null;

export function graphStyleSheets(): CSSStyleSheet[] {
  if (!sheets) {
    const reactFlowSheet = new CSSStyleSheet();
    reactFlowSheet.replaceSync(reactFlowCss);
    const overrideSheet = new CSSStyleSheet();
    overrideSheet.replaceSync(GRAPH_CANVAS_OVERRIDES);
    sheets = [reactFlowSheet, overrideSheet];
  }
  return sheets;
}
