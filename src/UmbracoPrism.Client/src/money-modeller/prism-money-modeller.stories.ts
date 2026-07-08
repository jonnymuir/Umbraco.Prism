import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { unsafeHTML } from 'lit/directives/unsafe-html.js';
import { expect, within, userEvent } from '@storybook/test';
import './prism-money-modeller';
import type { MoneyModel } from './prism-money-modeller';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const PARAMETERS = {
  accrualDivisor: 75,
  lumpAccrualFactor: 3,
  salaryThreshold: 74208,
  normalPensionAge: 66,
  minRetirementAge: 55,
  maxRetirementAge: 75,
  earlyPensionReductionPerYear: 0.04,
  earlyLumpReductionPerYear: 0.025,
  lateUpliftPerYear: 0.03,
  commutationRate: 12,
  taxFreeShare: 0.25,
  dcDrawdownYears: 20,
  statePensionAmount: 11975,
  statePensionAge: 68,
  aboveThresholdDcRate: 0.2,
};

const DEFAULT_INPUTS = {
  retireAge: 66,
  benefitOption: 'Standard benefits',
  inflation: 2.5,
  salaryGrowth: 3,
  invReturn: 5,
  todaysMoney: true,
  quoteMode: false,
  quotePension: 0,
  quoteLump: 0,
  quoteDc: 0,
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
  parameters: PARAMETERS,
  inputs: { ...DEFAULT_INPUTS },
  results: { pension: 0, cash: 0, cashLabel: 'Tax-free cash', dcIncome: 0, total: 0 },
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
    quoteMode: true,
    retireAge: 63,
    quotePension: 18500,
    quoteLump: 55500,
    quoteDc: 48000,
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

    // No DC pot and salary under the threshold — the DC cash option is not offered.
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
