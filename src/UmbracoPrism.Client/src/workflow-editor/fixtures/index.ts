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
  queues: [{ key: 'applicant', displayName: 'Applicant', actor: 'applicant', queueName: 'web-user', roleGates: [] }],
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
        queueKey: 'applicant',
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
        queueKey: 'applicant',
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
        queueKey: 'applicant',
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
        queueKey: 'applicant',
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
    gateways: [
      { key: 'route-application-form', displayName: 'Route to application form', gatewayType: 'Split', queueKey:'applicant', actor: 'applicant', roleGates: [] },
      { key: 'route-check-answers', displayName: 'Route to check answers', gatewayType: 'Split', queueKey:'applicant', actor: 'applicant', roleGates: [] },
      { key: 'route-submitted', displayName: 'Route to submitted', gatewayType: 'Split', queueKey:'applicant', actor: 'applicant', roleGates: [] },
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
  queues: [
    { key: 'applicant', displayName: 'Applicant', actor: 'applicant', queueName: 'web-user', roleGates: [] },
    { key: 'reviewer', displayName: 'Reviewer', actor: 'reviewer', queueName: 'business-user', roleGates: ['reviewer'] },
  ],
  states: [
    { stateKey: 'start-request', displayName: 'Start request', components: [], metadata: { description: 'Collect the request details before the service branches into review work.', stageType: 'Question', actor: 'applicant', queueKey:'applicant', actions: [], roleGates: [] } },
    { stateKey: 'applicant-amendments', displayName: 'Applicant amendments', components: [], metadata: { description: 'Applicant updates the request when more detail is needed.', stageType: 'Question', actor: 'applicant', queueKey:'applicant', actions: [], roleGates: [] } },
    { stateKey: 'upload-evidence', displayName: 'Upload evidence', components: [], metadata: { description: 'Applicant provides the supporting documents for the request.', stageType: 'Question', actor: 'applicant', queueKey:'applicant', actions: [], roleGates: [] } },
    { stateKey: 'reviewer-assessment', displayName: 'Reviewer assessment', components: [], metadata: { description: 'Reviewer checks the request before the service can continue.', stageType: 'Question', actor: 'reviewer', queueKey:'reviewer', actions: [], roleGates: ['reviewer'] } },
    { stateKey: 'decision-confirmed', displayName: 'Decision confirmed', components: [], metadata: { description: 'The shared path continues here once every branch is complete.', stageType: 'Confirmation', actor: 'applicant', queueKey:'applicant', actions: [], roleGates: [] } },
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
    gateways: [
      { key: 'review-split', displayName: 'Review split', description: 'Branch the request into the next pieces of work.', gatewayType: 'Split', queueKey:'applicant', actor: 'applicant', roleGates: [] },
      { key: 'decision-join', displayName: 'Decision join', description: 'Wait for every branch to complete before releasing the next step.', gatewayType: 'Join', queueKey:'applicant', actor: 'applicant', roleGates: [], waitingContent: 'Waiting for amendments, supporting evidence, and reviewer assessment before the decision can continue.', waitingAllowDefer: false, requiredIncomingQueues: ['applicant', 'reviewer'] },
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

/**
 * Community Enquiry workflow — migrated to queues/gateways/routes format.
 * Single-queue (applicant), simple linear flow with one Split gateway.
 */
export const COMMUNITY_ENQUIRY_WORKFLOW: AuthoredWorkflow = hydrateWorkflowDefinition({
  definitionKey: 'community-enquiry',
  displayName: 'Get in Touch',
  version: 1,
  description: 'Simple contact workflow for community enquiries.',
  schemaVersion: '1.0',
  initialStageKey: 'collecting-details',
  instancePolicy: 'single',
  queues: [
    { key: 'applicant', title: 'Applicant', actor: 'applicant', roleGates: [] },
  ],
  gateways: [
    {
      key: 'route-submitted',
      title: 'Route to submitted',
      type: 'Split',
      queueKey:'applicant',
      source: 'collecting-details',
      roleGates: [],
      routes: [
        { id: 'collecting-details--submit--submitted', target: 'submitted', trigger: 'submit', actions: [] },
      ],
    },
  ],
  stages: [
    {
      key: 'collecting-details',
      title: 'Your details',
      type: 'Question',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
    {
      key: 'submitted',
      title: 'Thank you',
      type: 'Confirmation',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
  ],
} as unknown as AuthoredWorkflow);

/**
 * Information Request workflow — migrated to queues/gateways/routes format.
 * Two-queue (applicant + caseworker) with a Split gateway and a Join gateway.
 */
export const INFORMATION_REQUEST_WORKFLOW: AuthoredWorkflow = hydrateWorkflowDefinition({
  definitionKey: 'information-request',
  displayName: 'Information Request',
  version: 1,
  schemaVersion: '1.0',
  initialStageKey: 'collecting-info',
  instancePolicy: 'single',
  queues: [
    { key: 'applicant', title: 'Applicant', actor: 'applicant', roleGates: [] },
    { key: 'caseworker', title: 'Caseworker', actor: 'caseworker', roleGates: [] },
  ],
  gateways: [
    {
      key: 'request-submitted',
      title: 'Request submitted',
      type: 'Split',
      queueKey:'applicant',
      source: 'collecting-info',
      roleGates: [],
      routes: [
        { id: 'collecting-info--submit--review-complete', target: 'review-complete', trigger: 'submit', actions: [] },
        { id: 'collecting-info--submit--caseworker-review', target: 'caseworker-review', trigger: 'submit', actions: [] },
      ],
    },
    {
      key: 'caseworker-route',
      title: 'Route from caseworker review',
      type: 'Split',
      queueKey:'caseworker',
      source: 'caseworker-review',
      roleGates: [],
      routes: [
        { id: 'caseworker-review--complete-review--review-complete', target: 'review-complete', trigger: 'complete-review', actions: [] },
      ],
    },
    {
      key: 'review-complete',
      title: 'Review complete',
      type: 'Join',
      queueKey:'applicant',
      roleGates: [],
      waitingInfo: {
        content: 'We\'ve received your submission and it\'s currently being reviewed.',
        expectedWaitSeconds: 30,
        pollIntervalMs: 5000,
        allowDefer: false,
      },
      requiredIncomingQueues: ['applicant', 'caseworker'],
      routes: [
        { id: 'review-complete--release--complete', target: 'complete', trigger: 'release', actions: [] },
      ],
    },
  ],
  stages: [
    {
      key: 'collecting-info',
      title: 'Tell us about yourself',
      type: 'Question',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
    {
      key: 'caseworker-review',
      title: 'Caseworker review',
      description: 'Caseworker confirms the review outcome before the applicant sees the final status.',
      type: 'Question',
      queueKey:'caseworker',
      actions: [],
      roleGates: [],
      components: [],
    },
    {
      key: 'complete',
      title: 'Request Complete',
      type: 'Confirmation',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
  ],
} as unknown as AuthoredWorkflow);

/**
 * Planning Application workflow — migrated to queues/gateways/routes format.
 * Single-queue (applicant), linear flow through declaration → form → check → submitted.
 */
export const PLANNING_WORKFLOW_MIGRATED: AuthoredWorkflow = hydrateWorkflowDefinition({
  definitionKey: 'planning-application',
  displayName: 'Planning Application',
  version: 1,
  description: 'Standard planning application workflow for submitting and tracking planning permission requests.',
  schemaVersion: '1.0',
  initialStageKey: 'declaration',
  instancePolicy: 'single',
  queues: [
    { key: 'applicant', title: 'Applicant', actor: 'applicant', roleGates: [] },
  ],
  gateways: [
    {
      key: 'route-application-form',
      title: 'Route to application form',
      type: 'Split',
      queueKey:'applicant',
      source: 'declaration',
      roleGates: [],
      routes: [
        { id: 'declaration--continue--application-form', target: 'application-form', trigger: 'continue', actions: [] },
      ],
    },
    {
      key: 'route-check-answers',
      title: 'Route to check answers',
      type: 'Split',
      queueKey:'applicant',
      source: 'application-form',
      roleGates: [],
      routes: [
        { id: 'application-form--continue--check-answers', target: 'check-answers', trigger: 'continue', actions: [] },
      ],
    },
    {
      key: 'route-submitted',
      title: 'Route to submitted',
      type: 'Split',
      queueKey:'applicant',
      source: 'check-answers',
      roleGates: [],
      routes: [
        { id: 'check-answers--submit--submitted', target: 'submitted', trigger: 'submit', actions: [] },
      ],
    },
  ],
  stages: [
    {
      key: 'declaration',
      title: 'Declaration',
      description: 'Collects applicant and site identity before the full planning form.',
      type: 'Question',
      actor: 'applicant',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
    {
      key: 'application-form',
      title: 'Application Form',
      description: 'Captures the substantive planning request.',
      type: 'Question',
      actor: 'applicant',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
    {
      key: 'check-answers',
      title: 'Check your answers',
      description: 'Summarises captured answers before final submission.',
      type: 'CheckAnswers',
      actor: 'applicant',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
    {
      key: 'submitted',
      title: 'Application submitted',
      description: 'Confirms receipt and moves the case into reviewer handling.',
      type: 'Confirmation',
      actor: 'applicant',
      queueKey:'applicant',
      actions: [],
      roleGates: [],
      components: [],
    },
  ],
} as unknown as AuthoredWorkflow);
