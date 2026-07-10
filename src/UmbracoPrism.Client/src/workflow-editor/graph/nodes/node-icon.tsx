import type { NodeIconDef } from '../node-icons.js';

export function NodeIcon({ icon, size = 16 }: { icon: NodeIconDef; size?: number }) {
  return (
    <svg
      className="node-icon-glyph"
      width={size}
      height={size}
      viewBox={icon.viewBox}
      fill="none"
      stroke="currentColor"
      strokeWidth={1.6}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {icon.paths.map((d, index) => (
        <path key={index} d={d} />
      ))}
    </svg>
  );
}
