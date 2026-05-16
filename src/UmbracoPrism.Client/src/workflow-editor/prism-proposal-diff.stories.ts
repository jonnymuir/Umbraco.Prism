import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-proposal-diff.js';
import type { PrismProposalDiffElement } from './prism-proposal-diff.js';
import { STUB_PROPOSAL } from './types.js';
import type { ProposalEnvelope } from './types.js';

type StoryArgs = {
  proposal: ProposalEnvelope | null;
};

function makeElement(args: StoryArgs): PrismProposalDiffElement {
  const el = document.createElement('prism-proposal-diff') as PrismProposalDiffElement;
  el.proposal = args.proposal;
  el.style.cssText = 'display: block; max-width: 480px;';
  return el;
}

const meta: Meta<StoryArgs> = {
  title: 'Workflow Editor/Proposal Diff',
  component: 'prism-proposal-diff',
  tags: ['autodocs'],
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-dialog-name', enabled: true },
        ],
      },
    },
  },
  args: {
    proposal: null,
  },
  render: (args) => makeElement(args),
};

export default meta;
type Story = StoryObj<StoryArgs>;

export const NoProposal: Story = {
  args: { proposal: null },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-proposal-diff') as PrismProposalDiffElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const dialog = root.querySelector('[role="dialog"]');
    await expect(dialog).toBeNull();
  },
};

export const WithProposal: Story = {
  args: { proposal: STUB_PROPOSAL },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 150));
    const el = canvasElement.querySelector('prism-proposal-diff') as PrismProposalDiffElement;
    await el.updateComplete;

    const root = el.shadowRoot!;

    const dialog = root.querySelector('[role="dialog"]');
    await expect(dialog).not.toBeNull();
    await expect(dialog?.getAttribute('aria-modal')).toBe('true');
    await expect(dialog?.getAttribute('data-prism-component')).toBe('proposal-diff');

    const labelledBy = dialog?.getAttribute('aria-labelledby');
    await expect(labelledBy).not.toBeNull();
    const heading = root.querySelector(`#${labelledBy}`);
    await expect(heading).not.toBeNull();

    const rationale = root.querySelector('.rationale-text');
    await expect(rationale?.textContent?.trim()).toContain('identity-and-verification');

    const opItems = root.querySelectorAll('[data-prism-op-index]');
    await expect(opItems.length).toBe(STUB_PROPOSAL.ops.length);

    const firstOp = root.querySelector('[data-prism-op-index="0"]');
    await expect(firstOp).not.toBeNull();

    const acceptBtn = root.querySelector('.btn-accept') as HTMLButtonElement;
    const rejectBtn = root.querySelector('.btn-reject') as HTMLButtonElement;
    await expect(acceptBtn).not.toBeNull();
    await expect(rejectBtn).not.toBeNull();
  },
};

export const AcceptProposal: Story = {
  args: { proposal: STUB_PROPOSAL },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 150));
    const el = canvasElement.querySelector('prism-proposal-diff') as PrismProposalDiffElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const acceptBtn = root.querySelector<HTMLButtonElement>('.btn-accept')!;

    let accepted = false;
    el.addEventListener('proposal-accept', () => { accepted = true; });

    acceptBtn.click();
    await expect(accepted).toBe(true);
  },
};

export const RejectProposal: Story = {
  args: { proposal: STUB_PROPOSAL },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 150));
    const el = canvasElement.querySelector('prism-proposal-diff') as PrismProposalDiffElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const rejectBtn = root.querySelector<HTMLButtonElement>('.btn-reject')!;

    let rejected = false;
    el.addEventListener('proposal-reject', () => { rejected = true; });

    rejectBtn.click();
    await expect(rejected).toBe(true);
  },
};

export const WithValidationFailure: Story = {
  args: {
    proposal: {
      ...STUB_PROPOSAL,
      validationResult: {
        status: 'fail',
        checkedAt: '2026-05-16T13:20:33.659+01:00',
        errors: ['Stage "identity-verification" has no exit transitions.', 'HandoffId "idv-complete" cannot resolve.'],
      },
    },
  },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 150));
    const el = canvasElement.querySelector('prism-proposal-diff') as PrismProposalDiffElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const failBadge = root.querySelector('.validation-badge.fail');
    await expect(failBadge).not.toBeNull();

    const errorList = root.querySelector('.error-list');
    await expect(errorList).not.toBeNull();
    await expect(errorList?.querySelectorAll('li').length).toBe(2);
  },
};
