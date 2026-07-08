// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.
// This island loads on the member-facing money-modeller workflow stage.
import { LitElement, html, css, nothing, svg } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import {
  Dec,
  evaluateCalculations,
  toScope,
  type CalculationSet,
  type CalculationOutput,
} from '../calculations/calculation-engine.js';

/**
 * Mirrors the moneyModel render-data contract built by MoneyModellerService.
 * There is no maths here or anywhere in this component: `calculations` is the same
 * declarative block the server evaluated (from the workflow definition), and this
 * island re-evaluates it locally on every input for instant feedback. The server
 * re-evaluates authoritatively on every render/advance.
 */
export interface MoneyModel {
  member: Record<string, unknown>;
  inputs: {
    retireAge: number;
    benefitOption: string;
    inflation: number;
    salaryGrowth: number;
    invReturn: number;
    moneyBasis: string;
    qPension: number;
    qLump: number;
    qDC: number;
    qAge: number;
  };
  calculations: CalculationSet;
  results: Record<string, unknown>;
}

interface SavedScenario {
  title: string;
  pension: number;
  cash: number;
  total: number;
}

const OPTION_STANDARD = 'Standard benefits';
const OPTION_MAX_TFC = 'Maximum tax-free cash';
const OPTION_DC_CASH = 'Take DC pot as cash';

const OPTION_HINTS: Record<string, string> = {
  [OPTION_STANDARD]: 'Your pension plus the standard tax-free lump sum.',
  [OPTION_MAX_TFC]:
    'Take the biggest tax-free lump sum allowed (25% of the value of your benefits), in exchange for some pension.',
  [OPTION_DC_CASH]:
    'Keep your full DB pension and take the whole DC pot as a one-off payment (part may be taxed).',
};

// Categorical palette validated with the dataviz six-checks script (light surface):
// DB #4f46e5 · DC #0d9488 · State Pension #b45309.
const SERIES = [
  { key: 'db' as const, label: 'DB pension', color: '#4f46e5' },
  { key: 'dc' as const, label: 'DC drawdown', color: '#0d9488' },
  { key: 'sp' as const, label: 'State Pension', color: '#b45309' },
];

const gbp = new Intl.NumberFormat('en-GB', {
  style: 'currency',
  currency: 'GBP',
  maximumFractionDigits: 0,
});

/**
 * Money Modeller interaction island.
 *
 * Progressive enhancement over the server-rendered workflow stage:
 *  - reads its model from the sibling `data-prism-interactive-data` JSON script,
 *  - hides the fallback form controls but keeps writing its state into them, so the
 *    nonce-validated POST (Recalculate / Request a formal quote) is unchanged,
 *  - live-updates the page's stat-group cards via their data-prism-stat-field hooks,
 *  - evaluates the workflow definition's own calculations block via the shared
 *    expression engine — the figures shown are indicative; the server re-evaluates
 *    the identical definitions authoritatively on every POST.
 */
@customElement('prism-money-modeller')
export class PrismMoneyModeller extends LitElement {
  @state() private model: MoneyModel | null = null;
  @state() private retireAge = 66;
  @state() private benefitOption = OPTION_STANDARD;
  @state() private inflation = 2.5;
  @state() private salaryGrowth = 3;
  @state() private invReturn = 5;
  @state() private todaysMoney = true;
  @state() private assumptionsOpen = false;
  @state() private saved: SavedScenario | null = null;
  @state() private hoveredBar: { age: number; total: number; x: number } | null = null;

  connectedCallback(): void {
    super.connectedCallback();
    queueMicrotask(() => this.bootstrap());
  }

  private bootstrap(): void {
    const dataScript = this.querySelector('script[data-prism-interactive-data]');
    if (!dataScript?.textContent) {
      return; // no model — leave the fallback form alone
    }

    let parsed: MoneyModel | null;
    try {
      parsed = JSON.parse(dataScript.textContent) as MoneyModel | null;
    } catch {
      return;
    }
    if (!parsed?.member || !parsed.calculations || !parsed.inputs) {
      return;
    }

    this.model = parsed;
    this.retireAge = parsed.inputs.retireAge;
    this.benefitOption = parsed.inputs.benefitOption;
    this.inflation = parsed.inputs.inflation;
    this.salaryGrowth = parsed.inputs.salaryGrowth;
    this.invReturn = parsed.inputs.invReturn;
    this.todaysMoney = parsed.inputs.moneyBasis !== 'Future money';

    // If the definitions don't evaluate (bad expression, missing input), stay
    // un-upgraded: the server-rendered form remains fully usable.
    if (!this.evaluate()) {
      this.model = null;
      return;
    }

    this.querySelector('[data-prism-interactive-fallback]')?.classList.add('prism-interactive--upgraded');
    this.publish();
  }

  /** Evaluates the workflow's calculation block against the island's current inputs. */
  private evaluate(): CalculationOutput | null {
    const m = this.model!;
    try {
      return evaluateCalculations(
        m.calculations,
        toScope({
          retireAge: this.retireAge,
          benefitOption: this.benefitOption,
          inflation: this.inflation,
          salaryGrowth: this.salaryGrowth,
          invReturn: this.invReturn,
          moneyBasis: this.todaysMoney ? "Today's money" : 'Future money',
          qPension: m.inputs.qPension,
          qLump: m.inputs.qLump,
          qDC: m.inputs.qDC,
          qAge: m.inputs.qAge,
          member: m.member,
        }),
      );
    } catch (error) {
      console.warn('prism-money-modeller: calculation evaluation failed', error);
      return null;
    }
  }

  private static num(output: CalculationOutput, field: string): number {
    const value = output.fields[field];
    return value instanceof Dec ? value.toNumber() : 0;
  }

  private static bool(output: CalculationOutput, field: string): boolean {
    return output.fields[field] === true;
  }

  /** Push current state into the hidden fallback form fields and the page stat cards. */
  private publish(): void {
    this.syncField('retireAge', String(this.retireAge));
    this.syncRadio('benefitOption', this.benefitOption);
    this.syncField('inflation', String(this.inflation));
    this.syncField('salaryGrowth', String(this.salaryGrowth));
    this.syncField('invReturn', String(this.invReturn));
    this.syncRadio('moneyBasis', this.todaysMoney ? "Today's money" : 'Future money');

    const output = this.evaluate();
    if (!output) {
      return;
    }

    this.updateStat('resultPension', gbp.format(PrismMoneyModeller.num(output, 'resultPension')));
    this.updateStat('resultCash', gbp.format(PrismMoneyModeller.num(output, 'resultCash')));
    this.updateStat('resultDcIncome', gbp.format(PrismMoneyModeller.num(output, 'resultDcIncome')));
    this.updateStat('resultTotal', gbp.format(PrismMoneyModeller.num(output, 'resultTotal')));
  }

  private syncField(key: string, value: string): void {
    const input = this.querySelector<HTMLInputElement>(`[name="fields[${key}]"]`);
    if (input) {
      input.value = value;
    }
  }

  private syncRadio(key: string, value: string): void {
    const radios = this.querySelectorAll<HTMLInputElement>(`[name="fields[${key}]"]`);
    radios.forEach((radio) => {
      radio.checked = radio.value === value;
    });
  }

  private updateStat(fieldKey: string, value: string): void {
    const card = this.closest('form, body')?.querySelector(`[data-prism-stat-field="${fieldKey}"]`);
    card?.querySelector('.prism-stat-card__value')?.replaceChildren(value);
  }

  private setState(patch: Partial<{
    retireAge: number;
    benefitOption: string;
    inflation: number;
    salaryGrowth: number;
    invReturn: number;
    todaysMoney: boolean;
  }>): void {
    Object.assign(this, patch);
    this.publish();
  }

  private saveScenario(): void {
    const output = this.evaluate();
    if (!output) {
      return;
    }

    this.saved = {
      title: `Retire at ${this.retireAge}, ${this.benefitOption}`,
      pension: PrismMoneyModeller.num(output, 'resultPension'),
      cash: PrismMoneyModeller.num(output, 'resultCash'),
      total: PrismMoneyModeller.num(output, 'resultTotal'),
    };
  }

  render() {
    if (!this.model) {
      return nothing;
    }

    const m = this.model;
    const output = this.evaluate();
    if (!output) {
      return nothing;
    }

    const quoteMode = PrismMoneyModeller.bool(output, 'quoteMode');
    const hasDc = PrismMoneyModeller.bool(output, 'hasDc');
    const npa = PrismMoneyModeller.num(output, 'npa');
    const minRetireAge = PrismMoneyModeller.num(output, 'minRetireAge');
    const maxRetireAge = PrismMoneyModeller.num(output, 'maxRetireAge');
    const memberActive = m.member.active === true;
    const options = [OPTION_STANDARD, OPTION_MAX_TFC, ...(hasDc ? [OPTION_DC_CASH] : [])];
    const showSalaryGrowth = memberActive && !quoteMode;
    const showInvReturn = hasDc && !quoteMode;

    return html`
      <div class="modeller" data-prism-modeller>
        ${quoteMode
          ? html`<p class="quote-badge" data-prism-quote-badge>
              Using your retirement quote at age ${m.inputs.qAge}
            </p>`
          : nothing}

        ${!quoteMode
          ? html`
              <div class="control" data-prism-control="retireAge">
                <div class="control__head">
                  <label for="mm-retire-age">When do you want to retire?</label>
                  <span class="control__value">${this.retireAge}</span>
                </div>
                <input
                  id="mm-retire-age"
                  type="range"
                  min=${minRetireAge}
                  max=${maxRetireAge}
                  step="1"
                  .value=${String(this.retireAge)}
                  @input=${(e: Event) =>
                    this.setState({ retireAge: Number((e.target as HTMLInputElement).value) })}
                />
                <div class="control__bounds">
                  <span>${minRetireAge}</span>
                  <span>Normal Pension Age ${npa}</span>
                  <span>${maxRetireAge}</span>
                </div>
                ${this.retireAge < npa
                  ? html`<p class="note" role="note">
                      Retiring before ${npa} reduces your DB pension, because it's paid for longer.
                    </p>`
                  : nothing}
              </div>
            `
          : nothing}

        <fieldset class="control" data-prism-control="benefitOption">
          <legend>How do you want to take your benefits?</legend>
          <div class="options">
            ${options.map(
              (option) => html`
                <label class="option ${this.benefitOption === option ? 'option--active' : ''}">
                  <input
                    type="radio"
                    name="mm-option"
                    .value=${option}
                    .checked=${this.benefitOption === option}
                    @change=${() => this.setState({ benefitOption: option })}
                  />
                  <span class="option__title">${option}</span>
                  <span class="option__hint">${OPTION_HINTS[option]}</span>
                </label>
              `,
            )}
          </div>
        </fieldset>

        ${!quoteMode
          ? html`
              <div class="control control--assumptions" data-prism-control="assumptions">
                <button
                  type="button"
                  class="assumptions-toggle"
                  aria-expanded=${this.assumptionsOpen}
                  @click=${() => (this.assumptionsOpen = !this.assumptionsOpen)}
                >
                  Assumptions <span>${this.assumptionsOpen ? 'Hide' : 'Change'}</span>
                </button>
                ${this.assumptionsOpen
                  ? html`
                      ${this.renderAssumption('inflation', 'Inflation (CPI)', 0, 5, this.inflation, (v) =>
                        this.setState({ inflation: v }),
                      )}
                      ${showSalaryGrowth
                        ? this.renderAssumption('salaryGrowth', 'Yearly salary growth', 0, 6, this.salaryGrowth, (v) =>
                            this.setState({ salaryGrowth: v }),
                          )
                        : nothing}
                      ${showInvReturn
                        ? this.renderAssumption('invReturn', 'Investment return', 0, 8, this.invReturn, (v) =>
                            this.setState({ invReturn: v }),
                          )
                        : nothing}
                      <div class="basis" role="group" aria-label="Show amounts in">
                        <button
                          type="button"
                          class=${this.todaysMoney ? 'basis--active' : ''}
                          @click=${() => this.setState({ todaysMoney: true })}
                        >
                          Today's money
                        </button>
                        <button
                          type="button"
                          class=${!this.todaysMoney ? 'basis--active' : ''}
                          @click=${() => this.setState({ todaysMoney: false })}
                        >
                          Future money
                        </button>
                      </div>
                    `
                  : nothing}
              </div>
            `
          : nothing}

        ${this.renderChart(output)}

        <div class="compare">
          <button type="button" class="save-btn" data-prism-save-scenario @click=${this.saveScenario}>
            ${this.saved ? 'Save current scenario instead' : '+ Save this scenario to compare'}
          </button>
          ${this.saved
            ? html`
                <div class="compare__panel" data-prism-compare>
                  <div class="compare__col">
                    <h3>Saved — ${this.saved.title}</h3>
                    <p>Pension: <strong>${gbp.format(this.saved.pension)}/yr</strong></p>
                    <p>Cash: <strong>${gbp.format(this.saved.cash)}</strong></p>
                    <p>Total income: <strong>${gbp.format(this.saved.total)}/yr</strong></p>
                  </div>
                  <div class="compare__col">
                    <h3>Current — Retire at ${this.retireAge}, ${this.benefitOption}</h3>
                    <p>
                      Pension:
                      <strong>${gbp.format(PrismMoneyModeller.num(output, 'resultPension'))}/yr</strong>
                    </p>
                    <p>Cash: <strong>${gbp.format(PrismMoneyModeller.num(output, 'resultCash'))}</strong></p>
                    <p>
                      Total income:
                      <strong>${gbp.format(PrismMoneyModeller.num(output, 'resultTotal'))}/yr</strong>
                    </p>
                  </div>
                  <button type="button" class="compare__clear" @click=${() => (this.saved = null)}>
                    Remove
                  </button>
                </div>
              `
            : nothing}
        </div>

        <p class="sr-only" aria-live="polite" data-prism-result-announcement>
          Estimated DB pension ${gbp.format(PrismMoneyModeller.num(output, 'resultPension'))} a year,
          ${String(output.fields.cashLabel ?? 'cash').toLowerCase()}
          ${gbp.format(PrismMoneyModeller.num(output, 'resultCash'))}, total income
          ${gbp.format(PrismMoneyModeller.num(output, 'resultTotal'))} a year from age
          ${PrismMoneyModeller.num(output, 'retireAgeEff')}.
        </p>
        <slot></slot>
      </div>
    `;
  }

  private renderAssumption(
    key: string,
    label: string,
    min: number,
    max: number,
    value: number,
    onChange: (value: number) => void,
  ) {
    return html`
      <div class="assumption" data-prism-control=${key}>
        <div class="control__head">
          <label for="mm-${key}">${label}</label>
          <span class="control__value">${value}%</span>
        </div>
        <input
          id="mm-${key}"
          type="range"
          min=${min}
          max=${max}
          step="0.5"
          .value=${String(value)}
          @input=${(e: Event) => onChange(Number((e.target as HTMLInputElement).value))}
        />
      </div>
    `;
  }

  private renderChart(output: CalculationOutput) {
    const rows = output.series.incomeByAge ?? [];
    if (rows.length === 0) {
      return nothing;
    }

    const bars = rows.map((row) => ({
      age: row.age instanceof Dec ? row.age.toNumber() : 0,
      db: row.db instanceof Dec ? row.db.toNumber() : 0,
      dc: row.dc instanceof Dec ? row.dc.toNumber() : 0,
      sp: row.sp instanceof Dec ? row.sp.toNumber() : 0,
    }));

    const width = 720;
    const height = 190;
    const plotHeight = 160;
    const gap = 2;
    const barWidth = width / bars.length - gap;
    const maxTotal = Math.max(1, ...bars.map((b) => b.db + b.dc + b.sp));
    const scale = (v: number) => (v / maxTotal) * plotHeight;

    return html`
      <figure class="chart" data-prism-chart>
        <figcaption>
          Your estimated yearly income, age ${bars[0].age} to ${bars[bars.length - 1].age}
        </figcaption>
        <div class="chart__legend">
          ${SERIES.map(
            (s) => html`<span class="chart__legend-item">
              <span class="chart__swatch" style="background:${s.color}"></span>${s.label}
            </span>`,
          )}
        </div>
        <div class="chart__plot">
          <svg
            viewBox="0 0 ${width} ${height}"
            role="img"
            aria-label="Stacked bar chart of estimated yearly income by age. Data table follows."
            @mouseleave=${() => (this.hoveredBar = null)}
          >
            ${bars.map((bar, i) => {
              const x = i * (barWidth + gap);
              const total = bar.db + bar.dc + bar.sp;
              let y = plotHeight;
              const segments = [
                { h: scale(bar.db), color: SERIES[0].color },
                { h: scale(bar.dc), color: SERIES[1].color },
                { h: scale(bar.sp), color: SERIES[2].color },
              ].map((seg) => {
                y -= seg.h;
                return svg`<rect x=${x} y=${y + gap / 2} width=${barWidth}
                  height=${Math.max(0, seg.h - gap / 2)} fill=${seg.color} rx="1"></rect>`;
              });
              return svg`
                <g
                  @mouseenter=${() => (this.hoveredBar = { age: bar.age, total, x })}
                >
                  <rect x=${x} y="0" width=${barWidth + gap} height=${plotHeight} fill="transparent"></rect>
                  ${segments}
                  ${bar.age % 5 === 0
                    ? svg`<text x=${x + barWidth / 2} y=${height - 4} text-anchor="middle"
                        class="chart__tick">${bar.age}</text>`
                    : nothing}
                </g>`;
            })}
          </svg>
          ${this.hoveredBar
            ? html`<div
                class="chart__tooltip"
                style="left:${(this.hoveredBar.x / width) * 100}%"
                role="status"
              >
                Age ${this.hoveredBar.age}: ${gbp.format(this.hoveredBar.total)} a year
              </div>`
            : nothing}
        </div>
        <table class="sr-only">
          <caption>Estimated yearly income by age</caption>
          <thead>
            <tr><th scope="col">Age</th>${SERIES.map((s) => html`<th scope="col">${s.label}</th>`)}</tr>
          </thead>
          <tbody>
            ${bars
              .filter((b) => b.age % 5 === 0 || b.age === bars[0].age)
              .map(
                (b) => html`<tr>
                  <th scope="row">${b.age}</th>
                  <td>${gbp.format(b.db)}</td>
                  <td>${gbp.format(b.dc)}</td>
                  <td>${gbp.format(b.sp)}</td>
                </tr>`,
              )}
          </tbody>
        </table>
      </figure>
    `;
  }

  static styles = css`
    :host {
      display: block;
      font-family: inherit;
      color: inherit;
    }

    .modeller {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      margin-bottom: 1.5rem;
    }

    .quote-badge {
      align-self: flex-start;
      margin: 0;
      background: #fef3c7;
      border: 1px solid #d97706;
      border-radius: 4px;
      padding: 0.25rem 0.625rem;
      font-size: 0.875rem;
      font-weight: 700;
    }

    .control {
      background: #ffffff;
      border: 1px solid #d1d5db;
      border-radius: 8px;
      padding: 1rem 1.25rem;
    }

    .control__head {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin-bottom: 0.5rem;
    }

    .control__head label,
    legend {
      font-weight: 700;
      font-size: 1rem;
    }

    .control__value {
      font-size: 1.375rem;
      font-weight: 800;
      color: var(--prism-primary, #4f46e5);
      font-variant-numeric: tabular-nums;
    }

    input[type='range'] {
      width: 100%;
      accent-color: var(--prism-primary, #4f46e5);
      min-height: 1.75rem;
    }

    .control__bounds {
      display: flex;
      justify-content: space-between;
      font-size: 0.75rem;
      color: #6b7280;
    }

    .note {
      margin: 0.75rem 0 0;
      font-size: 0.8125rem;
      background: #fef3c7;
      border-radius: 6px;
      padding: 0.5rem 0.75rem;
    }

    fieldset.control {
      border: 1px solid #d1d5db;
      margin: 0;
    }

    .options {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin-top: 0.75rem;
    }

    .option {
      display: block;
      border: 1px solid #d1d5db;
      border-radius: 8px;
      padding: 0.75rem 0.875rem;
      cursor: pointer;
    }

    .option--active {
      border: 2px solid var(--prism-primary, #4f46e5);
      background: #eef2ff;
      padding: calc(0.75rem - 1px) calc(0.875rem - 1px);
    }

    .option input {
      position: absolute;
      opacity: 0;
    }

    .option:has(input:focus-visible) {
      outline: 3px solid #fbbf24;
      outline-offset: 1px;
    }

    .option__title {
      display: block;
      font-weight: 700;
      font-size: 0.9375rem;
    }

    .option__hint {
      display: block;
      font-size: 0.8125rem;
      color: #4b5563;
      margin-top: 0.125rem;
    }

    .assumptions-toggle {
      display: flex;
      justify-content: space-between;
      width: 100%;
      background: none;
      border: none;
      padding: 0;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }

    .assumptions-toggle span {
      color: var(--prism-primary, #4f46e5);
      font-weight: 600;
      font-size: 0.875rem;
    }

    .assumption {
      margin-top: 1rem;
    }

    .assumption .control__value {
      font-size: 1rem;
    }

    .basis {
      display: flex;
      border: 1px solid #d1d5db;
      border-radius: 6px;
      overflow: hidden;
      margin-top: 1rem;
    }

    .basis button {
      flex: 1;
      border: none;
      background: #ffffff;
      padding: 0.5rem;
      font: inherit;
      font-size: 0.8125rem;
      font-weight: 600;
      cursor: pointer;
    }

    .basis .basis--active {
      background: var(--prism-primary, #4f46e5);
      color: #ffffff;
    }

    .chart {
      margin: 0;
      background: #ffffff;
      border: 1px solid #d1d5db;
      border-radius: 8px;
      padding: 1rem 1.25rem;
    }

    .chart figcaption {
      font-weight: 700;
      margin-bottom: 0.5rem;
    }

    .chart__legend {
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
      font-size: 0.75rem;
      color: #4b5563;
      margin-bottom: 0.75rem;
    }

    .chart__legend-item {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
    }

    .chart__swatch {
      width: 10px;
      height: 10px;
      border-radius: 2px;
    }

    .chart__plot {
      position: relative;
    }

    .chart__plot svg {
      display: block;
      width: 100%;
      height: auto;
    }

    .chart__tick {
      font-size: 11px;
      fill: #6b7280;
    }

    .chart__tooltip {
      position: absolute;
      top: -0.25rem;
      transform: translateX(-50%);
      background: #111827;
      color: #ffffff;
      font-size: 0.75rem;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      white-space: nowrap;
      pointer-events: none;
    }

    .compare {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .save-btn {
      align-self: flex-start;
      background: #ffffff;
      border: 1px dashed var(--prism-primary, #4f46e5);
      color: var(--prism-primary, #4f46e5);
      border-radius: 8px;
      padding: 0.625rem 1rem;
      font: inherit;
      font-size: 0.875rem;
      font-weight: 700;
      cursor: pointer;
    }

    .compare__panel {
      position: relative;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 1rem;
      background: #fef9ec;
      border: 1px solid #d9c58a;
      border-radius: 8px;
      padding: 2rem 1.25rem 1rem;
      font-size: 0.875rem;
    }

    .compare__col h3 {
      margin: 0 0 0.375rem;
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .compare__col p {
      margin: 0.125rem 0;
    }

    .compare__clear {
      position: absolute;
      top: 0.5rem;
      right: 0.75rem;
      background: none;
      border: none;
      font: inherit;
      font-size: 0.75rem;
      text-decoration: underline;
      color: var(--prism-primary, #4f46e5);
      cursor: pointer;
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
      border: 0;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-money-modeller': PrismMoneyModeller;
  }
}
