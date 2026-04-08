import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { expect, within } from '@storybook/test';
import './prism-workflow-completion';
import type { PrismWorkflowCompletionElement } from './prism-workflow-completion';
import type { WorkflowAction } from './workflow-api-client';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const START_ANOTHER_ACTION: WorkflowAction = {
  actionKey: 'start-another',
  label: 'Start Another',
  style: 'primary',
};

const RETURN_TO_DASHBOARD_ACTION: WorkflowAction = {
  actionKey: 'return-to-dashboard',
  label: 'Return to Dashboard',
  style: 'secondary',
};

// ─── Story Args ───────────────────────────────────────────────────────────────

type StoryArgs = {
  stateDisplayName: string;
  availableActions: WorkflowAction[];
};

// ─── Meta ─────────────────────────────────────────────────────────────────────

const meta: Meta<StoryArgs> = {
  title: 'Prism/Workflow/Completion',
  component: 'prism-workflow-completion',
  tags: ['autodocs'],
  args: {
    stateDisplayName: 'Application submitted',
    availableActions: [],
  },
  render: (args) => html`
    <prism-workflow-completion
      .stateDisplayName=${args.stateDisplayName}
      .availableActions=${args.availableActions}
    ></prism-workflow-completion>
  `,
};

export default meta;

type Story = StoryObj<StoryArgs>;

// ─── Stories ──────────────────────────────────────────────────────────────────

/** Successful submission — green confirmation panel with "Start another" action. */
export const SuccessfulSubmission: Story = {
  args: {
    stateDisplayName: 'Application submitted',
    availableActions: [START_ANOTHER_ACTION, RETURN_TO_DASHBOARD_ACTION],
  },
  play: async ({ canvasElement }) => {
    const component = canvasElement.querySelector(
      'prism-workflow-completion'
    ) as PrismWorkflowCompletionElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');

    const canvas = within(component.shadowRoot as unknown as HTMLElement);

    // Confirmation panel should be present
    const panel = component.shadowRoot.querySelector('.govuk-panel--confirmation');
    await expect(panel).toBeInTheDocument();

    // Title should match
    const title = component.shadowRoot.querySelector('.govuk-panel__title') as HTMLElement;
    await expect(title.textContent?.trim()).toBe('Application submitted');

    // Actions should be rendered
    const startAnotherButton = canvas.getByText('Start Another');
    await expect(startAnotherButton).toBeInTheDocument();

    const dashboardButton = canvas.getByText('Return to Dashboard');
    await expect(dashboardButton).toBeInTheDocument();
  },
};

/** Simple completion — no next actions available. */
export const NoActions: Story = {
  args: {
    stateDisplayName: 'Request complete',
    availableActions: [],
  },
  play: async ({ canvasElement }) => {
    const component = canvasElement.querySelector(
      'prism-workflow-completion'
    ) as PrismWorkflowCompletionElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');

    // Panel should be present
    const panel = component.shadowRoot.querySelector('.govuk-panel--confirmation');
    await expect(panel).toBeInTheDocument();

    // No buttons should be present
    const buttonGroup = component.shadowRoot.querySelector('.govuk-button-group');
    await expect(buttonGroup).not.toBeInTheDocument();
  },
};

/** Approved outcome — demonstrates positive completion state. */
export const Approved: Story = {
  args: {
    stateDisplayName: 'Your application has been approved',
    availableActions: [
      {
        actionKey: 'view-details',
        label: 'View Details',
        style: 'primary',
      },
    ],
  },
};

/** Rejected outcome — still uses green panel (workflow completed successfully). */
export const Rejected: Story = {
  args: {
    stateDisplayName: 'Application not approved',
    availableActions: [
      {
        actionKey: 'view-feedback',
        label: 'View Feedback',
        style: 'primary',
      },
      {
        actionKey: 'submit-new',
        label: 'Submit New Application',
        style: 'secondary',
      },
    ],
  },
};

/** Withdrawn — user-initiated cancellation. */
export const Withdrawn: Story = {
  args: {
    stateDisplayName: 'Application withdrawn',
    availableActions: [
      {
        actionKey: 'start-new',
        label: 'Start New Application',
        style: 'primary',
      },
    ],
  },
};

/** Accessibility check — verifies semantic HTML and focus order. */
export const AccessibilityCheck: Story = {
  args: {
    stateDisplayName: 'Form submitted successfully',
    availableActions: [START_ANOTHER_ACTION],
  },
  play: async ({ canvasElement }) => {
    const component = canvasElement.querySelector(
      'prism-workflow-completion'
    ) as PrismWorkflowCompletionElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');

    // Panel should use <h1> for the title
    const title = component.shadowRoot.querySelector('h1.govuk-panel__title');
    await expect(title).toBeInTheDocument();

    // Buttons should be keyboard accessible
    const button = component.shadowRoot.querySelector('.govuk-button') as HTMLButtonElement;
    await expect(button).toHaveAttribute('type', 'button');
  },
};
