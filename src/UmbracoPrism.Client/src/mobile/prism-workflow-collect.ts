// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.

import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type {
  FieldGroupRenderPayload,
  WorkflowAction,
  WorkflowProblem,
  FieldRenderPayload,
} from './workflow-api-client';

@customElement('prism-workflow-collect')
export class PrismWorkflowCollectElement extends LitElement {
  @property({ type: Array })
  fieldGroups: FieldGroupRenderPayload[] = [];

  @property({ type: Array })
  availableActions: WorkflowAction[] = [];

  @property({ type: Array })
  problems: WorkflowProblem[] = [];

  private _formValues: Record<string, unknown> = {};

  private _getFieldError(fieldKey: string): string | null {
    const problem = this.problems.find((p) => p.fieldKey === fieldKey);
    return problem?.message || null;
  }

  private _handleSubmit(event: Event): void {
    event.preventDefault();
    const form = event.target as HTMLFormElement;
    const formData = new FormData(form);

    const fieldValues: Record<string, unknown> = {};
    for (const [key, value] of formData.entries()) {
      // Handle checkboxes (always present with 'on' value if checked)
      if (value === 'on') {
        fieldValues[key] = true;
      } else {
        fieldValues[key] = value;
      }
    }

    // Store for re-rendering
    this._formValues = fieldValues;

    // Find which button was clicked
    const submitter = (event as SubmitEvent).submitter as HTMLButtonElement;
    const actionKey = submitter?.dataset.actionKey;

    if (!actionKey) return;

    this.dispatchEvent(
      new CustomEvent('action', {
        detail: { actionKey, fieldValues },
        bubbles: true,
        composed: true,
      })
    );
  }

  private _renderErrorSummary(): unknown {
    if (this.problems.length === 0) return null;

    return html`
      <div
        class="govuk-error-summary"
        role="alert"
        tabindex="-1"
        aria-labelledby="error-summary-title"
        data-module="govuk-error-summary"
      >
        <h2 class="govuk-error-summary__title" id="error-summary-title">There is a problem</h2>
        <div class="govuk-error-summary__body">
          <ul class="govuk-list govuk-error-summary__list">
            ${this.problems.map(
              (problem) => html`
                <li>
                  ${problem.fieldKey
                    ? html`<a href="#field-${problem.fieldKey}">${problem.message}</a>`
                    : html`<span>${problem.message}</span>`}
                </li>
              `
            )}
          </ul>
        </div>
      </div>
    `;
  }

  private _renderField(field: FieldRenderPayload): unknown {
    const hasError = this._getFieldError(field.fieldKey) !== null;
    const errorMessage = this._getFieldError(field.fieldKey);
    const describedBy: string[] = [];

    if (field.hint) {
      describedBy.push(`${field.fieldKey}-hint`);
    }
    if (hasError) {
      describedBy.push(`${field.fieldKey}-error`);
    }

    const fieldValue = this._formValues[field.fieldKey] ?? field.value ?? '';

    const commonAttrs = {
      id: `field-${field.fieldKey}`,
      name: field.fieldKey,
      'aria-describedby': describedBy.length > 0 ? describedBy.join(' ') : undefined,
      'aria-invalid': hasError ? 'true' : undefined,
      required: field.required,
    };

    let inputHtml: unknown;

    switch (field.fieldType) {
      case 'text':
        inputHtml = html`
          <input
            type="text"
            class="govuk-input${hasError ? ' govuk-input--error' : ''}"
            ...=${commonAttrs}
            .value=${fieldValue as string}
          />
        `;
        break;

      case 'number':
        inputHtml = html`
          <input
            type="number"
            class="govuk-input govuk-input--width-10${hasError ? ' govuk-input--error' : ''}"
            ...=${commonAttrs}
            .value=${fieldValue as string}
          />
        `;
        break;

      case 'date':
        inputHtml = html`
          <input
            type="date"
            class="govuk-input govuk-input--width-10${hasError ? ' govuk-input--error' : ''}"
            ...=${commonAttrs}
            .value=${fieldValue as string}
          />
        `;
        break;

      case 'currency':
        inputHtml = html`
          <div class="govuk-input__wrapper">
            <div class="govuk-input__prefix" aria-hidden="true">£</div>
            <input
              type="text"
              inputmode="decimal"
              class="govuk-input govuk-input--width-10${hasError ? ' govuk-input--error' : ''}"
              ...=${commonAttrs}
              .value=${fieldValue as string}
            />
          </div>
        `;
        break;

      case 'textarea':
        inputHtml = html`
          <textarea
            class="govuk-textarea${hasError ? ' govuk-textarea--error' : ''}"
            rows="5"
            ...=${commonAttrs}
            .value=${fieldValue as string}
          ></textarea>
        `;
        break;

      case 'select':
        inputHtml = html`
          <select
            class="govuk-select${hasError ? ' govuk-select--error' : ''}"
            ...=${commonAttrs}
            .value=${fieldValue as string}
          >
            <option value="">Select an option</option>
            ${(field.options || []).map(
              (option) => html`<option value="${option}">${option}</option>`
            )}
          </select>
        `;
        break;

      case 'radio':
        inputHtml = html`
          <div class="govuk-radios" data-module="govuk-radios">
            ${(field.options || []).map(
              (option) => html`
                <div class="govuk-radios__item">
                  <input
                    class="govuk-radios__input"
                    type="radio"
                    name="${field.fieldKey}"
                    id="field-${field.fieldKey}-${option}"
                    value="${option}"
                    ?checked=${fieldValue === option}
                    ?required=${field.required}
                  />
                  <label
                    class="govuk-label govuk-radios__label"
                    for="field-${field.fieldKey}-${option}"
                  >
                    ${option}
                  </label>
                </div>
              `
            )}
          </div>
        `;
        break;

      case 'checkbox':
        inputHtml = html`
          <div class="govuk-checkboxes" data-module="govuk-checkboxes">
            <div class="govuk-checkboxes__item">
              <input
                class="govuk-checkboxes__input"
                type="checkbox"
                id="field-${field.fieldKey}"
                name="${field.fieldKey}"
                ?checked=${fieldValue === true || fieldValue === 'true'}
              />
              <label class="govuk-label govuk-checkboxes__label" for="field-${field.fieldKey}">
                ${field.label}
              </label>
            </div>
          </div>
        `;
        // For checkbox, return early without wrapping label
        return html`
          <div class="govuk-form-group${hasError ? ' govuk-form-group--error' : ''}">
            ${field.hint
              ? html`<div class="govuk-hint" id="${field.fieldKey}-hint">${field.hint}</div>`
              : null}
            ${errorMessage
              ? html`
                  <p class="govuk-error-message" id="${field.fieldKey}-error">
                    <span class="govuk-visually-hidden">Error:</span> ${errorMessage}
                  </p>
                `
              : null}
            ${inputHtml}
          </div>
        `;

      default:
        inputHtml = html`<p>Unsupported field type: ${field.fieldType}</p>`;
    }

    return html`
      <div class="govuk-form-group${hasError ? ' govuk-form-group--error' : ''}">
        <label class="govuk-label" for="field-${field.fieldKey}">
          ${field.label}${field.required ? html`<span class="required-marker">*</span>` : null}
        </label>
        ${field.hint
          ? html`<div class="govuk-hint" id="${field.fieldKey}-hint">${field.hint}</div>`
          : null}
        ${errorMessage
          ? html`
              <p class="govuk-error-message" id="${field.fieldKey}-error">
                <span class="govuk-visually-hidden">Error:</span> ${errorMessage}
              </p>
            `
          : null}
        ${inputHtml}
      </div>
    `;
  }

  private _renderFieldGroup(group: FieldGroupRenderPayload): unknown {
    return html`
      <fieldset class="govuk-fieldset">
        <legend class="govuk-fieldset__legend govuk-fieldset__legend--m">
          <h2 class="govuk-fieldset__heading">${group.displayName}</h2>
        </legend>
        ${group.fields.map((field) => this._renderField(field))}
      </fieldset>
    `;
  }

  private _renderActions(): unknown {
    if (this.availableActions.length === 0) return null;

    return html`
      <div class="govuk-button-group">
        ${this.availableActions.map((action) => {
          let buttonClass = 'govuk-button';
          if (action.style === 'secondary') {
            buttonClass += ' govuk-button--secondary';
          } else if (action.style === 'destructive') {
            buttonClass += ' govuk-button--warning';
          }

          return html`
            <button
              type="submit"
              class="${buttonClass}"
              data-module="govuk-button"
              data-action-key="${action.actionKey}"
            >
              ${action.label}
            </button>
          `;
        })}
      </div>
    `;
  }

  render() {
    return html`
      <div class="workflow-collect">
        ${this._renderErrorSummary()}
        <form @submit=${this._handleSubmit} novalidate>
          ${this.fieldGroups.map((group) => this._renderFieldGroup(group))}
          ${this._renderActions()}
        </form>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }

    .workflow-collect {
      max-width: 640px;
    }

    /* GDS Error Summary */
    .govuk-error-summary {
      border: 5px solid #d4351c;
      padding: 15px 20px;
      margin-bottom: 30px;
    }

    .govuk-error-summary:focus {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
    }

    .govuk-error-summary__title {
      margin: 0 0 15px 0;
      font-size: 1.125rem;
      font-weight: 700;
      color: #d4351c;
    }

    .govuk-list {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .govuk-error-summary__list li {
      margin-bottom: 10px;
    }

    .govuk-error-summary__list a {
      color: #d4351c;
      font-weight: 700;
      text-decoration: underline;
    }

    /* GDS Fieldset */
    .govuk-fieldset {
      border: 0;
      margin: 0 0 30px 0;
      padding: 0;
      min-width: 0;
    }

    .govuk-fieldset__legend {
      margin-bottom: 10px;
    }

    .govuk-fieldset__legend--m {
      font-size: 1.125rem;
      font-weight: 700;
    }

    .govuk-fieldset__heading {
      margin: 0;
      font-size: inherit;
      font-weight: inherit;
    }

    /* GDS Form Group */
    .govuk-form-group {
      margin-bottom: 20px;
    }

    .govuk-form-group--error {
      padding-left: 15px;
      border-left: 5px solid #d4351c;
    }

    /* GDS Label */
    .govuk-label {
      display: block;
      margin-bottom: 5px;
      font-weight: 600;
      color: #0b0c0c;
    }

    .required-marker {
      color: #d4351c;
      margin-left: 2px;
    }

    /* GDS Hint */
    .govuk-hint {
      margin-bottom: 10px;
      color: #505a5f;
      font-size: 0.9375rem;
    }

    /* GDS Error Message */
    .govuk-error-message {
      margin-bottom: 10px;
      color: #d4351c;
      font-weight: 700;
      font-size: 0.9375rem;
    }

    .govuk-visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      margin: -1px;
      padding: 0;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    /* GDS Input */
    .govuk-input {
      width: 100%;
      padding: 5px;
      border: 2px solid #0b0c0c;
      font-size: 1rem;
      font-family: inherit;
      box-sizing: border-box;
      min-height: 44px;
    }

    .govuk-input:focus {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
      box-shadow: inset 0 0 0 2px;
    }

    .govuk-input--error {
      border-color: #d4351c;
    }

    .govuk-input--width-10 {
      max-width: 23ex;
    }

    .govuk-input__wrapper {
      display: flex;
      align-items: stretch;
    }

    .govuk-input__prefix {
      min-width: 40px;
      min-height: 44px;
      padding: 5px 10px;
      border: 2px solid #0b0c0c;
      border-right: 0;
      background: #f3f2f1;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      white-space: nowrap;
      cursor: default;
      flex: 0 0 auto;
    }

    .govuk-input__wrapper .govuk-input {
      flex: 1 1 auto;
    }

    /* GDS Textarea */
    .govuk-textarea {
      width: 100%;
      padding: 5px;
      border: 2px solid #0b0c0c;
      font-size: 1rem;
      font-family: inherit;
      box-sizing: border-box;
      min-height: 44px;
    }

    .govuk-textarea:focus {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
      box-shadow: inset 0 0 0 2px;
    }

    .govuk-textarea--error {
      border-color: #d4351c;
    }

    /* GDS Select */
    .govuk-select {
      width: 100%;
      padding: 5px;
      border: 2px solid #0b0c0c;
      font-size: 1rem;
      font-family: inherit;
      box-sizing: border-box;
      min-height: 44px;
    }

    .govuk-select:focus {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
      box-shadow: inset 0 0 0 2px;
    }

    .govuk-select--error {
      border-color: #d4351c;
    }

    /* GDS Radios */
    .govuk-radios {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .govuk-radios__item {
      display: flex;
      align-items: center;
      min-height: 44px;
      margin-bottom: 10px;
    }

    .govuk-radios__input {
      width: 44px;
      height: 44px;
      margin: 0 10px 0 0;
      cursor: pointer;
      flex-shrink: 0;
    }

    .govuk-radios__label {
      margin: 0;
      cursor: pointer;
    }

    /* GDS Checkboxes */
    .govuk-checkboxes {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .govuk-checkboxes__item {
      display: flex;
      align-items: center;
      min-height: 44px;
      margin-bottom: 10px;
    }

    .govuk-checkboxes__input {
      width: 44px;
      height: 44px;
      margin: 0 10px 0 0;
      cursor: pointer;
      flex-shrink: 0;
    }

    .govuk-checkboxes__label {
      margin: 0;
      cursor: pointer;
    }

    /* GDS Buttons */
    .govuk-button-group {
      margin-top: 30px;
      display: flex;
      gap: 15px;
      flex-wrap: wrap;
    }

    .govuk-button {
      font-size: 1rem;
      font-weight: 600;
      padding: 10px 20px;
      border: 2px solid transparent;
      background-color: #00703c;
      color: #fff;
      cursor: pointer;
      text-align: center;
      text-decoration: none;
      min-height: 44px;
      appearance: none;
      border-radius: 0;
    }

    .govuk-button:hover {
      background-color: #005a30;
    }

    .govuk-button:focus {
      outline: 3px solid #ffdd00;
      outline-offset: 0;
      box-shadow: inset 0 0 0 1px #0b0c0c;
      background-color: #00703c;
    }

    .govuk-button--secondary {
      background-color: #f3f2f1;
      color: #0b0c0c;
      box-shadow: 0 2px 0 #929191;
    }

    .govuk-button--secondary:hover {
      background-color: #dbdad9;
    }

    .govuk-button--warning {
      background-color: #d4351c;
      box-shadow: 0 2px 0 #942514;
    }

    .govuk-button--warning:hover {
      background-color: #aa2a16;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-workflow-collect': PrismWorkflowCollectElement;
  }
}
