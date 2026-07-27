import { LitElement, css, html, nothing } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import type { ActionCatalogEntry, ActionFormFieldConfig, AuthoredAction, AuthoredParameterDefinition } from './types.js';
import {
  ACTION_FORM_FIELD_TYPES,
  availableContexts,
  blankActionFormField,
  buildActionParams,
  cloneJsonValue,
  contextForTiming,
  contextLabel,
  findCatalogEntry,
  isFormsBackedAction,
  normaliseActionFormFields,
  timingForContext,
  updateActionSummary,
  validateAction,
  type ActionEditorContext,
  type ActionEditorTarget,
} from './action-editing.js';
import './prism-inline-help.js';

type ActionsUpdatedDetail = {
  actions: AuthoredAction[];
};

type ActionSelectedDetail = {
  index: number | null;
  target: ActionEditorTarget;
};

type ActionPickerState = {
  query: string;
  context: ActionEditorContext;
  selectedType: string | null;
};

type DeleteActionDialogState = {
  index: number;
  label: string;
};

/**
 * @internal Composition detail of <prism-service-blueprint-editor>; not part of the public API surface.
 */
@customElement('prism-stage-action-editor')
export class PrismServiceBlueprintActionEditorElement extends LitElement {
  @property({ attribute: false })
  actions: AuthoredAction[] = [];

  @property({ attribute: false })
  actionCatalog: ActionCatalogEntry[] = [];

  @property({ type: String })
  target: ActionEditorTarget = 'stage';

  @property({ type: String, attribute: 'subject-label' })
  subjectLabel = 'stage';

  @property({ type: Number, attribute: false })
  selectedActionIndex: number | null = null;

  @state() private _statusMessage: string | null = null;
  @state() private _draggedActionIndex: number | null = null;
  @state() private _dragOverActionIndex: number | null = null;
  @state() private _picker: ActionPickerState | null = null;
  @state() private _deleteDialog: DeleteActionDialogState | null = null;

  private _dialogReturnTarget: HTMLElement | null = null;

  protected updated(changed: Map<string, unknown>) {
    if (changed.has('_picker') && this._picker) {
      requestAnimationFrame(() => {
        this.shadowRoot?.querySelector<HTMLInputElement>('[data-prism-action-picker-search]')?.focus();
      });
    }

    if (changed.has('_deleteDialog') && this._deleteDialog) {
      requestAnimationFrame(() => {
        this.shadowRoot?.querySelector<HTMLElement>('[data-prism-delete-action-cancel]')?.focus();
      });
    }

    if (changed.has('selectedActionIndex') && this.selectedActionIndex !== null) {
      requestAnimationFrame(() => {
        this._focusActionEditor(this.selectedActionIndex!);
      });
    }

    if (changed.has('actions') && this.selectedActionIndex !== null && this.selectedActionIndex >= this.actions.length) {
      this._setSelectedAction(this.actions.length > 0 ? this.actions.length - 1 : null);
    }
  }

  private get _catalogEntries(): ActionCatalogEntry[] {
    return this.actionCatalog.filter(entry =>
      availableContexts(entry, this.target).length > 0
    );
  }

  private get _pickerEntries(): ActionCatalogEntry[] {
    if (!this._picker) {
      return [];
    }

    const query = this._picker.query.trim().toLowerCase();
    return this._catalogEntries
      .filter(entry => entry.appliesTo.includes(this._picker!.context))
      .filter(entry => {
        if (!query) {
          return true;
        }

        return [entry.label, entry.type, entry.summary].some(value =>
          value.toLowerCase().includes(query)
        );
      });
  }

  private _emitActionsUpdated(actions: AuthoredAction[]) {
    this.dispatchEvent(
      new CustomEvent<ActionsUpdatedDetail>('actions-updated', {
        detail: { actions },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _announce(message: string) {
    this._statusMessage = '';
    requestAnimationFrame(() => {
      this._statusMessage = message;
    });
  }

  private _emitActionSelected(index: number | null) {
    this.dispatchEvent(
      new CustomEvent<ActionSelectedDetail>('action-selected', {
        detail: { index, target: this.target },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _setSelectedAction(index: number | null) {
    this.selectedActionIndex = index;
    this._emitActionSelected(index);
  }

  private _focusActionEditor(index: number) {
    const row = this.shadowRoot?.querySelector<HTMLElement>(`[data-prism-stage-action="${index}"]`);
    if (!row) {
      return;
    }

    const firstField = row.querySelector<HTMLElement>(`[data-prism-action-param^="${index}-"], [data-prism-stage-action-timing="${index}"]`)
      ?? row.querySelector<HTMLElement>('input, select, textarea')
      ?? row.querySelector<HTMLElement>('button:not([disabled])');
    row.scrollIntoView({ block: 'nearest' });
    (firstField ?? row).focus();
  }

  private _actionEntry(action: AuthoredAction) {
    return findCatalogEntry(this.actionCatalog, action.type);
  }

  private _actionLabel(action: AuthoredAction) {
    return this._actionEntry(action)?.label ?? action.summary ?? action.type;
  }

  private _updateAction(index: number, nextAction: AuthoredAction) {
    const actions = [...this.actions];
    if (!actions[index]) {
      return;
    }

    actions[index] = updateActionSummary(this._actionEntry(nextAction), nextAction);
    this._emitActionsUpdated(actions);
  }

  private _updateActionParams(index: number, nextParams: Record<string, unknown>) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    this._updateAction(index, { ...action, params: nextParams });
  }

  private _updateActionParam(index: number, key: string, value: unknown) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    this._updateActionParams(index, {
      ...(action.params ?? {}),
      [key]: value,
    });
  }

  private _openPicker(activator?: HTMLElement | null) {
    const firstContext = this.target === 'transition' ? 'transition' : 'stage.onEntry';
    const firstEntry = this._catalogEntries.find(entry => entry.appliesTo.includes(firstContext))
      ?? this._catalogEntries[0]
      ?? null;
    this._dialogReturnTarget = activator ?? null;
    this._picker = {
      query: '',
      context: firstContext,
      selectedType: firstEntry?.type ?? null,
    };
  }

  private _closePicker() {
    this._picker = null;
    this._dialogReturnTarget?.focus();
    this._dialogReturnTarget = null;
  }

  private _addPickedAction() {
    if (!this._picker) {
      return;
    }

    const entry = this._catalogEntries.find(candidate => candidate.type === this._picker?.selectedType)
      ?? this._pickerEntries[0]
      ?? null;
    if (!entry) {
      return;
    }

    const action = updateActionSummary(entry, {
      type: entry.type,
      timing: timingForContext(this._picker.context),
      parameterSchemaKey: entry.paramsSchema.key || undefined,
      params: buildActionParams(entry),
      summary: entry.summary,
    });

    this._emitActionsUpdated([...this.actions, action]);
    this._setSelectedAction(this.actions.length);
    this._announce(`${entry.label} added to ${this.subjectLabel}.`);
    this._closePicker();
  }

  private _updateActionTiming(index: number, event: Event) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    this._updateAction(index, {
      ...action,
      timing: (event.currentTarget as HTMLSelectElement).value as AuthoredAction['timing'],
    });
  }

  private _openDeleteDialog(index: number, activator?: HTMLElement | null) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    this._dialogReturnTarget = activator ?? null;
    this._deleteDialog = {
      index,
      label: this._actionLabel(action),
    };
  }

  private _closeDeleteDialog() {
    this._deleteDialog = null;
    this._dialogReturnTarget?.focus();
    this._dialogReturnTarget = null;
  }

  private _confirmDeleteAction() {
    if (!this._deleteDialog) {
      return;
    }

    const actions = [...this.actions];
    const [removed] = actions.splice(this._deleteDialog.index, 1);
    this._emitActionsUpdated(actions);
    if (this.selectedActionIndex === this._deleteDialog.index) {
      this._setSelectedAction(actions.length > 0 ? Math.max(this._deleteDialog.index - 1, 0) : null);
    } else if (this.selectedActionIndex !== null && this.selectedActionIndex > this._deleteDialog.index) {
      this._setSelectedAction(this.selectedActionIndex - 1);
    }
    if (removed) {
      this._announce(`${this._actionLabel(removed)} removed.`);
    }
    this._closeDeleteDialog();
  }

  private _moveAction(index: number, delta: -1 | 1) {
    const nextIndex = Math.min(this.actions.length - 1, Math.max(0, index + delta));
    if (nextIndex === index) {
      return;
    }

    const actions = [...this.actions];
    const [action] = actions.splice(index, 1);
    actions.splice(nextIndex, 0, action);
    this._emitActionsUpdated(actions);
    if (this.selectedActionIndex === index) {
      this._setSelectedAction(nextIndex);
    } else if (this.selectedActionIndex === nextIndex) {
      this._setSelectedAction(index);
    }
    this._announce(`${this._actionLabel(action)} moved to position ${nextIndex + 1}.`);
  }

  private _reorderAction(fromIndex: number, toIndex: number) {
    if (fromIndex === toIndex) {
      this._draggedActionIndex = null;
      this._dragOverActionIndex = null;
      return;
    }

    const actions = [...this.actions];
    const [action] = actions.splice(fromIndex, 1);
    actions.splice(toIndex, 0, action);
    this._draggedActionIndex = null;
    this._dragOverActionIndex = null;
    this._emitActionsUpdated(actions);
    if (this.selectedActionIndex === fromIndex) {
      this._setSelectedAction(toIndex);
    }
    this._announce(`${this._actionLabel(action)} reordered.`);
  }

  private _handleActionRowKeydown(event: KeyboardEvent, index: number) {
    if (event.altKey && event.key === 'ArrowUp') {
      event.preventDefault();
      this._moveAction(index, -1);
      return;
    }

    if (event.altKey && event.key === 'ArrowDown') {
      event.preventDefault();
      this._moveAction(index, 1);
      return;
    }

    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this._openDeleteDialog(index);
    }
  }

  private _updateFormFields(index: number, nextFields: ActionFormFieldConfig[]) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    this._updateActionParams(index, {
      ...(action.params ?? {}),
      fields: cloneJsonValue(nextFields),
    });
  }

  private _addFormField(index: number) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    const fields = normaliseActionFormFields(action.params?.fields);
    const nextFields = [...fields, blankActionFormField(fields.length)];
    this._updateFormFields(index, nextFields);
    this._announce(`Field ${nextFields.length} added.`);
  }

  private _updateFormField(index: number, fieldIndex: number, patch: Partial<ActionFormFieldConfig>) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    const fields = normaliseActionFormFields(action.params?.fields);
    const current = fields[fieldIndex];
    if (!current) {
      return;
    }

    const nextFields = [...fields];
    nextFields[fieldIndex] = { ...current, ...patch };
    this._updateFormFields(index, nextFields);
  }

  private _moveFormField(index: number, fieldIndex: number, delta: -1 | 1) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    const fields = normaliseActionFormFields(action.params?.fields);
    const nextIndex = Math.min(fields.length - 1, Math.max(0, fieldIndex + delta));
    if (nextIndex === fieldIndex) {
      return;
    }

    const nextFields = [...fields];
    const [field] = nextFields.splice(fieldIndex, 1);
    nextFields.splice(nextIndex, 0, field);
    this._updateFormFields(index, nextFields);
    this._announce(`${field.label || field.fieldKey || 'Field'} moved to position ${nextIndex + 1}.`);
  }

  private _removeFormField(index: number, fieldIndex: number) {
    const action = this.actions[index];
    if (!action) {
      return;
    }

    const fields = normaliseActionFormFields(action.params?.fields);
    const nextFields = [...fields];
    const [removed] = nextFields.splice(fieldIndex, 1);
    this._updateFormFields(index, nextFields);
    this._announce(`${removed?.label || removed?.fieldKey || 'Field'} removed.`);
  }

  private _handleDialogKeydown(event: KeyboardEvent, onClose: () => void) {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const root = event.currentTarget as HTMLElement;
    const focusable = Array.from(
      root.querySelectorAll<HTMLElement>('button, input, select, textarea, [href], [tabindex]:not([tabindex="-1"])')
    ).filter(element => !element.hasAttribute('disabled') && element.tabIndex >= 0);
    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const activeElement = this.shadowRoot?.activeElement as HTMLElement | null;
    if (event.shiftKey && activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private _renderScalarField(
    index: number,
    definition: AuthoredParameterDefinition,
    validation: ReturnType<typeof validateAction>
  ) {
    const action = this.actions[index];
    if (!action) {
      return nothing;
    }

    const value = action.params?.[definition.key];
    const error = validation.propertyErrors[definition.key];
    const editor = definition.editor ?? (definition.allowedValues?.length ? 'select' : undefined);

    if ((editor === 'toggle') || definition.valueKind === 'Boolean') {
      return html`
        <label class="field-toggle">
          <input
            type="checkbox"
            .checked=${Boolean(value)}
            data-prism-action-param="${index}-${definition.key}"
            @change=${(event: Event) => this._updateActionParam(index, definition.key, (event.currentTarget as HTMLInputElement).checked)}
          />
          <span>${definition.title}</span>
        </label>
      `;
    }

    if (editor === 'textarea') {
      return html`
        <label class="field-block">
          <span class="field-label">${definition.title}</span>
          <textarea
            class="field-control field-textarea ${error ? 'field-control-error' : ''}"
            aria-invalid=${String(Boolean(error))}
            data-prism-action-param="${index}-${definition.key}"
            .value=${typeof value === 'string' ? value : String(value ?? '')}
            @input=${(event: Event) => this._updateActionParam(index, definition.key, (event.currentTarget as HTMLTextAreaElement).value)}
          ></textarea>
          ${definition.description ? html`<span class="field-help">${definition.description}</span>` : nothing}
          ${error ? html`<span class="field-error">${error}</span>` : nothing}
        </label>
      `;
    }

    if (editor === 'select' || definition.allowedValues?.length) {
      return html`
        <label class="field-block">
          <span class="field-label">${definition.title}</span>
          <select
            class="field-control ${error ? 'field-control-error' : ''}"
            aria-invalid=${String(Boolean(error))}
            data-prism-action-param="${index}-${definition.key}"
            @change=${(event: Event) => this._updateActionParam(index, definition.key, (event.currentTarget as HTMLSelectElement).value)}
          >
            ${definition.allowedValues?.map(option => html`
              <option value=${option} ?selected=${String(value ?? '') === option}>${option}</option>
            `)}
          </select>
          ${definition.description ? html`<span class="field-help">${definition.description}</span>` : nothing}
          ${error ? html`<span class="field-error">${error}</span>` : nothing}
        </label>
      `;
    }

    const inputType =
      editor === 'date' || definition.format === 'date'
        ? 'date'
        : editor === 'number' || definition.valueKind === 'Integer' || definition.valueKind === 'Number'
          ? 'number'
          : 'text';

    return html`
      <label class="field-block">
        <span class="field-label">${definition.title}</span>
        <input
          class="field-control ${error ? 'field-control-error' : ''}"
          aria-invalid=${String(Boolean(error))}
          type=${inputType}
          data-prism-action-param="${index}-${definition.key}"
          .value=${value === undefined || value === null ? '' : String(value)}
          @input=${(event: Event) => {
            const nextValue = (event.currentTarget as HTMLInputElement).value;
            this._updateActionParam(
              index,
              definition.key,
              inputType === 'number' ? (nextValue === '' ? '' : Number(nextValue)) : nextValue
            );
          }}
        />
        ${definition.description ? html`<span class="field-help">${definition.description}</span>` : nothing}
        ${error ? html`<span class="field-error">${error}</span>` : nothing}
      </label>
    `;
  }

  private _renderFormsEditor(index: number, validation: ReturnType<typeof validateAction>) {
    const action = this.actions[index];
    if (!action) {
      return nothing;
    }

    const fields = normaliseActionFormFields(action.params?.fields);
    return html`
      <div class="forms-editor" data-prism-action-forms-editor="${index}">
        <div class="section-header-row">
          <h4 class="subsection-heading">Form fields</h4>
          <span class="section-meta">${fields.length}</span>
        </div>
        <p class="section-copy">Add, remove, and reorder fields. Select and radio fields require options.</p>
        <button type="button" class="secondary-button" data-prism-add-form-field="${index}" @click=${() => this._addFormField(index)}>
          Add field
        </button>
        ${fields.length === 0
          ? html`<p class="section-empty">No fields configured yet.</p>`
          : html`
              <ol class="form-field-list">
                ${fields.map((field, fieldIndex) => {
                  const fieldErrors = validation.formFieldErrors[fieldIndex] ?? {};
                  return html`
                    <li
                      class="form-field-item"
                      data-prism-form-field="${index}-${fieldIndex}"
                      tabindex="0"
                      @keydown=${(event: KeyboardEvent) => {
                        if (event.altKey && event.key === 'ArrowUp') {
                          event.preventDefault();
                          this._moveFormField(index, fieldIndex, -1);
                        } else if (event.altKey && event.key === 'ArrowDown') {
                          event.preventDefault();
                          this._moveFormField(index, fieldIndex, 1);
                        }
                      }}
                    >
                      <div class="field-grid">
                        <label class="field-block">
                          <span class="field-label">Field key</span>
                          <input
                            class="field-control ${fieldErrors.fieldKey ? 'field-control-error' : ''}"
                            aria-invalid=${String(Boolean(fieldErrors.fieldKey))}
                            .value=${field.fieldKey}
                            data-prism-form-field-key="${index}-${fieldIndex}"
                            @input=${(event: Event) => this._updateFormField(index, fieldIndex, { fieldKey: (event.currentTarget as HTMLInputElement).value })}
                          />
                          ${fieldErrors.fieldKey ? html`<span class="field-error">${fieldErrors.fieldKey}</span>` : nothing}
                        </label>
                        <label class="field-block">
                          <span class="field-label">Label</span>
                          <input
                            class="field-control ${fieldErrors.label ? 'field-control-error' : ''}"
                            aria-invalid=${String(Boolean(fieldErrors.label))}
                            .value=${field.label}
                            data-prism-form-field-label="${index}-${fieldIndex}"
                            @input=${(event: Event) => this._updateFormField(index, fieldIndex, { label: (event.currentTarget as HTMLInputElement).value })}
                          />
                          ${fieldErrors.label ? html`<span class="field-error">${fieldErrors.label}</span>` : nothing}
                        </label>
                        <label class="field-block">
                          <span class="field-label">Field type</span>
                          <select
                            class="field-control ${fieldErrors.type ? 'field-control-error' : ''}"
                            aria-invalid=${String(Boolean(fieldErrors.type))}
                            data-prism-form-field-type="${index}-${fieldIndex}"
                            @change=${(event: Event) => this._updateFormField(index, fieldIndex, { type: (event.currentTarget as HTMLSelectElement).value as ActionFormFieldConfig['type'] })}
                          >
                            ${ACTION_FORM_FIELD_TYPES.map(option => html`
                              <option value=${option.value} ?selected=${field.type === option.value}>${option.label}</option>
                            `)}
                          </select>
                          ${fieldErrors.type ? html`<span class="field-error">${fieldErrors.type}</span>` : nothing}
                        </label>
                        <label class="field-block">
                          <span class="field-label">Default value</span>
                          <input
                            class="field-control ${fieldErrors.defaultValue ? 'field-control-error' : ''}"
                            aria-invalid=${String(Boolean(fieldErrors.defaultValue))}
                            .value=${field.defaultValue ?? ''}
                            data-prism-form-field-default="${index}-${fieldIndex}"
                            @input=${(event: Event) => this._updateFormField(index, fieldIndex, { defaultValue: (event.currentTarget as HTMLInputElement).value })}
                          />
                          ${fieldErrors.defaultValue ? html`<span class="field-error">${fieldErrors.defaultValue}</span>` : nothing}
                        </label>
                      </div>
                      <div class="field-grid">
                        <label class="field-block field-block-full">
                          <span class="field-label-row">
                            <span class="field-label">Help text</span>
                            <prism-inline-help
                              label="Form field help text guidance"
                              message="Use this for short, task-specific guidance that appears below the field in the authored form. Keep it instructional rather than repeating the label."
                            ></prism-inline-help>
                          </span>
                          <textarea
                            class="field-control field-textarea"
                            .value=${field.hintText ?? ''}
                            @input=${(event: Event) => this._updateFormField(index, fieldIndex, { hintText: (event.currentTarget as HTMLTextAreaElement).value })}
                          ></textarea>
                        </label>
                        <label class="field-block">
                          <span class="field-label-row">
                            <span class="field-label">Validation pattern</span>
                            <prism-inline-help
                              label="Validation pattern help"
                              message="Add a regular expression only when the field needs a strict format such as a reference number or postcode. Keep patterns short and explain them in help text if they are not obvious."
                            ></prism-inline-help>
                          </span>
                          <input
                            class="field-control"
                            .value=${field.validationPattern ?? ''}
                            @input=${(event: Event) => this._updateFormField(index, fieldIndex, { validationPattern: (event.currentTarget as HTMLInputElement).value })}
                          />
                        </label>
                        <label class="field-toggle">
                          <input
                            type="checkbox"
                            .checked=${field.required}
                            data-prism-form-field-required="${index}-${fieldIndex}"
                            @change=${(event: Event) => this._updateFormField(index, fieldIndex, { required: (event.currentTarget as HTMLInputElement).checked })}
                          />
                          <span>Required</span>
                        </label>
                      </div>
                      ${field.type === 'select' || field.type === 'radio'
                        ? html`
                            <label class="field-block field-block-full">
                              <span class="field-label-row">
                                <span class="field-label">Options</span>
                                <prism-inline-help
                                  label="Field options help"
                                  message="Enter one choice per line in the order authors should see them. Keep labels short and distinct so keyboard and screen-reader users can scan them quickly."
                                ></prism-inline-help>
                              </span>
                              <textarea
                                class="field-control field-textarea ${fieldErrors.options ? 'field-control-error' : ''}"
                                aria-invalid=${String(Boolean(fieldErrors.options))}
                                data-prism-form-field-options="${index}-${fieldIndex}"
                                .value=${field.options.join('\n')}
                                @input=${(event: Event) =>
                                  this._updateFormField(index, fieldIndex, {
                                    options: (event.currentTarget as HTMLTextAreaElement).value
                                      .split('\n')
                                      .map(option => option.trim())
                                      .filter(Boolean),
                                  })}
                              ></textarea>
                              <span class="field-help">One option per line.</span>
                              ${fieldErrors.options ? html`<span class="field-error">${fieldErrors.options}</span>` : nothing}
                            </label>
                          `
                        : nothing}
                      <div class="action-buttons">
                        <button type="button" class="icon-button" ?disabled=${fieldIndex === 0} @click=${() => this._moveFormField(index, fieldIndex, -1)}>Move up</button>
                        <button type="button" class="icon-button" ?disabled=${fieldIndex === fields.length - 1} @click=${() => this._moveFormField(index, fieldIndex, 1)}>Move down</button>
                        <button type="button" class="icon-button danger-button" @click=${() => this._removeFormField(index, fieldIndex)}>Remove field</button>
                      </div>
                    </li>
                  `;
                })}
              </ol>
            `}
      </div>
    `;
  }

  private _renderActionParameters(index: number) {
    const action = this.actions[index];
    if (!action) {
      return nothing;
    }

    const entry = this._actionEntry(action);
    const validation = validateAction(entry, action);
    const properties = entry?.paramsSchema.properties ?? [];
    const formFieldsProperty = properties.find(property => property.key === 'fields');
    const scalarProperties = properties.filter(property => property.key !== 'fields');

    return html`
      <div class="action-parameters">
        ${scalarProperties.length === 0
          ? nothing
          : html`
              <div class="field-grid">
                ${scalarProperties.map(property => this._renderScalarField(index, property, validation))}
              </div>
            `}
        ${formFieldsProperty && isFormsBackedAction(entry) ? this._renderFormsEditor(index, validation) : nothing}
        ${validation.messages.length > 0
          ? html`
              <div class="action-validation" data-prism-action-errors="${index}">
                <p class="action-validation-title">Fix these action details before saving:</p>
                <ul>
                  ${validation.messages.map(message => html`<li>${message}</li>`)}
                </ul>
              </div>
            `
          : nothing}
      </div>
    `;
  }

  private _renderPickerDialog() {
    if (!this._picker) {
      return nothing;
    }

    return html`
      <div class="dialog-backdrop" role="presentation">
        <div
          class="dialog-panel"
          role="dialog"
          aria-modal="true"
          aria-labelledby="action-picker-title"
          data-prism-action-picker-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closePicker())}
        >
          <div class="dialog-header">
            <div>
              <p class="dialog-eyebrow">Action picker</p>
              <h4 id="action-picker-title" class="dialog-title">Add an action</h4>
            </div>
          </div>
          <div class="dialog-grid">
            <label class="dialog-field">
              <span class="dialog-label">Search</span>
              <input
                class="dialog-control"
                data-prism-action-picker-search
                .value=${this._picker.query}
                @input=${(event: Event) => {
                  const query = (event.currentTarget as HTMLInputElement).value;
                  this._picker = this._picker ? { ...this._picker, query } : null;
                }}
              />
            </label>
            <label class="dialog-field">
              <span class="dialog-label">Context</span>
              <select
                class="dialog-control"
                data-prism-action-picker-context
                @change=${(event: Event) => {
                  const context = (event.currentTarget as HTMLSelectElement).value as ActionEditorContext;
                  const firstEntry = this._catalogEntries.find(entry => entry.appliesTo.includes(context)) ?? null;
                  this._picker = this._picker ? { ...this._picker, context, selectedType: firstEntry?.type ?? null } : null;
                }}
              >
                ${(this.target === 'transition' ? ['transition'] : ['stage.onEntry', 'stage.onExit']).map(context => html`
                  <option value=${context} ?selected=${this._picker?.context === context}>${contextLabel(context as ActionEditorContext)}</option>
                `)}
              </select>
            </label>
          </div>
          <div class="picker-list" role="listbox" aria-label="Available actions">
            ${this._pickerEntries.map(entry => html`
              <button
                type="button"
                class=${`picker-option ${this._picker?.selectedType === entry.type ? 'selected' : ''}`}
                data-prism-action-picker-option=${entry.type}
                @click=${() => {
                  this._picker = this._picker ? { ...this._picker, selectedType: entry.type } : null;
                }}
              >
                <span class="picker-option-title">${entry.label}</span>
                <span class="picker-option-type">${entry.type}</span>
                <span class="picker-option-summary">${entry.summary}</span>
                <span class="picker-option-meta">${entry.appliesTo.filter(scope => scope !== 'transition' || this.target === 'transition').map(scope => contextLabel(scope as ActionEditorContext)).join(' · ')}</span>
              </button>
            `)}
            ${this._pickerEntries.length === 0 ? html`<p class="section-empty">No actions match the current filter.</p>` : nothing}
          </div>
          <div class="dialog-actions">
            <button type="button" class="dialog-button secondary" @click=${() => this._closePicker()}>Cancel</button>
            <button type="button" class="dialog-button primary" data-prism-action-picker-add @click=${() => this._addPickedAction()} ?disabled=${this._pickerEntries.length === 0}>Add action</button>
          </div>
        </div>
      </div>
    `;
  }

  private _renderDeleteDialog() {
    if (!this._deleteDialog) {
      return nothing;
    }

    return html`
      <div class="dialog-backdrop" role="presentation">
        <div
          class="dialog-panel dialog-panel-danger"
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-action-title"
          data-prism-delete-action-dialog
          @keydown=${(event: KeyboardEvent) => this._handleDialogKeydown(event, () => this._closeDeleteDialog())}
        >
          <div class="dialog-header">
            <div>
              <p class="dialog-eyebrow danger">Delete action</p>
              <h4 id="delete-action-title" class="dialog-title">Delete ${this._deleteDialog.label}?</h4>
            </div>
          </div>
          <p class="dialog-copy">This removes the action and its configuration from this ${this.subjectLabel}.</p>
          <div class="dialog-actions">
            <button type="button" class="dialog-button secondary" data-prism-delete-action-cancel @click=${() => this._closeDeleteDialog()}>Cancel</button>
            <button type="button" class="dialog-button danger" data-prism-delete-action-confirm @click=${() => this._confirmDeleteAction()}>Delete action</button>
          </div>
        </div>
      </div>
    `;
  }

  render() {
    return html`
      <div class="service-blueprint-action-editor">
        <div class="sr-only" role="status" aria-live="polite" aria-atomic="true">${this._statusMessage ?? ''}</div>
        <div class="section-header-row">
          <div>
            <p class="section-copy">
              ${this.target === 'stage'
                ? 'Pick actions by stage context, then configure typed parameters or forms-backed fields.'
                : 'Pick transition actions and configure their parameters here.'}
            </p>
          </div>
          <button type="button" class="secondary-button" data-prism-open-action-picker @click=${(event: Event) => this._openPicker(event.currentTarget as HTMLElement)}>
            Add action
          </button>
        </div>
        ${this.actions.length === 0
          ? html`<p class="section-empty">No actions configured for this ${this.subjectLabel}.</p>`
          : html`
              <ol class="action-list">
                ${this.actions.map((action, index) => {
                  const entry = this._actionEntry(action);
                  const contexts = entry ? availableContexts(entry, this.target) : [];
                  const isDragOver = this._dragOverActionIndex === index;
                  return html`
                    <li
                      class="action-item ${isDragOver ? 'action-item-drop' : ''} ${this.selectedActionIndex === index ? 'action-item-selected' : ''}"
                      data-prism-stage-action="${index}"
                      data-prism-action-selected=${String(this.selectedActionIndex === index)}
                      tabindex="0"
                      @click=${() => this._setSelectedAction(index)}
                      @focusin=${() => this._setSelectedAction(index)}
                      @keydown=${(event: KeyboardEvent) => this._handleActionRowKeydown(event, index)}
                      @dragover=${(event: DragEvent) => {
                        if (this._draggedActionIndex === null || this._draggedActionIndex === index) {
                          return;
                        }
                        event.preventDefault();
                        this._dragOverActionIndex = index;
                      }}
                      @drop=${(event: DragEvent) => {
                        event.preventDefault();
                        if (this._draggedActionIndex !== null) {
                          this._reorderAction(this._draggedActionIndex, index);
                        }
                      }}
                    >
                      <div class="action-item-main">
                        <button
                          type="button"
                          class="drag-button"
                          draggable="true"
                          aria-label=${`Drag ${this._actionLabel(action)} to reorder`}
                          @dragstart=${() => {
                            this._draggedActionIndex = index;
                            this._dragOverActionIndex = null;
                          }}
                          @dragend=${() => {
                            this._draggedActionIndex = null;
                            this._dragOverActionIndex = null;
                          }}
                        >
                          ↕
                        </button>
                        <div class="action-copy">
                          <p class="action-title">${this._actionLabel(action)}</p>
                          <p class="action-summary">${action.summary ?? entry?.summary ?? action.type}</p>
                        </div>
                      </div>
                      <div class="action-item-controls">
                        ${this.target === 'stage'
                          ? html`
                              <label class="field-block compact-field">
                                <span class="field-label">Timing</span>
                                <select
                                  class="field-control"
                                  data-prism-stage-action-timing="${index}"
                                  @change=${(event: Event) => this._updateActionTiming(index, event)}
                                >
                                  ${contexts.map(context => html`
                                    <option
                                      value=${timingForContext(context)}
                                      ?selected=${contextForTiming(action.timing, this.target) === context}
                                    >
                                      ${contextLabel(context)}
                                    </option>
                                  `)}
                                </select>
                              </label>
                            `
                          : html`<span class="action-context-pill">${contextLabel('transition')}</span>`}
                        <div class="action-buttons">
                          <button type="button" class="icon-button" ?disabled=${index === 0} @click=${() => this._moveAction(index, -1)}>Move up</button>
                          <button type="button" class="icon-button" ?disabled=${index === this.actions.length - 1} @click=${() => this._moveAction(index, 1)}>Move down</button>
                          <button
                            type="button"
                            class="icon-button danger-button"
                            data-prism-stage-action-remove="${index}"
                            @click=${(event: Event) => this._openDeleteDialog(index, event.currentTarget as HTMLElement)}
                          >
                            Remove
                          </button>
                        </div>
                      </div>
                      ${this._renderActionParameters(index)}
                    </li>
                  `;
                })}
              </ol>
            `}
        ${this._renderPickerDialog()}
        ${this._renderDeleteDialog()}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    .section-header-row {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }

    .section-copy,
    .section-empty {
      margin: 0;
      color: #475569;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .section-meta,
    .action-context-pill {
      color: #475569;
      font-size: 0.8125rem;
      font-weight: 600;
    }

    .action-context-pill {
      display: inline-flex;
      align-items: center;
      min-height: 2.5rem;
      padding: 0 0.75rem;
      border-radius: 999px;
      background: #eff6ff;
      color: #1d4ed8;
    }

    .subsection-heading {
      margin: 0;
      font-size: 0.8125rem;
      font-weight: 700;
      color: #334155;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .secondary-button,
    .icon-button,
    .drag-button,
    .picker-option {
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #111827;
      font: inherit;
      cursor: pointer;
    }

    .secondary-button {
      min-height: 2.5rem;
      padding: 0.625rem 0.875rem;
      font-weight: 600;
    }

    .icon-button,
    .drag-button {
      min-height: 2.25rem;
      padding: 0.5rem 0.75rem;
      font-size: 0.875rem;
    }

    .drag-button {
      width: 2.25rem;
      height: 2.25rem;
      flex-shrink: 0;
      font-weight: 700;
    }

    .secondary-button:focus-visible,
    .icon-button:focus-visible,
    .drag-button:focus-visible,
    .picker-option:focus-visible,
    .field-control:focus-visible,
    .action-item:focus-visible,
    .dialog-button:focus-visible {
      outline: 3px solid #1d4ed8;
      outline-offset: 2px;
    }

    .action-list,
    .form-field-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.75rem;
    }

    .action-item,
    .form-field-item {
      display: grid;
      gap: 0.875rem;
      padding: 0.875rem;
      border: 1px solid #dbe2ea;
      border-radius: 12px;
      background: #f8fafc;
    }

    .action-item-selected {
      border-color: #1d70b8;
      box-shadow: inset 0 0 0 2px rgba(29, 112, 184, 0.16);
    }

    .action-item-drop {
      border-color: #1d4ed8;
      box-shadow: inset 0 0 0 2px rgba(29, 78, 216, 0.2);
    }

    .action-item-main {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
    }

    .action-copy {
      min-width: 0;
    }

    .action-title {
      margin: 0 0 0.25rem;
      color: #111827;
      font-weight: 700;
      font-size: 0.9375rem;
    }

    .action-summary {
      margin: 0;
      color: #475569;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .action-item-controls {
      display: grid;
      grid-template-columns: minmax(0, 13rem) 1fr;
      gap: 0.75rem;
      align-items: end;
    }

    .action-buttons {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      justify-content: flex-end;
    }

    .compact-field {
      margin: 0;
    }

    .action-parameters,
    .forms-editor {
      display: grid;
      gap: 0.75rem;
    }

    .field-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.875rem;
    }

    .field-block {
      display: grid;
      gap: 0.375rem;
      min-width: 0;
    }

    .field-block-full {
      grid-column: 1 / -1;
    }

    .field-label {
      font-size: 0.8125rem;
      font-weight: 700;
      color: #334155;
    }

    .field-label-row {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      flex-wrap: wrap;
    }

    .field-help {
      color: #475569;
      font-size: 0.75rem;
      line-height: 1.5;
    }

    .field-control {
      width: 100%;
      min-height: 2.5rem;
      padding: 0.625rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #111827;
      font: inherit;
      box-sizing: border-box;
    }

    .field-textarea {
      min-height: 5.5rem;
      resize: vertical;
    }

    .field-toggle {
      display: flex;
      align-items: center;
      gap: 0.625rem;
      min-height: 2.5rem;
      color: #111827;
      font-size: 0.875rem;
      font-weight: 600;
    }

    .field-control-error {
      border-color: #dc2626;
    }

    .field-error {
      color: #b91c1c;
      font-size: 0.8125rem;
    }

    .action-validation {
      padding: 0.75rem;
      border-radius: 10px;
      background: #fff7ed;
      color: #9a3412;
      font-size: 0.875rem;
    }

    .action-validation-title {
      margin: 0 0 0.5rem;
      font-weight: 700;
    }

    .action-validation ul {
      margin: 0;
      padding-left: 1rem;
      display: grid;
      gap: 0.25rem;
    }

    .dialog-backdrop {
      position: fixed;
      inset: 0;
      display: grid;
      place-items: center;
      padding: 1.5rem;
      background: rgba(15, 23, 42, 0.52);
      z-index: 10;
    }

    .dialog-panel {
      width: min(42rem, calc(100vw - 2rem));
      max-height: calc(100vh - 3rem);
      overflow: auto;
      padding: 1.25rem;
      border-radius: 16px;
      background: #ffffff;
      box-shadow: 0 24px 48px rgba(15, 23, 42, 0.24);
    }

    .dialog-panel-danger {
      border: 1px solid #fecaca;
    }

    .dialog-header {
      display: flex;
      justify-content: space-between;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }

    .dialog-eyebrow {
      margin: 0 0 0.25rem;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #1d4ed8;
    }

    .dialog-eyebrow.danger {
      color: #b91c1c;
    }

    .dialog-title {
      margin: 0;
      font-size: 1.125rem;
      font-weight: 700;
      color: #111827;
    }

    .dialog-copy {
      margin: 0 0 0.875rem;
      color: #475569;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .dialog-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 0.875rem;
      margin-bottom: 0.875rem;
    }

    .dialog-field {
      display: grid;
      gap: 0.375rem;
    }

    .dialog-label {
      font-size: 0.8125rem;
      font-weight: 700;
      color: #334155;
    }

    .dialog-control {
      width: 100%;
      min-height: 2.5rem;
      padding: 0.625rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 10px;
      background: #ffffff;
      color: #111827;
      font: inherit;
      box-sizing: border-box;
    }

    .picker-list {
      display: grid;
      gap: 0.625rem;
      margin-bottom: 0.875rem;
    }

    .picker-option {
      display: grid;
      gap: 0.25rem;
      padding: 0.875rem;
      text-align: left;
    }

    .picker-option.selected {
      border-color: #1d4ed8;
      box-shadow: inset 0 0 0 2px rgba(29, 78, 216, 0.12);
      background: #eff6ff;
    }

    .picker-option-title {
      font-weight: 700;
      color: #111827;
    }

    .picker-option-type,
    .picker-option-summary,
    .picker-option-meta {
      color: #475569;
      font-size: 0.8125rem;
      line-height: 1.5;
    }

    .dialog-actions {
      display: flex;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: 0.75rem;
    }

    .dialog-button {
      min-height: 2.5rem;
      padding: 0.625rem 0.875rem;
      border-radius: 10px;
      border: 1px solid #cbd5e1;
      background: #ffffff;
      color: #111827;
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }

    .dialog-button.primary {
      border-color: #1d4ed8;
      background: #1d4ed8;
      color: #ffffff;
    }

    .danger-button,
    .dialog-button.danger {
      border-color: #fecaca;
      color: #b91c1c;
      background: #fff5f5;
    }

    @media (max-width: 760px) {
      .section-header-row,
      .action-item-controls,
      .dialog-grid,
      .field-grid {
        grid-template-columns: 1fr;
      }

      .action-buttons,
      .dialog-actions {
        justify-content: flex-start;
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-stage-action-editor': PrismServiceBlueprintActionEditorElement;
  }
}
