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

  /* Connection anchors: inert and invisible in read-only mode; hover-visible
     grab targets when the canvas is editable. */
  .react-flow__handle.graph-handle {
    opacity: 0;
    width: 8px;
    height: 8px;
    min-width: 0;
    min-height: 0;
    border: none;
    background: transparent;
    pointer-events: none;
    transition: opacity 120ms ease;
  }

  .react-flow__handle.graph-handle.connectable {
    /* Resting state stays subtly visible — a hairline dot, not fully hidden
       — so authors can discover drag-to-connect without having to hover
       every node first. Hover/focus/drag states below make it unmistakable. */
    opacity: 0.55;
    pointer-events: all;
    width: 10px;
    height: 10px;
    border-radius: 50%;
    background: #1d4ed8;
    border: 2px solid #ffffff;
    box-shadow: 0 1px 3px rgba(15, 23, 42, 0.35);
  }

  .react-flow__node:hover .react-flow__handle.graph-handle.connectable,
  .react-flow__handle.graph-handle.connectable:hover,
  .react-flow__handle.graph-handle.connectingfrom,
  .react-flow__handle.graph-handle.connectingto,
  .react-flow__handle.graph-handle.valid {
    opacity: 1;
    width: 12px;
    height: 12px;
  }

  .react-flow__connectionline path {
    stroke: #1d4ed8;
    stroke-width: 2.25;
    stroke-dasharray: 10 8;
  }

  /* Per-transition overlay paths ride on top of the base rail; only their
     selected/simulation/branch colouring should be visible interaction-wise. */
  .edge-path.transition-overlay {
    pointer-events: none;
  }

  /* Shift-marquee multi-select: RF-level selection is a multi-drag aid, shown
     as a dashed outline distinct from the semantic (solid) single selection. */
  .react-flow__node.selected .stage-node,
  .react-flow__node.selected .gateway-node {
    outline: 2px dashed #1d4ed8;
    outline-offset: 3px;
  }

  .react-flow__selection {
    background: rgba(29, 78, 216, 0.06);
    border: 1px dashed #1d4ed8;
  }

  .graph-minimap {
    border: 1px solid #dbe2ea;
    border-radius: 8px;
    overflow: hidden;
  }

  /* The default attribution grey fails WCAG contrast on the canvas gradient. */
  .react-flow__attribution {
    background: rgba(255, 255, 255, 0.85);
  }

  .react-flow__attribution a {
    color: #334155;
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
