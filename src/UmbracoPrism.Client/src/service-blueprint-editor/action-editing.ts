import type {
  ActionCatalogEntry,
  ActionFormFieldConfig,
  ActionFormFieldType,
  ActionTiming,
  AuthoredAction,
  AuthoredParameterDefinition,
} from './types.js';

export type ActionEditorTarget = 'stage' | 'transition';
export type ActionEditorContext = 'stage.onEntry' | 'stage.onExit' | 'transition';

export type ActionValidationResult = {
  messages: string[];
  propertyErrors: Record<string, string>;
  formFieldErrors: Record<number, Partial<Record<'fieldKey' | 'label' | 'type' | 'options' | 'defaultValue', string>>>;
};

export const ACTION_CONTEXTS: ActionEditorContext[] = ['stage.onEntry', 'stage.onExit', 'transition'];
export const ACTION_FORM_FIELD_TYPES: Array<{ value: ActionFormFieldType; label: string }> = [
  { value: 'text', label: 'Text' },
  { value: 'number', label: 'Number' },
  { value: 'textarea', label: 'Textarea' },
  { value: 'select', label: 'Select' },
  { value: 'radio', label: 'Radio' },
  { value: 'date', label: 'Date' },
];

export function cloneJsonValue<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

export function contextLabel(context: ActionEditorContext): string {
  switch (context) {
    case 'stage.onExit':
      return 'Stage · On exit';
    case 'transition':
      return 'Transition';
    case 'stage.onEntry':
    default:
      return 'Stage · On entry';
  }
}

export function timingForContext(context: ActionEditorContext): ActionTiming {
  switch (context) {
    case 'stage.onExit':
      return 'OnExit';
    case 'transition':
      return 'OnTransition';
    case 'stage.onEntry':
    default:
      return 'OnEntry';
  }
}

export function contextForTiming(timing: ActionTiming, target: ActionEditorTarget): ActionEditorContext {
  if (target === 'transition') {
    return 'transition';
  }

  return timing === 'OnExit' ? 'stage.onExit' : 'stage.onEntry';
}

export function entrySupportsContext(entry: ActionCatalogEntry, context: ActionEditorContext): boolean {
  return entry.appliesTo.includes(context);
}

export function availableContexts(entry: ActionCatalogEntry, target: ActionEditorTarget): ActionEditorContext[] {
  return ACTION_CONTEXTS.filter(context => (target === 'stage' ? context !== 'transition' : context === 'transition'))
    .filter(context => entrySupportsContext(entry, context));
}

export function findCatalogEntry(entries: ActionCatalogEntry[], actionType: string): ActionCatalogEntry | null {
  return entries.find(entry => entry.type === actionType) ?? null;
}

function propertyDefault(definition: AuthoredParameterDefinition): unknown {
  if (definition.defaultValue !== undefined) {
    return cloneJsonValue(definition.defaultValue);
  }

  if (definition.valueKind === 'Boolean') return false;
  if (definition.valueKind === 'Integer' || definition.valueKind === 'Number') return '';
  if (definition.valueKind === 'Array') return [];
  if (definition.valueKind === 'Object') {
    return Object.fromEntries(
      (definition.properties ?? []).map(property => [property.key, propertyDefault(property)])
    );
  }
  return '';
}

export function buildActionParams(entry: ActionCatalogEntry): Record<string, unknown> {
  const schemaDefaults = Object.fromEntries(
    (entry.paramsSchema.properties ?? []).map(property => [property.key, propertyDefault(property)])
  );

  return {
    ...schemaDefaults,
    ...(entry.defaultParams ? cloneJsonValue(entry.defaultParams) : {}),
  };
}

export function isFormsBackedAction(entry: ActionCatalogEntry | null): boolean {
  const fieldsDefinition = entry?.paramsSchema.properties?.find(property => property.key === 'fields');
  return fieldsDefinition?.valueKind === 'Array' && fieldsDefinition.items?.valueKind === 'Object';
}

export function normaliseActionFormFields(value: unknown): ActionFormFieldConfig[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.map((field, index) => {
    const record = typeof field === 'object' && field !== null ? field as Record<string, unknown> : {};
    const type = String(record.type ?? 'text') as ActionFormFieldType;
    return {
      fieldKey: String(record.fieldKey ?? `field-${index + 1}`),
      label: String(record.label ?? ''),
      type: ACTION_FORM_FIELD_TYPES.some(option => option.value === type) ? type : 'text',
      required: Boolean(record.required),
      hintText: typeof record.hintText === 'string' ? record.hintText : undefined,
      validationPattern: typeof record.validationPattern === 'string' ? record.validationPattern : undefined,
      defaultValue: typeof record.defaultValue === 'string' ? record.defaultValue : undefined,
      options: Array.isArray(record.options) ? record.options.map(option => String(option)) : [],
    };
  });
}

function isMissingValue(value: unknown): boolean {
  return value === undefined
    || value === null
    || (typeof value === 'string' && value.trim().length === 0)
    || (Array.isArray(value) && value.length === 0);
}

function validateProperty(definition: AuthoredParameterDefinition, value: unknown): string | null {
  if (isMissingValue(value)) {
    return null;
  }

  if (definition.allowedValues?.length && typeof value === 'string' && !definition.allowedValues.includes(value)) {
    return `Choose one of: ${definition.allowedValues.join(', ')}.`;
  }

  if (definition.valueKind === 'Integer' || definition.valueKind === 'Number') {
    const numberValue = typeof value === 'number' ? value : Number(value);
    if (Number.isNaN(numberValue)) {
      return 'Enter a valid number.';
    }
  }

  if (definition.format === 'email' && typeof value === 'string' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())) {
    return 'Enter a valid email address.';
  }

  if (definition.format === 'date' && typeof value === 'string' && value.trim().length > 0 && Number.isNaN(Date.parse(value))) {
    return 'Enter a valid date.';
  }

  return null;
}

function validateFormFields(fields: ActionFormFieldConfig[]): ActionValidationResult['formFieldErrors'] {
  const errors: ActionValidationResult['formFieldErrors'] = {};
  const seenKeys = new Set<string>();

  fields.forEach((field, index) => {
    const nextErrors: ActionValidationResult['formFieldErrors'][number] = {};
    const key = field.fieldKey.trim();
    if (!key) {
      nextErrors.fieldKey = 'Field key is required.';
    } else if (seenKeys.has(key)) {
      nextErrors.fieldKey = 'Field key must be unique.';
    } else {
      seenKeys.add(key);
    }

    if (!field.label.trim()) {
      nextErrors.label = 'Field label is required.';
    }

    if (!ACTION_FORM_FIELD_TYPES.some(option => option.value === field.type)) {
      nextErrors.type = 'Choose a supported field type.';
    }

    if ((field.type === 'select' || field.type === 'radio') && field.options.filter(option => option.trim()).length === 0) {
      nextErrors.options = 'Add at least one option for select or radio fields.';
    }

    if (field.type === 'number' && field.defaultValue && Number.isNaN(Number(field.defaultValue))) {
      nextErrors.defaultValue = 'Default value must be numeric.';
    }

    if (Object.keys(nextErrors).length > 0) {
      errors[index] = nextErrors;
    }
  });

  return errors;
}

export function validateAction(entry: ActionCatalogEntry | null, action: AuthoredAction): ActionValidationResult {
  const propertyErrors: Record<string, string> = {};
  const messages: string[] = [];

  if (!entry) {
    return { messages, propertyErrors, formFieldErrors: {} };
  }

  const params = action.params ?? {};
  const requiredKeys = new Set(entry.paramsSchema.required ?? []);

  for (const property of entry.paramsSchema.properties ?? []) {
    const value = params[property.key];
    if (requiredKeys.has(property.key) && isMissingValue(value)) {
      propertyErrors[property.key] = `${property.title || property.key} is required.`;
      continue;
    }

    const propertyError = validateProperty(property, value);
    if (propertyError) {
      propertyErrors[property.key] = propertyError;
    }
  }

  const formFieldErrors = isFormsBackedAction(entry)
    ? validateFormFields(normaliseActionFormFields(params.fields))
    : {};

  Object.values(propertyErrors).forEach(message => messages.push(message));
  Object.values(formFieldErrors).forEach(fieldErrors => {
    Object.values(fieldErrors).forEach(message => {
      if (message) {
        messages.push(message);
      }
    });
  });

  return { messages, propertyErrors, formFieldErrors };
}

export function summariseAction(entry: ActionCatalogEntry | null, action: AuthoredAction): string {
  const params = action.params ?? {};
  switch (action.type) {
    case 'forms.load':
      return params.formDefinitionId ? `Load form “${params.formDefinitionId}”` : 'Load form';
    case 'forms.save':
      return params.formDefinitionId ? `Save form “${params.formDefinitionId}”` : 'Save form';
    case 'forms.submit':
      return params.formDefinitionId ? `Submit form “${params.formDefinitionId}”` : 'Submit form';
    case 'case.assign': {
      const type = typeof params.assigneeType === 'string' ? params.assigneeType : 'role';
      const value = typeof params.assigneeValue === 'string' ? params.assigneeValue : '';
      return value ? `Assign to ${type} ${value}` : 'Assign case';
    }
    case 'case.enqueue': {
      const queue = typeof params.queue === 'string' ? params.queue : '';
      const priority = typeof params.priority === 'string' ? params.priority : 'normal';
      return queue ? `Queue in ${queue} (${priority})` : 'Enqueue case';
    }
    case 'case.set-status':
      return typeof params.status === 'string' && params.status.trim() ? `Set case status to ${params.status}` : 'Set case status';
    case 'case.add-note':
      return typeof params.visibility === 'string' && params.visibility.trim()
        ? `Add ${params.visibility} note`
        : 'Add case note';
    case 'notifications.send-email':
      return typeof params.recipientEmail === 'string' && params.recipientEmail.trim()
        ? `Send email to ${params.recipientEmail}`
        : 'Send email';
    case 'notifications.send-sms':
      return typeof params.recipientNumber === 'string' && params.recipientNumber.trim()
        ? `Send SMS to ${params.recipientNumber}`
        : 'Send SMS';
    case 'forms.request-evidence': {
      const fields = normaliseActionFormFields(params.fields);
      return `Request evidence form: ${fields.length} field${fields.length === 1 ? '' : 's'}`;
    }
    default:
      return action.summary ?? entry?.summary ?? action.type;
  }
}

export function updateActionSummary(entry: ActionCatalogEntry | null, action: AuthoredAction): AuthoredAction {
  return {
    ...action,
    summary: summariseAction(entry, action),
  };
}

export function blankActionFormField(index: number): ActionFormFieldConfig {
  return {
    fieldKey: `field-${index + 1}`,
    label: '',
    type: 'text',
    required: false,
    hintText: '',
    validationPattern: '',
    defaultValue: '',
    options: [],
  };
}
