import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { expect } from '@storybook/test';
import './prism-workflow-collect';
import type { PrismWorkflowCollectElement } from './prism-workflow-collect';
import type {
  FieldGroupRenderPayload,
  WorkflowAction,
  WorkflowProblem,
} from './workflow-api-client';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const PERSONAL_DETAILS_GROUP: FieldGroupRenderPayload = {
  groupKey: 'personal-details',
  displayName: 'Personal Details',
  fields: [
    {
      fieldKey: 'fullName',
      label: 'Full name',
      hint: 'Enter your full legal name as it appears on your ID',
      fieldType: 'text',
      required: true,
      value: '',
      options: null,
    },
    {
      fieldKey: 'email',
      label: 'Email address',
      hint: 'We will use this to send you updates',
      fieldType: 'text',
      required: true,
      value: '',
      options: null,
    },
    {
      fieldKey: 'dateOfBirth',
      label: 'Date of birth',
      hint: '',
      fieldType: 'date',
      required: true,
      value: '',
      options: null,
    },
  ],
};

const EMPLOYMENT_GROUP: FieldGroupRenderPayload = {
  groupKey: 'employment',
  displayName: 'Employment Information',
  fields: [
    {
      fieldKey: 'employmentStatus',
      label: 'Employment status',
      hint: 'Select the option that best describes your current situation',
      fieldType: 'radio',
      required: true,
      value: '',
      options: ['Employed', 'Self-employed', 'Unemployed', 'Retired'],
    },
    {
      fieldKey: 'annualIncome',
      label: 'Annual income',
      hint: 'Your gross income before tax',
      fieldType: 'currency',
      required: false,
      value: '',
      options: null,
    },
  ],
};

const CONSENT_GROUP: FieldGroupRenderPayload = {
  groupKey: 'consent',
  displayName: 'Terms and Conditions',
  fields: [
    {
      fieldKey: 'agreeToTerms',
      label: 'I agree to the terms and conditions',
      hint: '',
      fieldType: 'checkbox',
      required: true,
      value: false,
      options: null,
    },
  ],
};

const PRIMARY_ACTION: WorkflowAction = {
  actionKey: 'submit',
  label: 'Submit',
  style: 'primary',
};

const SAVE_DRAFT_ACTION: WorkflowAction = {
  actionKey: 'save-draft',
  label: 'Save Draft',
  style: 'secondary',
};

const VALIDATION_PROBLEMS: WorkflowProblem[] = [
  {
    fieldKey: 'fullName',
    message: 'Enter your full name',
    code: 'required',
  },
  {
    fieldKey: 'email',
    message: 'Enter a valid email address',
    code: 'invalid_format',
  },
];

// ─── Story Args ───────────────────────────────────────────────────────────────

type StoryArgs = {
  fieldGroups: FieldGroupRenderPayload[];
  availableActions: WorkflowAction[];
  problems: WorkflowProblem[];
};

// ─── Meta ─────────────────────────────────────────────────────────────────────

const meta: Meta<StoryArgs> = {
  title: 'Prism/Workflow/Collect',
  component: 'prism-workflow-collect',
  tags: ['autodocs'],
  args: {
    fieldGroups: [PERSONAL_DETAILS_GROUP],
    availableActions: [PRIMARY_ACTION],
    problems: [],
  },
  render: (args) => html`
    <prism-workflow-collect
      .fieldGroups=${args.fieldGroups}
      .availableActions=${args.availableActions}
      .problems=${args.problems}
    ></prism-workflow-collect>
  `,
};

export default meta;

type Story = StoryObj<StoryArgs>;

// ─── Stories ──────────────────────────────────────────────────────────────────

/** Empty form with no values — the default initial state. */
export const EmptyForm: Story = {
  args: {
    fieldGroups: [PERSONAL_DETAILS_GROUP, EMPLOYMENT_GROUP, CONSENT_GROUP],
    availableActions: [PRIMARY_ACTION, SAVE_DRAFT_ACTION],
    problems: [],
  },
  play: async ({ canvasElement }) => {
    const component = canvasElement.querySelector(
      'prism-workflow-collect'
    ) as PrismWorkflowCollectElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');

    const form = component.shadowRoot.querySelector('form');
    await expect(form).toBeInTheDocument();

    const submitButton = component.shadowRoot.querySelector(
      '[data-action-key="submit"]'
    ) as HTMLButtonElement;
    await expect(submitButton).toBeInTheDocument();
    await expect(submitButton.textContent?.trim()).toBe('Submit');
  },
};

/** Form with validation errors — shows error summary and field-level errors. */
export const WithValidationErrors: Story = {
  args: {
    fieldGroups: [PERSONAL_DETAILS_GROUP],
    availableActions: [PRIMARY_ACTION],
    problems: VALIDATION_PROBLEMS,
  },
  play: async ({ canvasElement }) => {
    const component = canvasElement.querySelector(
      'prism-workflow-collect'
    ) as PrismWorkflowCollectElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');

    // Error summary should be present
    const errorSummary = component.shadowRoot.querySelector('.govuk-error-summary');
    await expect(errorSummary).toBeInTheDocument();
    await expect(errorSummary).toHaveAttribute('role', 'alert');

    // Error summary should list both errors
    const errorLinks = component.shadowRoot.querySelectorAll('.govuk-error-summary__list a');
    await expect(errorLinks).toHaveLength(2);

    // Field-level error messages should be present
    const fullNameError = component.shadowRoot.querySelector('#fullName-error');
    await expect(fullNameError).toBeInTheDocument();

    const emailError = component.shadowRoot.querySelector('#email-error');
    await expect(emailError).toBeInTheDocument();

    // Fields should have aria-invalid
    const fullNameInput = component.shadowRoot.querySelector('#field-fullName') as HTMLInputElement;
    await expect(fullNameInput).toHaveAttribute('aria-invalid', 'true');
  },
};

/** Form with values pre-populated — demonstrates field value binding. */
export const FilledOut: Story = {
  args: {
    fieldGroups: [
      {
        ...PERSONAL_DETAILS_GROUP,
        fields: PERSONAL_DETAILS_GROUP.fields.map((field) => ({
          ...field,
          value:
            field.fieldKey === 'fullName'
              ? 'Jane Smith'
              : field.fieldKey === 'email'
                ? 'jane.smith@example.com'
                : field.fieldKey === 'dateOfBirth'
                  ? '1990-05-15'
                  : '',
        })),
      },
      {
        ...EMPLOYMENT_GROUP,
        fields: EMPLOYMENT_GROUP.fields.map((field) => ({
          ...field,
          value:
            field.fieldKey === 'employmentStatus'
              ? 'Employed'
              : field.fieldKey === 'annualIncome'
                ? '45000'
                : '',
        })),
      },
      {
        ...CONSENT_GROUP,
        fields: CONSENT_GROUP.fields.map((field) => ({
          ...field,
          value: field.fieldKey === 'agreeToTerms' ? true : false,
        })),
      },
    ],
    availableActions: [PRIMARY_ACTION, SAVE_DRAFT_ACTION],
    problems: [],
  },
  play: async ({ canvasElement }) => {
    const component = canvasElement.querySelector(
      'prism-workflow-collect'
    ) as PrismWorkflowCollectElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');

    // Verify text input has value
    const nameInput = component.shadowRoot.querySelector('#field-fullName') as HTMLInputElement;
    await expect(nameInput.value).toBe('Jane Smith');

    // Verify radio is selected
    const radioInput = component.shadowRoot.querySelector(
      '#field-employmentStatus-Employed'
    ) as HTMLInputElement;
    await expect(radioInput.checked).toBe(true);

    // Verify checkbox is checked
    const checkbox = component.shadowRoot.querySelector(
      '#field-agreeToTerms'
    ) as HTMLInputElement;
    await expect(checkbox.checked).toBe(true);
  },
};

/** All field types — demonstrates the full range of supported inputs. */
export const AllFieldTypes: Story = {
  args: {
    fieldGroups: [
      {
        groupKey: 'all-types',
        displayName: 'All Field Types',
        fields: [
          {
            fieldKey: 'textField',
            label: 'Text field',
            hint: 'A simple text input',
            fieldType: 'text',
            required: false,
            value: '',
            options: null,
          },
          {
            fieldKey: 'numberField',
            label: 'Number field',
            hint: 'Numeric input only',
            fieldType: 'number',
            required: false,
            value: '',
            options: null,
          },
          {
            fieldKey: 'dateField',
            label: 'Date field',
            hint: 'Pick a date',
            fieldType: 'date',
            required: false,
            value: '',
            options: null,
          },
          {
            fieldKey: 'currencyField',
            label: 'Currency field',
            hint: 'Amount in GBP',
            fieldType: 'currency',
            required: false,
            value: '',
            options: null,
          },
          {
            fieldKey: 'textareaField',
            label: 'Textarea',
            hint: 'Multi-line text input',
            fieldType: 'textarea',
            required: false,
            value: '',
            options: null,
          },
          {
            fieldKey: 'selectField',
            label: 'Select dropdown',
            hint: 'Choose one option',
            fieldType: 'select',
            required: false,
            value: '',
            options: ['Option 1', 'Option 2', 'Option 3'],
          },
          {
            fieldKey: 'radioField',
            label: 'Radio buttons',
            hint: 'Select one',
            fieldType: 'radio',
            required: false,
            value: '',
            options: ['Yes', 'No', 'Maybe'],
          },
          {
            fieldKey: 'checkboxField',
            label: 'I agree to something',
            hint: '',
            fieldType: 'checkbox',
            required: false,
            value: false,
            options: null,
          },
        ],
      },
    ],
    availableActions: [PRIMARY_ACTION],
    problems: [],
  },
};

/** Accessibility check — verifies ARIA attributes and keyboard navigation. */
export const AccessibilityCheck: Story = {
  args: {
    fieldGroups: [PERSONAL_DETAILS_GROUP],
    availableActions: [PRIMARY_ACTION],
    problems: VALIDATION_PROBLEMS,
  },
  play: async ({ canvasElement }) => {
    const component = canvasElement.querySelector(
      'prism-workflow-collect'
    ) as PrismWorkflowCollectElement;
    await component.updateComplete;

    if (!component.shadowRoot) throw new Error('Shadow root not found');

    // Error summary should be focusable and have role="alert"
    const errorSummary = component.shadowRoot.querySelector(
      '.govuk-error-summary'
    ) as HTMLElement;
    await expect(errorSummary).toHaveAttribute('role', 'alert');
    await expect(errorSummary).toHaveAttribute('tabindex', '-1');

    // Fields with errors should have aria-invalid and aria-describedby
    const fullNameInput = component.shadowRoot.querySelector('#field-fullName') as HTMLInputElement;
    await expect(fullNameInput).toHaveAttribute('aria-invalid', 'true');
    await expect(fullNameInput).toHaveAttribute('aria-describedby');

    const describedBy = fullNameInput.getAttribute('aria-describedby');
    await expect(describedBy).toContain('fullName-hint');
    await expect(describedBy).toContain('fullName-error');

    // Submit button should be keyboard accessible
    const submitButton = component.shadowRoot.querySelector(
      '[data-action-key="submit"]'
    ) as HTMLButtonElement;
    await expect(submitButton).toHaveAttribute('type', 'submit');
  },
};
