import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-conversation-pane.js';
import type { PrismConversationPaneElement } from './prism-conversation-pane.js';
import { STUB_PROPOSAL } from './types.js';
import type { ProposalEnvelope } from './types.js';

type StoryArgs = {
  proposal: ProposalEnvelope | null;
};

function makeElement(args: StoryArgs): PrismConversationPaneElement {
  const el = document.createElement('prism-conversation-pane') as PrismConversationPaneElement;
  el.proposal = args.proposal;
  el.style.cssText = 'display: block; width: 360px; height: 500px;';
  return el;
}

const meta: Meta<StoryArgs> = {
  title: 'Workflow Editor/Conversation Pane',
  component: 'prism-conversation-pane',
  tags: ['autodocs'],
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
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

export const Empty: Story = {
  args: { proposal: null },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-conversation-pane') as PrismConversationPaneElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const container = root.querySelector('[data-prism-component="conversation-pane"]');
    await expect(container).not.toBeNull();

    const input = root.querySelector('[data-prism-conversation-input]');
    await expect(input).not.toBeNull();
    await expect(input?.tagName.toLowerCase()).toBe('textarea');

    const liveRegion = root.querySelector('[aria-live="polite"]');
    await expect(liveRegion).not.toBeNull();

    const submitBtn = root.querySelector('.submit-btn') as HTMLButtonElement;
    await expect(submitBtn).not.toBeNull();
    await expect(submitBtn.disabled).toBe(true);
  },
};

export const WithProposal: Story = {
  args: { proposal: STUB_PROPOSAL },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-conversation-pane') as PrismConversationPaneElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const proposalArea = root.querySelector('.proposal-area');
    await expect(proposalArea).not.toBeNull();

    const diffEl = root.querySelector('prism-proposal-diff');
    await expect(diffEl).not.toBeNull();
  },
};

export const WithMessage: Story = {
  args: { proposal: null },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 100));
    const el = canvasElement.querySelector('prism-conversation-pane') as PrismConversationPaneElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const textarea = root.querySelector<HTMLTextAreaElement>('[data-prism-conversation-input]')!;
    const submitBtn = root.querySelector<HTMLButtonElement>('.submit-btn')!;

    textarea.value = 'Add an ID&V step before the reviewer stage';
    textarea.dispatchEvent(new Event('input'));
    await el.updateComplete;

    await expect(submitBtn.disabled).toBe(false);

    let eventFired = false;
    el.addEventListener('nl-request', (e) => {
      const detail = (e as CustomEvent<{ text: string }>).detail;
      if (detail.text.includes('ID&V')) eventFired = true;
    });

    submitBtn.click();
    await el.updateComplete;
    await expect(eventFired).toBe(true);

    await expect(textarea.value).toBe('');
  },
};
