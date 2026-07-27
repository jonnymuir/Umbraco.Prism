import type { AuthoredInputComponent, AuthoredServiceBlueprint } from '../types.js';
import { hydrateServiceBlueprintDefinition } from '../types.js';

export function cloneAuthoredServiceBlueprint<T extends AuthoredServiceBlueprint>(serviceBlueprint: T): T {
  return hydrateServiceBlueprintDefinition(JSON.parse(JSON.stringify(serviceBlueprint)) as T);
}

export const PLANNING_SERVICE_BLUEPRINT: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition({
  definitionKey: 'planning-application',
  displayName: 'Planning Application',
  version: 1,
  initialStage: 'declaration',
  requestPolicy: 'single',
  queues: [{ key: 'applicant', displayName: 'Applicant', actor: 'applicant', queueName: 'web-user', roleGates: [] }],
  stages: [
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
    description: 'Standard planning application serviceBlueprint for submitting and tracking planning permission requests.',
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

export const LEAVE_REQUEST_STARTER_SERVICE_BLUEPRINT: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition({
  definitionKey: 'leave-request',
  displayName: 'Leave Request',
  version: 1,
  initialStage: 'start-request',
  requestPolicy: 'multiple',
  queues: [
    { key: 'applicant', displayName: 'Applicant', actor: 'applicant', queueName: 'web-user', roleGates: [] },
    { key: 'reviewer', displayName: 'Reviewer', actor: 'reviewer', queueName: 'business-user', roleGates: ['reviewer'] },
  ],
  stages: [
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

export const PAYMENT_DEMO_SERVICE_BLUEPRINT: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition({
  definitionKey: 'payment-demo',
  displayName: 'Payment Demo',
  version: 1,
  initialStage: 'enter-details',
  requestPolicy: 'single',
  description: 'Payment flow showing the web queue handing off to the business queue before completion.',
  schemaVersion: '1.0',
  queues: [
    { key: 'web-user', displayName: 'Applicant', actor: 'applicant' },
    { key: 'business-user', displayName: 'Payments team', actor: 'reviewer', roleGates: ['reviewer'] },
  ],
  stages: [
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
 * Community Enquiry serviceBlueprint — migrated to queues/gateways/routes format.
 * Single-queue (applicant), simple linear flow with one Split gateway.
 */
export const COMMUNITY_ENQUIRY_SERVICE_BLUEPRINT: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition({
  definitionKey: 'community-enquiry',
  displayName: 'Get in Touch',
  version: 1,
  description: 'Simple contact serviceBlueprint for community enquiries.',
  schemaVersion: '1.0',
  initialStageKey: 'collecting-details',
  requestPolicy: 'single',
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
} as unknown as AuthoredServiceBlueprint);

/**
 * Information Request serviceBlueprint — migrated to queues/gateways/routes format.
 * Two-queue (applicant + caseworker) with a Split gateway and a Join gateway.
 */
export const INFORMATION_REQUEST_SERVICE_BLUEPRINT: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition({
  definitionKey: 'information-request',
  displayName: 'Information Request',
  version: 1,
  schemaVersion: '1.0',
  initialStageKey: 'collecting-info',
  requestPolicy: 'single',
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
} as unknown as AuthoredServiceBlueprint);

/**
 * Planning Application serviceBlueprint — migrated to queues/gateways/routes format.
 * Single-queue (applicant), linear flow through declaration → form → check → submitted.
 */
export const PLANNING_SERVICE_BLUEPRINT_MIGRATED: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition({
  definitionKey: 'planning-application',
  displayName: 'Planning Application',
  version: 1,
  description: 'Standard planning application serviceBlueprint for submitting and tracking planning permission requests.',
  schemaVersion: '1.0',
  initialStageKey: 'declaration',
  requestPolicy: 'single',
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
} as unknown as AuthoredServiceBlueprint);

/**
 * Money Modeller — the fully declarative pension modeller demo (see
 * UmbracoPrism.MockBusinessApp/serviceBlueprint-seeds/money-modeller.json, mirrored
 * here rather than imported so the fixture stays a plain TS literal like the
 * rest of this file). Two queues, a calculations block driving live
 * stat-group/chart components, a recalculate self-loop, and a fan-out to a
 * back-office review queue — the most structurally complex real serviceBlueprint
 * this repo ships, and the one that originally surfaced the graph canvas's
 * chip-collision and edge-routing issues.
 */
export const MONEY_MODELLER_SERVICE_BLUEPRINT: AuthoredServiceBlueprint = hydrateServiceBlueprintDefinition({
  definitionKey: 'money-modeller',
  displayName: 'Money Modeller',
  version: 1,
  description: 'Interactive pension benefit modeller: model retirement scenarios from your record or a formal quote, then hand a chosen scenario to the scheme administrators as a quote request.',
  schemaVersion: '1.0',
  initialStage: 'choose-start',
  requestPolicy: 'single',
  calculations: {
    tables: {
      pensionAgeFactor: { interpolate: 'linear', values: { 55: 0.56, 66: 1.0, 75: 1.27 } },
      lumpAgeFactor: { interpolate: 'linear', values: { 55: 0.725, 66: 1.0, 75: 1.0 } },
    },
    fields: {
      member: { source: 'service' },
      quoteMode: { expr: 'qPension > 0' },
      todaysMoney: { expr: "moneyBasis <> 'Future money'" },
      npa: { expr: '66' },
      statePensionAge: { expr: '68' },
      minRetireAge: { expr: 'max(55, member.age + 1)' },
      maxRetireAge: { expr: '75' },
      retireAgeEff: { expr: 'clamp(if(quoteMode, qAge, retireAge), minRetireAge, maxRetireAge)' },
      hasDc: { expr: 'if(quoteMode, qDC > 0, member.dcPot > 0 or (member.active and member.salary > 74208))' },
      years: { expr: 'max(0, retireAgeEff - member.age)' },
      realGrowth: { expr: '(salaryGrowth - inflation) / 100' },
      realReturn: { expr: '(invReturn - inflation) / 100' },
      cappedSalary: { expr: 'min(member.salary, 74208)' },
      futurePension: { expr: 'if(member.active and not quoteMode, years * (cappedSalary / 75) * pow(1 + max(realGrowth, -0.05), years / 2), 0)' },
      basePension: { expr: 'if(quoteMode, qPension, member.accruedPension + futurePension)' },
      baseLump: { expr: 'if(quoteMode, qLump, member.accruedLump + 3 * futurePension)' },
      annualDc: { expr: 'if(member.active and not quoteMode, max(0, member.salary - 74208) * 0.2, 0)' },
      growthFactor: { expr: 'pow(1 + realReturn, years)' },
      newDcSavings: { expr: 'if(abs(realReturn) > 0.0001, annualDc * ((growthFactor - 1) / realReturn), annualDc * years)' },
      pot: { expr: 'if(quoteMode, qDC, member.dcPot * growthFactor + newDcSavings)' },
      pensionFactor: { expr: 'if(quoteMode, 1, lookup(pensionAgeFactor, retireAgeEff))' },
      lumpFactor: { expr: 'if(quoteMode, 1, lookup(lumpAgeFactor, retireAgeEff))' },
      moneyFactor: { expr: 'if(todaysMoney, 1, pow(1 + inflation / 100, years))' },
      adjPension: { expr: 'basePension * pensionFactor * moneyFactor' },
      adjLump: { expr: 'baseLump * lumpFactor * moneyFactor' },
      adjPot: { expr: 'pot * moneyFactor' },
      totalValue: { expr: '20 * adjPension + adjLump + adjPot' },
      maxTfc: { expr: '0.25 * totalValue' },
      extraTfc: { expr: 'max(0, maxTfc - adjLump)' },
      tfcFromDc: { expr: 'min(adjPot, extraTfc)' },
      tfcShortfall: { expr: 'extraTfc - tfcFromDc' },
      pensionOut: { expr: "if(benefitOption = 'Maximum tax-free cash', max(0, adjPension - tfcShortfall / 12), adjPension)" },
      cashOut: { expr: "if(benefitOption = 'Maximum tax-free cash', maxTfc, if(benefitOption = 'Take DC pot as cash', adjLump + adjPot, adjLump))" },
      potOut: { expr: "if(benefitOption = 'Maximum tax-free cash', adjPot - tfcFromDc, if(benefitOption = 'Take DC pot as cash', 0, adjPot))" },
      dcIncomeOut: { expr: 'potOut / 20' },
      statePension: { expr: '11975 * moneyFactor' },
      cashLabel: { expr: "if(benefitOption = 'Take DC pot as cash', 'One-off cash', 'Tax-free cash')" },
      resultPension: { expr: 'round(pensionOut)', format: 'gbp' },
      resultCash: { expr: 'round(cashOut)', format: 'gbp' },
      resultDcIncome: { expr: 'round(dcIncomeOut)', format: 'gbp' },
      resultTotal: { expr: 'round(pensionOut + dcIncomeOut + if(retireAgeEff >= statePensionAge, statePension, 0))', format: 'gbp' },
      memberName: { expr: 'member.name' },
    },
    series: {
      incomeByAge: {
        over: 'age',
        from: 'retireAgeEff',
        to: '90',
        values: {
          db: 'round(pensionOut)',
          dc: 'if(age < retireAgeEff + 20, round(dcIncomeOut), 0)',
          sp: 'if(age >= statePensionAge, round(statePension), 0)',
        },
      },
    },
  },
  queues: [
    { key: 'web-user', displayName: 'Member', description: 'Scheme member exploring retirement scenarios.', actor: 'member' },
    { key: 'business-user', displayName: 'Scheme administrators', description: 'Back-office queue handling formal quote requests.', actor: 'reviewer' },
  ],
  stages: [
    {
      stateKey: 'choose-start',
      displayName: 'Model your money',
      stageType: 'Question',
      actor: 'member',
      queueKey: 'web-user',
      components: [
        { type: 'body', content: "See what your benefits could be worth, explore your options for taking them, and model changes like retiring earlier. You can start from your current pension record, or from the figures on a retirement quote we've sent you." },
        { type: 'inset-text', content: 'Modelling uses your latest Annual Member Statement values and standard scheme assumptions, which you can change at any time.' },
      ],
      routes: [
        { id: 'choose-start--start-modelling--to-model-from-record', target: 'to-model-from-record', trigger: 'start-modelling', label: 'Model with my current record', style: 'primary' },
        { id: 'choose-start--use-quote--to-quote-entry', target: 'to-quote-entry', trigger: 'use-quote', label: 'I have a retirement quote', style: 'secondary' },
      ],
    },
    {
      stateKey: 'enter-quote',
      displayName: 'Enter your retirement quote',
      stageType: 'Question',
      actor: 'member',
      queueKey: 'web-user',
      components: [
        { type: 'body', content: "Copy the figures from the quote we sent you. You'll find them on the first page, under 'Your benefits'." },
        {
          type: 'fieldset',
          legend: 'Your quote figures',
          legendSize: 'm',
          children: [
            { type: 'decimal', fieldKey: 'qPension', label: 'Yearly pension on your quote', hint: 'The yearly pension amount shown on your quote.', prefix: '£', min: 0, required: true, default: '0' },
            { type: 'decimal', fieldKey: 'qLump', label: 'Lump sum on your quote', hint: 'The one-off lump sum shown on your quote.', prefix: '£', min: 0, required: true, default: '0' },
            { type: 'decimal', fieldKey: 'qDC', label: 'DC pot value on your quote', hint: 'Leave blank if your quote has no defined contribution savings.', prefix: '£', min: 0, required: false, default: '0' },
            { type: 'number', fieldKey: 'qAge', label: 'Retirement age on your quote', min: 55, max: 75, required: true, default: '66' },
          ],
        },
      ],
      routes: [
        { id: 'enter-quote--use-quote-figures--to-model-from-quote', target: 'to-model-from-quote', trigger: 'use-quote-figures', label: 'Use these figures', style: 'primary' },
      ],
    },
    {
      stateKey: 'model',
      displayName: 'Your money, modelled',
      stageType: 'Question',
      actor: 'member',
      queueKey: 'web-user',
      components: [
        { type: 'body', content: 'Adjust your retirement age, how you take your benefits, and the assumptions behind the figures. All amounts are estimates before tax.' },
        { type: 'inset-text', showWhen: 'quoteMode', content: "You're modelling with the figures from your retirement quote, so the retirement age and assumptions are fixed to match it." },
        { type: 'slider', showWhen: 'not quoteMode', fieldKey: 'retireAge', label: 'When do you want to retire?', hint: 'Your Normal Pension Age is 66.', min: 55, max: 75, step: 1, default: '66', required: true },
        { type: 'warning-text', showWhen: 'not quoteMode and retireAge < npa', content: "Retiring before 66 reduces your DB pension, because it's paid for longer." },
        {
          type: 'radio',
          fieldKey: 'benefitOption',
          label: 'How do you want to take your benefits?',
          hint: 'You can change your mind any time before you retire.',
          options: ['Standard benefits', 'Maximum tax-free cash', 'Take DC pot as cash'],
          default: 'Standard benefits',
          required: true,
        },
        { type: 'heading', level: 2, content: 'Assumptions', showWhen: 'not quoteMode' },
        { type: 'slider', showWhen: 'not quoteMode', fieldKey: 'inflation', label: 'Inflation (CPI)', min: 0, max: 5, step: 0.5, suffix: '%', default: '2.5', required: false },
        { type: 'slider', showWhen: 'not quoteMode and member.active', fieldKey: 'salaryGrowth', label: 'Yearly salary growth', min: 0, max: 6, step: 0.5, suffix: '%', default: '3', required: false },
        { type: 'slider', showWhen: 'not quoteMode and hasDc', fieldKey: 'invReturn', label: 'Investment return', min: 0, max: 8, step: 0.5, suffix: '%', default: '5', required: false },
        {
          type: 'radio',
          showWhen: 'not quoteMode',
          fieldKey: 'moneyBasis',
          label: 'Show amounts in',
          options: ["Today's money", 'Future money'],
          default: "Today's money",
          required: false,
        },
        {
          type: 'stat-group',
          title: 'Your estimated benefits',
          items: [
            { label: 'DB pension', fieldKey: 'resultPension', qualifier: 'a year, for life', emphasis: true },
            { label: 'Cash', fieldKey: 'resultCash', qualifier: 'one-off payment' },
            { label: 'DC income', fieldKey: 'resultDcIncome', qualifier: 'a year, over 20 years' },
            { label: 'Total income', fieldKey: 'resultTotal', qualifier: 'a year at your chosen age, incl. State Pension from 68', emphasis: true },
          ],
        },
        {
          type: 'chart',
          title: 'Your estimated yearly income by age',
          kind: 'stacked-bar',
          series: 'incomeByAge',
          x: 'age',
          xLabelEvery: 5,
          bands: [
            { key: 'db', label: 'DB pension' },
            { key: 'dc', label: 'DC drawdown' },
            { key: 'sp', label: 'State Pension' },
          ],
        },
        { type: 'inset-text', content: "Figures are estimates for illustration only, based on the assumptions shown, and aren't a promise of what you'll get. Before making decisions, request a formal quote or consider taking financial advice." },
      ],
      routes: [
        { id: 'model--recalculate--recalculate-loop', target: 'recalculate-loop', trigger: 'recalculate', label: 'Recalculate', style: 'secondary' },
        { id: 'model--request-quote--fan-out-quote-request', target: 'fan-out-quote-request', trigger: 'request-quote', label: 'Request a formal quote', style: 'primary' },
      ],
    },
    {
      stateKey: 'quote-requested',
      displayName: 'Quote request sent',
      stageType: 'Confirmation',
      actor: 'member',
      queueKey: 'web-user',
      components: [
        { type: 'panel', heading: 'Your quote request has been sent' },
        { type: 'body', content: 'The scheme administrators will prepare a formal quote for your chosen scenario and send it to you. A formal quote gives you guaranteed figures you can rely on when deciding to retire.' },
      ],
      routes: [],
    },
    {
      stateKey: 'review-quote-request',
      displayName: 'Review quote request',
      description: "Back-office review of a member's modelled scenario before issuing a formal quote.",
      stageType: 'Question',
      actor: 'reviewer',
      queueKey: 'business-user',
      roleGates: ['reviewer'],
      components: [
        { type: 'body', content: 'The member has requested a formal quote for the scenario below. Confirm the figures against the administration system before issuing.' },
        {
          type: 'summary-list',
          title: 'Requested scenario',
          children: [
            { type: 'text', fieldKey: 'memberName', label: 'Member' },
            { type: 'text', fieldKey: 'retireAge', label: 'Retirement age' },
            { type: 'text', fieldKey: 'benefitOption', label: 'Benefit option' },
            { type: 'text', fieldKey: 'resultPension', label: 'Estimated DB pension (a year)' },
            { type: 'text', fieldKey: 'resultCash', label: 'Estimated cash' },
            { type: 'text', fieldKey: 'resultTotal', label: 'Estimated total yearly income' },
          ],
        },
      ],
      routes: [
        { id: 'review-quote-request--send-quote--close-request', target: 'close-request', trigger: 'send-quote', label: 'Issue formal quote', style: 'primary' },
      ],
    },
    {
      stateKey: 'quote-sent',
      displayName: 'Formal quote issued',
      stageType: 'Confirmation',
      actor: 'reviewer',
      queueKey: 'business-user',
      components: [
        { type: 'panel', heading: 'Formal quote issued' },
        { type: 'body', content: 'The formal quote has been generated and sent to the member.' },
      ],
      routes: [],
    },
  ],
  gateways: [
    {
      key: 'to-model-from-record',
      displayName: 'Start from record',
      gatewayType: 'Split',
      queueKey: 'web-user',
      routes: [{ id: 'to-model-from-record--continue--model', target: 'model', trigger: 'continue' }],
    },
    {
      key: 'to-quote-entry',
      displayName: 'Start from quote',
      gatewayType: 'Split',
      queueKey: 'web-user',
      routes: [{ id: 'to-quote-entry--continue--enter-quote', target: 'enter-quote', trigger: 'continue' }],
    },
    {
      key: 'to-model-from-quote',
      displayName: 'Quote figures captured',
      gatewayType: 'Split',
      queueKey: 'web-user',
      routes: [{ id: 'to-model-from-quote--continue--model', target: 'model', trigger: 'continue' }],
    },
    {
      key: 'recalculate-loop',
      displayName: 'Recalculate',
      gatewayType: 'Split',
      queueKey: 'web-user',
      routes: [{ id: 'recalculate-loop--continue--model', target: 'model', trigger: 'continue' }],
    },
    {
      key: 'fan-out-quote-request',
      displayName: 'Send quote request',
      gatewayType: 'Split',
      queueKey: 'web-user',
      routes: [
        { id: 'fan-out-quote-request--continue--quote-requested', target: 'quote-requested', trigger: 'continue' },
        { id: 'fan-out-quote-request--continue--review-quote-request', target: 'review-quote-request', trigger: 'continue' },
      ],
    },
    {
      key: 'close-request',
      displayName: 'Close request',
      gatewayType: 'Split',
      queueKey: 'business-user',
      routes: [{ id: 'close-request--continue--quote-sent', target: 'quote-sent', trigger: 'continue' }],
    },
  ],
  tags: { demo: 'money-modeller', pattern: 'interactive-island' },
} as unknown as AuthoredServiceBlueprint);
