/** The branding variables a tenant can override, as the server describes them (with annotations) and as the page declares them (without). */
export interface BrandingVariable {
  variable: string;
  label: string;
  description: string;
  type: string;
  syntax: string;
  currentValue: string;
}

export interface BrandingMetadata {
  sections: Array<{ name: string; variables: BrandingVariable[] }>;
}

export interface BrandingTabVariable {
  name: string;
  defaultValue?: string;
  overrideValue?: string;
  mobileOverrideValue?: string;
}

export interface BrandingTab {
  label: string;
  variables: BrandingTabVariable[];
}

/** A `Record<name, value>` from a saved overrides value, which may arrive as an object or as JSON text. */
export function toOverrideMap(value: unknown): Record<string, string> {
  const source = typeof value === 'string' ? safeParse(value) : value;
  if (!source || typeof source !== 'object') return {};

  return Object.fromEntries(Object.entries(source).filter((entry): entry is [string, string] => typeof entry[1] === 'string'));
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

/** The tabs from the host, with each variable's mobile override filled in from the tenant's saved ones. */
export function tabsWithMobileOverrides(tabs: BrandingTab[], tenant: any): BrandingTab[] {
  const tenantMobileOverrides = toOverrideMap(tenant?.mobileBrandingOverrides);
  return tabs.map(tab => ({
    ...tab,
    variables: tab.variables.map(variable => ({
      ...variable,
      mobileOverrideValue: variable.mobileOverrideValue ?? tenantMobileOverrides[variable.name]
    }))
  }));
}

/**
 * A variable's raw value can itself be a reference to another variable, e.g.
 * "var(--prism-danger, #d4351c)" (see prism-govuk-bridge.css). Detects that and pulls out the
 * target name and optional fallback text.
 */
export function parseLink(rawValue: string | undefined): { target: string; fallback?: string } | null {
  if (!rawValue) return null;
  const match = /^var\(\s*(--[\w-]+)\s*(?:,\s*(.+))?\)$/.exec(rawValue.trim());
  return match ? { target: match[1], fallback: match[2]?.trim() } : null;
}

export function findVariable(metadata: BrandingMetadata | null, name: string): BrandingVariable | undefined {
  return metadata?.sections.flatMap(section => section.variables).find(variable => variable.variable === name);
}

/**
 * Follows a chain of var(--x) references down to a literal value, for swatch previews and for
 * seeding a fallback when a link is first created. Bounded depth guards against a cycle.
 */
export function resolveLiteralValue(
  metadata: BrandingMetadata | null,
  values: Record<string, string>,
  rawValue: string | undefined,
  depth = 0
): string | undefined {
  if (!rawValue || depth > 5) return rawValue;
  const link = parseLink(rawValue);
  if (!link) return rawValue;

  const meta = findVariable(metadata, link.target);
  const targetRaw = meta ? (values[link.target] ?? meta.currentValue) : undefined;
  return resolveLiteralValue(metadata, values, targetRaw, depth + 1) ?? link.fallback;
}

/**
 * Other variables this one could link to: same type, not itself already a link (keeps the picker
 * to one hop, no chains), excluding itself.
 */
export function linkableTargets(metadata: BrandingMetadata | null, values: Record<string, string>, variable: BrandingVariable) {
  const options: Array<{ name: string; value: string; group: string }> = [];
  for (const section of metadata?.sections ?? []) {
    for (const candidate of section.variables) {
      if (candidate.variable === variable.variable || candidate.type !== variable.type) continue;
      if (parseLink(values[candidate.variable] ?? candidate.currentValue)) continue;
      options.push({ name: candidate.label || candidate.variable, value: candidate.variable, group: section.name });
    }
  }
  return options;
}

const trimmedNonEmpty = (value: string | undefined): string | undefined => {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
};

/** Non-empty values by variable name, trimmed; `keep` can drop entries (mobile ones that are inheriting). */
export function nonEmptyOverrides(values: Record<string, string>, keep: (name: string) => boolean = () => true): Record<string, string> {
  const overrides: Record<string, string> = {};
  for (const [name, value] of Object.entries(values)) {
    const trimmed = trimmedNonEmpty(value);
    if (trimmed && keep(name)) overrides[name] = trimmed;
  }
  return overrides;
}

/** The overrides typed into the static (no-metadata) table, by variable name. */
export function staticTableOverrides(tabs: BrandingTab[], field: 'overrideValue' | 'mobileOverrideValue'): Record<string, string> {
  const overrides: Record<string, string> = {};
  for (const variable of tabs.flatMap(tab => tab.variables)) {
    const trimmed = trimmedNonEmpty(variable[field]);
    if (trimmed) overrides[variable.name] = trimmed;
  }
  return overrides;
}
