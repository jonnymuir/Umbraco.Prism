// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.
//
// Generic live-form runtime. Progressive enhancement for any stage whose
// definition declares a calculations block:
//
//  - reads the embedded live model ([data-wayfinder-live-model]): the calculation set,
//    input types/defaults and service-sourced values the server evaluated with,
//  - listens to the stage's ordinary form controls (field:{key} inputs — GovUk.FieldName's own
//    convention, see readInput's own remarks) and re-evaluates the same declarative definitions
//    via the shared expression engine on every change,
//  - updates whatever declares a binding: stat cards ([data-wayfinder-stat-field]),
//    charts ([data-wayfinder-chart]), slider value readouts ([data-wayfinder-slider]), and
//    visibility wrappers ([data-wayfinder-show-when]).
//
// It contains no domain knowledge and no layout: the service blueprint JSON decides what exists
// on the page; this runtime only keeps it live between (nonce-validated) POSTs. The
// server re-evaluates the identical definitions authoritatively on every render.
import {
  Dec,
  evaluateCalculations,
  evaluateExpression,
  toScope,
  type CalculationSet,
  type CalcScope,
  type CalcValue,
} from '../calculations/calculation-engine.js';

interface LiveModel {
  calculations: CalculationSet;
  inputTypes: Record<string, 'number' | 'string' | 'boolean'>;
  defaults: Record<string, string>;
  service: Record<string, unknown>;
}

const gbp = new Intl.NumberFormat('en-GB', {
  style: 'currency',
  currency: 'GBP',
  maximumFractionDigits: 0,
});

function formatValue(value: CalcValue, format: string | undefined): string {
  if (value instanceof Dec) {
    return format?.toLowerCase() === 'gbp' ? gbp.format(value.toNumber()) : value.toString();
  }

  return String(value);
}

function boot(): void {
  const modelScript = document.querySelector('script[data-wayfinder-live-model]');
  if (!modelScript?.textContent) {
    return;
  }

  let model: LiveModel;
  try {
    model = JSON.parse(modelScript.textContent) as LiveModel;
  } catch {
    return;
  }
  if (!model?.calculations?.fields) {
    return;
  }

  const form = modelScript.closest('form') ?? document.querySelector('form.wayfinder-service-request-form') ?? document;
  const serviceScope = toScope(model.service ?? {});

  const readInput = (key: string): unknown => {
    // GovUk.FieldName's own convention (Wayfinder.Rendering.GovUk) is "field:{key}", not
    // "fields[key]" — found live: every slider/input's real name= attribute never matched this
    // selector at all, so no interaction ever re-triggered update() (the initial on-load call
    // added below happened to mask this for the very first render, since it reads the same
    // server-supplied defaults the server itself rendered with — but every subsequent drag,
    // and the Recalculate button's own client-side repaint immediately after, silently fell
    // back to those defaults too, undoing whatever the user had actually changed).
    const controls = form.querySelectorAll<HTMLInputElement | HTMLSelectElement>(
      `[name="field:${key}"]`,
    );
    let raw: string | null = null;
    for (const control of controls) {
      if (control instanceof HTMLInputElement && (control.type === 'radio' || control.type === 'checkbox')) {
        if (control.checked) {
          raw = control.value;
          break;
        }
      } else {
        raw = control.value;
        break;
      }
    }

    if (raw === null || raw === '') {
      raw = model.defaults[key] ?? null;
    }

    const type = model.inputTypes[key];

    if (raw === null) {
      // Absent (nothing typed/ticked yet, no declared default) isn't the same as unknown — the
      // field is genuinely declared on this stage, it just has no value in the browser right
      // now. A number has no safe placeholder (0 is a real, meaningful value), so it stays out
      // of scope and any expression referencing it bare simply doesn't evaluate yet (see the
      // catch in update(), which leaves server-rendered values until it can). String/boolean
      // fields DO have a safe "nothing here" value — matching CalculationScopeBuilder.Build's
      // server-side rule (Wayfinder/Services/Calculations/CalculationScopeBuilder.cs).
      if (type === 'number') {
        return undefined;
      }
      return type === 'boolean' ? false : '';
    }

    if (type === 'number') {
      const cleaned = raw.replace(/£|,/g, '').trim();
      return /^-?\d+(\.\d+)?$/.test(cleaned) ? Dec.fromString(cleaned) : undefined;
    }

    if (type === 'boolean') {
      // A checked GOV.UK checkbox's own value="true" (or a string default authored the same
      // way) needs coercing to a real boolean the same way CalculationScopeBuilder.Build does
      // server-side — the calculation engine's own boolean handling requires an actual boolean,
      // not this string.
      if (raw === 'true' || raw === 'True') return true;
      if (raw === 'false' || raw === 'False') return false;
      return raw;
    }

    return raw;
  };

  const collectScope = (): CalcScope => {
    const scope: CalcScope = { ...serviceScope };
    for (const key of Object.keys(model.inputTypes)) {
      const value = readInput(key);
      if (value !== undefined) {
        scope[key] = value;
      }
    }

    return scope;
  };

  const update = (): void => {
    let scope: CalcScope;
    let output;
    try {
      scope = collectScope();
      output = evaluateCalculations(model.calculations, scope);
    } catch (error) {
      console.warn('prism-live-form: evaluation failed; leaving server-rendered values', error);
      return;
    }

    const fullScope: CalcScope = { ...scope, ...output.fields };

    // Stat cards (and anything else bound to a calculated field).
    document.querySelectorAll<HTMLElement>('[data-wayfinder-stat-field]').forEach((card) => {
      const fieldKey = card.dataset.wayfinderStatField!;
      const value = output.fields[fieldKey];
      if (value === undefined) {
        return;
      }

      const format = model.calculations.fields[fieldKey]?.format;
      card.querySelector('.wayfinder-stat-card__value')?.replaceChildren(formatValue(value, format));
    });

    // Visibility wrappers.
    document.querySelectorAll<HTMLElement>('[data-wayfinder-show-when]').forEach((wrapper) => {
      const expression = wrapper.dataset.wayfinderShowWhen!;
      try {
        const visible = evaluateExpression(expression, fullScope, model.calculations) !== false;
        wrapper.hidden = !visible;
      } catch {
        wrapper.hidden = false;
      }
    });

    // Charts.
    document.querySelectorAll<HTMLElement>('[data-wayfinder-chart]').forEach((figure) => {
      rebuildChart(figure, output.series);
    });
  };

  const updateSliderReadout = (input: HTMLInputElement): void => {
    const wrapper = input.closest('[data-wayfinder-slider]');
    const readout = wrapper?.querySelector<HTMLElement>('[data-wayfinder-slider-value]');
    if (readout) {
      readout.textContent = `${readout.dataset.prefix ?? ''}${input.value}${readout.dataset.suffix ?? ''}`;
    }
  };

  form.addEventListener('input', (event) => {
    const target = event.target as HTMLElement;
    if (target instanceof HTMLInputElement && target.matches('[data-wayfinder-slider-input]')) {
      updateSliderReadout(target);
    }

    if (target.matches('[name^="field:"]')) {
      update();
    }
  });

  form.addEventListener('change', (event) => {
    if ((event.target as HTMLElement).matches('[name^="field:"]')) {
      update();
    }
  });

  // Run once on load, not just after the first interaction. The chart in particular needs
  // this: the server renders its bars/legend with literal style="height:...;background:..."
  // attributes (per-datapoint values computed from real data, not a fixed set of classes CSP
  // could allow-list), which a strict style-src with no unsafe-inline blocks outright — found
  // live, a real chart rendering fully blank until the user touched a slider. rebuildChart's
  // own DOM writes (element.style.height = ..., not a "style" attribute string) aren't inline
  // style="" attributes at all as far as CSP is concerned, so running the exact same update()
  // this handler already runs on every change once immediately here repaints the chart (and
  // re-confirms every other bound value) through that same safe path before the user does
  // anything, rather than leaving the page relying on the server's blocked markup until they do.
  update();
}

function rebuildChart(figure: HTMLElement, series: Record<string, Array<Record<string, CalcValue>>>): void {
  const configScript = figure.querySelector('script[data-wayfinder-chart-config]');
  if (!configScript?.textContent) {
    return;
  }

  let config: {
    series: string;
    x: string;
    xLabelEvery: number;
    bands: Array<{ key: string; label: string; color?: string | null }>;
  };
  try {
    config = JSON.parse(configScript.textContent);
  } catch {
    return;
  }

  const rows = series[config.series];
  if (!rows) {
    return;
  }

  // Same validated categorical palette the server-side partial uses.
  const palette = ['#4f46e5', '#0d9488', '#b45309', '#6d28d9'];
  const bands = config.bands.map((band, index) => ({
    ...band,
    color: band.color ?? palette[index % palette.length],
  }));

  // The legend itself is server-rendered once and never otherwise touched by this function —
  // its swatches carry the same literal style="background:..." CSP blocks, and unlike the bars
  // below, nothing was ever repainting them through a safe element.style.* write. Found live:
  // the bars themselves had real colour, but every legend swatch next to them stayed blank.
  const legendItems = figure.querySelectorAll<HTMLElement>('.wayfinder-chart__legend-item');
  legendItems.forEach((item, index) => {
    const swatch = item.querySelector<HTMLElement>('.wayfinder-chart__swatch');
    const color = bands[index]?.color;
    if (swatch && color) {
      swatch.style.background = color;
    }
  });

  const numeric = rows.map((row) => ({
    x: row[config.x] instanceof Dec ? (row[config.x] as Dec).toNumber() : 0,
    values: bands.map((band) => (row[band.key] instanceof Dec ? (row[band.key] as Dec).toNumber() : 0)),
  }));

  const maxTotal = Math.max(1, ...numeric.map((row) => row.values.reduce((a, b) => a + b, 0)));
  const plotHeight = 160;

  const plot = figure.querySelector<HTMLElement>('[data-wayfinder-chart-plot]');
  if (plot) {
    plot.replaceChildren(
      ...numeric.map((row) => {
        const bar = document.createElement('div');
        bar.className = 'wayfinder-chart__bar';
        bar.title = `${config.x} ${row.x}: ${row.values.reduce((a, b) => a + b, 0).toLocaleString('en-GB')}`;
        row.values.forEach((value, i) => {
          const segment = document.createElement('div');
          segment.style.height = `${Math.round((value / maxTotal) * plotHeight * 10) / 10}px`;
          segment.style.background = bands[i].color!;
          bar.appendChild(segment);
        });
        return bar;
      }),
    );
  }

  const labels = figure.querySelector<HTMLElement>('[data-wayfinder-chart-labels]');
  if (labels) {
    labels.replaceChildren(
      ...numeric.map((row) => {
        const span = document.createElement('span');
        span.textContent = row.x % config.xLabelEvery === 0 ? String(row.x) : '';
        return span;
      }),
    );
  }

  const tableBody = figure.querySelector<HTMLElement>('[data-wayfinder-chart-table] tbody');
  if (tableBody) {
    tableBody.replaceChildren(
      ...numeric
        .filter((row, index) => index === 0 || row.x % config.xLabelEvery === 0)
        .map((row) => {
          const tr = document.createElement('tr');
          const th = document.createElement('th');
          th.scope = 'row';
          th.textContent = String(row.x);
          tr.appendChild(th);
          row.values.forEach((value) => {
            const td = document.createElement('td');
            td.textContent = value.toLocaleString('en-GB');
            tr.appendChild(td);
          });
          return tr;
        }),
    );
  }
}

// Guidance-checklist live progress text ("X of Y completed") — independent of the
// calculations live model above (a service blueprint with a guidance checklist has no reason to also
// declare a calculations block), so this runs unconditionally rather than being gated behind
// boot()'s early return. The actual required-all-acknowledged gate is still server-side
// validation on submit; this is purely a display nicety.
function bootGuidanceChecklists(): void {
  document.querySelectorAll<HTMLElement>('[data-wayfinder-guidance-checklist]').forEach((container) => {
    const progress = container.querySelector<HTMLElement>('[data-wayfinder-guidance-progress]');
    const total = Number(progress?.dataset.wayfinderGuidanceTotal ?? '0');
    if (!progress || total === 0) {
      return;
    }

    container.addEventListener('change', (event) => {
      if (!(event.target as HTMLElement).matches('[data-wayfinder-guidance-checkbox]')) {
        return;
      }

      const completed = container.querySelectorAll('[data-wayfinder-guidance-checkbox]:checked').length;
      progress.textContent = `${completed} of ${total} guidance articles completed`;
    });
  });
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    boot();
    bootGuidanceChecklists();
  });
} else {
  boot();
  bootGuidanceChecklists();
}
