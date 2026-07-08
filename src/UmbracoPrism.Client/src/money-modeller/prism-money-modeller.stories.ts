import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { unsafeHTML } from 'lit/directives/unsafe-html.js';
import { expect, within, userEvent } from '@storybook/test';
import './prism-money-modeller';
import type { MoneyModel } from './prism-money-modeller';
import type { CalculationSet } from '../calculations/calculation-engine.js';

// ─── Fixtures ─────────────────────────────────────────────────────────────────
// Demo calculation set for stories: same field-name contract as the real
// money-modeller.json seed, with simplified demo formulas. The real maths lives
// only in the workflow definition; conformance between the two evaluators is
// pinned by the shared golden fixtures (npm run test:calc).

const DEMO_CALCULATIONS: CalculationSet = {
  tables: {
    pensionAgeFactor: { interpolate: 'linear', values: { '55': 0.56, '66': 1.0, '75': 1.27 } },
  },
  fields: {
    member: { source: 'service' },
    quoteMode: { expr: 'qPension > 0' },
    npa: { expr: '66' },
    statePensionAge: { expr: '68' },
    minRetireAge: { expr: 'max(55, member.age + 1)' },
    maxRetireAge: { expr: '75' },
    retireAgeEff: { expr: 'clamp(if(quoteMode, qAge, retireAge), minRetireAge, maxRetireAge)' },
    hasDc: { expr: 'if(quoteMode, qDC > 0, member.dcPot > 0)' },
    cashLabel: { expr: "if(benefitOption = 'Take DC pot as cash', 'One-off cash', 'Tax-free cash')" },
    basePension: { expr: 'if(quoteMode, qPension, member.accruedPension)' },
    resultPension: { expr: 'round(basePension * lookup(pensionAgeFactor, retireAgeEff))' },
    resultCash: { expr: 'round(if(quoteMode, qLump, member.accruedLump))' },
    resultDcIncome: { expr: 'round(if(quoteMode, qDC, member.dcPot) / 20)' },
    resultTotal: { expr: 'round(resultPension + resultDcIncome)' },
  },
  series: {
    incomeByAge: {
      over: 'age',
      from: 'retireAgeEff',
      to: '90',
      values: {
        db: 'resultPension',
        dc: 'if(age < retireAgeEff + 20, resultDcIncome, 0)',
        sp: 'if(age >= statePensionAge, 11975, 0)',
      },
    },
  },
};

const DEFAULT_INPUTS = {
  retireAge: 66,
  benefitOption: 'Standard benefits',
  inflation: 2.5,
  salaryGrowth: 3,
  invReturn: 5,
  moneyBasis: "Today's money",
  qPension: 0,
  qLump: 0,
  qDC: 0,
  qAge: 66,
};

const ACTIVE_DB_DC: MoneyModel = {
  member: {
    name: 'Dr Sarah Mitchell',
    active: true,
    age: 47,
    salary: 82000,
    accruedPension: 16400,
    accruedLump: 49200,
    dcPot: 48300,
  },
  inputs: { ...DEFAULT_INPUTS },
  calculations: DEMO_CALCULATIONS,
  results: {},
};

const ACTIVE_DB_ONLY: MoneyModel = {
  ...ACTIVE_DB_DC,
  member: {
    name: 'James Okafor',
    active: true,
    age: 39,
    salary: 46000,
    accruedPension: 7800,
    accruedLump: 23400,
    dcPot: 0,
  },
};

const DEFERRED: MoneyModel = {
  ...ACTIVE_DB_DC,
  member: {
    name: 'Prof Anne Whitfield',
    active: false,
    age: 54,
    salary: 0,
    accruedPension: 9600,
    accruedLump: 28800,
    dcPot: 21000,
  },
};

const QUOTE_MODE: MoneyModel = {
  ...ACTIVE_DB_DC,
  inputs: {
    ...DEFAULT_INPUTS,
    retireAge: 63,
    qAge: 63,
    qPension: 18500,
    qLump: 55500,
    qDC: 48000,
  },
};

// The island bootstraps from the light-DOM JSON script and fallback fields,
// exactly as the _PrismComponent-Interactive partial renders them.
const renderIsland = (model: MoneyModel) => html`
  <div class="prism-interactive" data-prism-interactive="prism-money-modeller" style="max-width: 720px">
    <prism-money-modeller>
      ${unsafeHTML(
        `<script type="application/json" data-prism-interactive-data>${JSON.stringify(model)}</script>`,
      )}
      <div data-prism-interactive-fallback>
        <input type="range" name="fields[retireAge]" value="${model.inputs.retireAge}" />
        <input type="range" name="fields[inflation]" value="${model.inputs.inflation}" />
        <input type="range" name="fields[salaryGrowth]" value="${model.inputs.salaryGrowth}" />
        <input type="range" name="fields[invReturn]" value="${model.inputs.invReturn}" />
        <input type="radio" name="fields[benefitOption]" value="Standard benefits" />
        <input type="radio" name="fields[benefitOption]" value="Maximum tax-free cash" />
        <input type="radio" name="fields[benefitOption]" value="Take DC pot as cash" />
        <input type="radio" name="fields[moneyBasis]" value="Today's money" />
        <input type="radio" name="fields[moneyBasis]" value="Future money" />
      </div>
    </prism-money-modeller>
  </div>
`;

const meta: Meta = {
  title: 'Money Modeller/prism-money-modeller',
  component: 'prism-money-modeller',
};

export default meta;
type Story = StoryObj;

export const ActiveMemberWithDc: Story = {
  render: () => renderIsland(ACTIVE_DB_DC),
  play: async ({ canvasElement }) => {
    const island = canvasElement.querySelector('prism-money-modeller')!;
    await new Promise((resolve) => setTimeout(resolve, 50));
    const shadow = within(island.shadowRoot as unknown as HTMLElement);

    await expect(shadow.getByLabelText('When do you want to retire?')).toBeVisible();
    await expect(shadow.getByText('Take DC pot as cash')).toBeVisible();

    const fallback = island.querySelector('[data-prism-interactive-fallback]')!;
    await expect(fallback.classList.contains('prism-interactive--upgraded')).toBe(true);
  },
};

export const ActiveMemberDbOnly: Story = {
  render: () => renderIsland(ACTIVE_DB_ONLY),
  play: async ({ canvasElement }) => {
    const island = canvasElement.querySelector('prism-money-modeller')!;
    await new Promise((resolve) => setTimeout(resolve, 50));
    const shadow = within(island.shadowRoot as unknown as HTMLElement);

    // No DC pot — the calculated hasDc field is false, so the option is not offered.
    await expect(shadow.queryByText('Take DC pot as cash')).toBeNull();
  },
};

export const DeferredMember: Story = {
  render: () => renderIsland(DEFERRED),
};

export const UsingRetirementQuote: Story = {
  render: () => renderIsland(QUOTE_MODE),
  play: async ({ canvasElement }) => {
    const island = canvasElement.querySelector('prism-money-modeller')!;
    await new Promise((resolve) => setTimeout(resolve, 50));
    const shadow = within(island.shadowRoot as unknown as HTMLElement);

    // Quote mode pins the retirement age, so the slider is not shown.
    await expect(shadow.getByText(/Using your retirement quote/)).toBeVisible();
    await expect(shadow.queryByLabelText('When do you want to retire?')).toBeNull();
  },
};

export const SavesScenarioForComparison: Story = {
  render: () => renderIsland(ACTIVE_DB_DC),
  play: async ({ canvasElement }) => {
    const island = canvasElement.querySelector('prism-money-modeller')!;
    await new Promise((resolve) => setTimeout(resolve, 50));
    const shadow = within(island.shadowRoot as unknown as HTMLElement);

    await userEvent.click(shadow.getByRole('button', { name: /Save this scenario/ }));
    await expect(shadow.getByText(/Saved — Retire at 66/)).toBeVisible();

    // Changing an input syncs the hidden workflow form field.
    const slider = shadow.getByLabelText('When do you want to retire?') as HTMLInputElement;
    slider.value = '60';
    slider.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    await new Promise((resolve) => setTimeout(resolve, 50));

    const hidden = island.querySelector<HTMLInputElement>('[name="fields[retireAge]"]')!;
    await expect(hidden.value).toBe('60');
  },
};
