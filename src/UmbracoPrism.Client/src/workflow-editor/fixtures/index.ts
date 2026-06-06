import type { AuthoredInputComponent, AuthoredWorkflow } from '../types.js';
import { hydrateWorkflowDefinition } from '../types.js';

export function cloneAuthoredWorkflow<T extends AuthoredWorkflow>(workflow: T): T {
  return hydrateWorkflowDefinition(JSON.parse(JSON.stringify(workflow)) as T);
}

export const PLANNING_WORKFLOW: AuthoredWorkflow = hydrateWorkflowDefinition({
  definitionKey: 'planning-application',
  displayName: 'Planning Application',
  version: 1,
  initialState: 'declaration',
  instancePolicy: 'single',
  states: [
    {
      stateKey: 'declaration',
      displayName: 'Declaration',
      components: [{
        type: 'fieldset',
        legend: 'Declaration',
        legendSize: 'm',
        children: [
          {
            type: 'text',
            fieldKey: 'applicant-name',
            label: 'Applicant name',
            required: true,
            hint: 'Enter the full name of the person or organisation applying.',
          } satisfies AuthoredInputComponent,
          {
            type: 'textarea',
            fieldKey: 'site-address',
            label: 'Site address',
            required: true,
            hint: 'Enter the full address of the site where development is proposed.',
          } satisfies AuthoredInputComponent,
        ],
      }],
      metadata: {
        description: 'Collects applicant and site identity before the full planning form.',
        stageType: 'Question',
        actor: 'applicant',
        laneKey: 'applicant',
        actions: [{
          type: 'forms.load',
          timing: 'OnEntry',
          parameterSchemaKey: 'forms-form-definition',
          params: { formDefinitionId: 'planning-declaration' },
          summary: 'Load the declaration form.',
        }],
        roleGates: [],
        editorComment: 'Entry point — collects basic applicant and site identity.',
      },
    },
    {
      stateKey: 'application-form',
      displayName: 'Application Form',
      components: [{
        type: 'fieldset',
        legend: 'Application Form',
        legendSize: 'm',
        children: [
          {
            type: 'textarea',
            fieldKey: 'description',
            label: 'Description of proposed works',
            required: true,
            hint: 'Provide a clear description of the development you are proposing.',
          } satisfies AuthoredInputComponent,
          {
            type: 'select',
            fieldKey: 'development-type',
            label: 'Type of development',
            required: true,
            options: ['New build', 'Extension', 'Change of use', 'Demolition', 'Other'],
          } satisfies AuthoredInputComponent,
        ],
      }],
      metadata: {
        description: 'Captures the substantive planning request.',
        stageType: 'Question',
        actor: 'applicant',
        laneKey: 'applicant',
        actions: [{
          type: 'forms.save',
          timing: 'OnExit',
          parameterSchemaKey: 'forms-form-definition',
          params: { formDefinitionId: 'planning-application' },
          summary: 'Persist the application form before moving on.',
        }],
        roleGates: [],
      },
    },
    {
      stateKey: 'check-answers',
      displayName: 'Check your answers',
      components: [],
      metadata: {
        description: 'Summarises captured answers before final submission.',
        stageType: 'CheckAnswers',
        actor: 'applicant',
        laneKey: 'applicant',
        actions: [],
        roleGates: [],
        editorComment: 'Summary of all answers before final submission.',
      },
    },
    {
      stateKey: 'submitted',
      displayName: 'Application submitted',
      components: [],
      metadata: {
        description: 'Confirms receipt and moves the case into reviewer handling.',
        stageType: 'Confirmation',
        actor: 'applicant',
        laneKey: 'applicant',
        actions: [],
        roleGates: [],
      },
    },
  ],
  transitions: [
    { fromState: 'declaration', toState: 'route-application-form', action: 'route' },
    { fromState: 'route-application-form', toState: 'application-form', action: 'continue' },
    { fromState: 'application-form', toState: 'route-check-answers', action: 'route' },
    { fromState: 'route-check-answers', toState: 'check-answers', action: 'continue' },
    {
      fromState: 'check-answers',
      toState: 'route-submitted',
      action: 'route',
    },
    {
      fromState: 'route-submitted',
      toState: 'submitted',
      action: 'submit',
      metadata: {
        conditions: [{ kind: 'expression', expression: 'application.isComplete == true', description: 'Prevent submission until the applicant has completed the form.' }],
        actions: [{
          type: 'forms.submit',
          timing: 'OnTransition',
          parameterSchemaKey: 'forms-form-definition',
          params: { formDefinitionId: 'planning-application' },
          summary: 'Submit the application form to the business app.',
        }],
      },
    },
  ],
  metadata: {
    description: 'Standard planning application workflow for submitting and tracking planning permission requests.',
    schemaVersion: '1.0',
    lanes: [{ key: 'applicant', displayName: 'Applicant', actor: 'applicant', queueName: 'web-user', roleGates: [] }],
    gateways: [
      { key: 'route-application-form', displayName: 'Route to application form', gatewayType: 'Split', laneKey: 'applicant', actor: 'applicant', roleGates: [] },
      { key: 'route-check-answers', displayName: 'Route to check answers', gatewayType: 'Split', laneKey: 'applicant', actor: 'applicant', roleGates: [] },
      { key: 'route-submitted', displayName: 'Route to submitted', gatewayType: 'Split', laneKey: 'applicant', actor: 'applicant', roleGates: [] },
    ],
    handoffs: [{
      id: 'applicant-to-caseworker',
      fromState: 'check-answers',
      toState: 'submitted',
      label: 'applicant-to-caseworker',
      actorChange: 'caseworker',
    }],
  },
  parameterSchemas: [{
    key: 'forms-form-definition',
    title: 'Forms engine definition reference',
    description: 'Shared parameter contract for load/save/submit form actions.',
    appliesTo: ['forms.load', 'forms.save', 'forms.submit'],
    valueKind: 'Object',
    allowAdditionalProperties: false,
    properties: [{
      key: 'formDefinitionId',
      title: 'Form definition id',
      description: 'Stable forms-engine key to load or persist.',
      valueKind: 'String',
      editor: 'text',
    }],
    required: ['formDefinitionId'],
  }],
});

export const LEAVE_REQUEST_STARTER_WORKFLOW: AuthoredWorkflow = hydrateWorkflowDefinition({
  definitionKey: 'leave-request',
  displayName: 'Leave Request',
  version: 1,
  initialState: 'start-request',
  instancePolicy: 'multiple',
  states: [
    { stateKey: 'start-request', displayName: 'Start request', components: [], metadata: { description: 'Collect the request details before the service branches into review work.', stageType: 'Question', actor: 'applicant', laneKey: 'applicant', actions: [], roleGates: [] } },
    { stateKey: 'applicant-amendments', displayName: 'Applicant amendments', components: [], metadata: { description: 'Applicant updates the request when more detail is needed.', stageType: 'Question', actor: 'applicant', laneKey: 'applicant', actions: [], roleGates: [] } },
    { stateKey: 'upload-evidence', displayName: 'Upload evidence', components: [], metadata: { description: 'Applicant provides the supporting documents for the request.', stageType: 'Question', actor: 'applicant', laneKey: 'applicant', actions: [], roleGates: [] } },
    { stateKey: 'reviewer-assessment', displayName: 'Reviewer assessment', components: [], metadata: { description: 'Reviewer checks the request before the service can continue.', stageType: 'Question', actor: 'reviewer', laneKey: 'reviewer', actions: [], roleGates: ['reviewer'] } },
    { stateKey: 'decision-confirmed', displayName: 'Decision confirmed', components: [], metadata: { description: 'The shared path continues here once every branch is complete.', stageType: 'Confirmation', actor: 'applicant', laneKey: 'applicant', actions: [], roleGates: [] } },
  ],
  transitions: [
    { fromState: 'start-request', toState: 'review-split', action: 'route' },
    { fromState: 'review-split', toState: 'applicant-amendments', action: 'request amendments' },
    { fromState: 'review-split', toState: 'upload-evidence', action: 'upload evidence' },
    { fromState: 'review-split', toState: 'reviewer-assessment', action: 'send to reviewer', requiresRole: 'reviewer' },
    { fromState: 'applicant-amendments', toState: 'decision-join', action: 'finish amendments' },
    { fromState: 'upload-evidence', toState: 'decision-join', action: 'evidence complete' },
    { fromState: 'reviewer-assessment', toState: 'decision-join', action: 'confirm review', requiresRole: 'reviewer' },
    { fromState: 'decision-join', toState: 'decision-confirmed', action: 'continue' },
  ],
  metadata: {
    schemaVersion: '1.0',
    lanes: [
      { key: 'applicant', displayName: 'Applicant', actor: 'applicant', queueName: 'web-user', roleGates: [] },
      { key: 'reviewer', displayName: 'Reviewer', actor: 'reviewer', queueName: 'business-user', roleGates: ['reviewer'] },
    ],
    gateways: [
      { key: 'review-split', displayName: 'Review split', description: 'Branch the request into the next pieces of work.', gatewayType: 'Split', laneKey: 'applicant', actor: 'applicant', roleGates: [] },
      { key: 'decision-join', displayName: 'Decision join', description: 'Wait for every branch to complete before releasing the next step.', gatewayType: 'Join', laneKey: 'applicant', actor: 'applicant', roleGates: [], waitingContent: 'Waiting for amendments, supporting evidence, and reviewer assessment before the decision can continue.', waitingAllowDefer: false, requiredIncomingLanes: ['applicant', 'reviewer'] },
    ],
  },
});

export const PAYMENT_DEMO_WORKFLOW: AuthoredWorkflow = hydrateWorkflowDefinition({
  definitionKey: 'payment-demo',
  displayName: 'Payment Demo',
  version: 1,
  initialState: 'enter-details',
  instancePolicy: 'single',
  description: 'Payment flow showing the web queue handing off to the business queue before completion.',
  schemaVersion: '1.0',
  queues: [
    { key: 'web-user', displayName: 'Applicant', actor: 'applicant' },
    { key: 'business-user', displayName: 'Payments team', actor: 'reviewer', roleGates: ['reviewer'] },
  ],
  states: [
    {
      stateKey: 'enter-details',
      displayName: 'Enter payment details',
      components: [{
        type: 'fieldset',
        legend: 'Enter Payment Details',
        children: [
          { type: 'text', fieldKey: 'cardholderName', label: 'Cardholder name', required: true } satisfies AuthoredInputComponent,
          { type: 'decimal', fieldKey: 'amount', label: 'Amount (£)', required: true } satisfies AuthoredInputComponent,
        ],
      }],
      kind: 'Question',
      actor: 'applicant',
      queueKey: 'web-user',
      actions: [],
      roleGates: [],
      routes: [
        { id: 'enter-details--submit--submit-payment', target: 'submit-payment', trigger: 'submit', actions: [] },
      ],
    },
    {
      stateKey: 'confirm-payment-received',
      displayName: 'Confirm payment received',
      components: [],
      description: 'Back-office confirmation step for reconciling the payment before the applicant is released.',
      kind: 'Question',
      actor: 'reviewer',
      queueKey: 'business-user',
      actions: [],
      roleGates: ['reviewer'],
      routes: [
        {
          id: 'confirm-payment-received--confirm--await-payment-confirmation',
          target: 'await-payment-confirmation',
          trigger: 'confirm',
          requiresRole: 'reviewer',
          actions: [],
        },
      ],
    },
    {
      stateKey: 'payment-complete',
      displayName: 'Payment complete',
      components: [],
      description: 'Payment received. A receipt has been sent to your email address.',
      kind: 'Confirmation',
      actor: 'applicant',
      queueKey: 'web-user',
      actions: [],
      roleGates: [],
      routes: [],
    },
  ],
  gateways: [
    {
      key: 'submit-payment',
      displayName: 'Submit payment → notify back-office',
      gatewayType: 'Split',
      kind: 'Split',
      queueKey: 'web-user',
      actor: 'applicant',
      roleGates: [],
      routes: [
        { id: 'submit-payment--submit--await-payment-confirmation', target: 'await-payment-confirmation', trigger: 'submit', actions: [] },
        { id: 'submit-payment--submit--confirm-payment-received', target: 'confirm-payment-received', trigger: 'submit', actions: [] },
      ],
    },
    {
      key: 'await-payment-confirmation',
      displayName: 'Awaiting payment confirmation',
      gatewayType: 'Join',
      kind: 'Join',
      queueKey: 'web-user',
      actor: 'applicant',
      roleGates: [],
      waitingContent: 'We are waiting for the payments team to confirm receipt of your payment.',
      waitingExpectedSeconds: 60,
      waitingPollIntervalMs: 5000,
      waitingAllowDefer: true,
      waitingDeferMessage: 'You can leave this page and return later. We will update this payment as soon as the confirmation arrives.',
      requiredIncomingQueues: ['web-user', 'business-user'],
      routes: [
        { id: 'await-payment-confirmation--release--payment-complete', target: 'payment-complete', trigger: 'release', actions: [] },
      ],
    },
  ],
});
