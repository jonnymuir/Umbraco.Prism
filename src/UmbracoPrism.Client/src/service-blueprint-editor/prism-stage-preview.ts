import { LitElement, css, html, nothing, type TemplateResult } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import type { AuthoredStage } from './types.js';
import type {
  ProjectedComponent,
  ProjectedFieldsetComponent,
  ProjectedInputComponent,
  ProjectedSummaryListComponent,
  ProjectedTaskListComponent,
  ProjectedWaitingComponent,
  ProjectedServiceBlueprintState,
  ProjectedServiceBlueprintTransition,
} from './service-request-runtime-projection.js';
import { humaniseAssignmentLabel } from './stage-assignment.js';

function assignmentCopy(projectedState: ProjectedServiceBlueprintState): string {
  const roleGates = projectedState.metadata?.roleGates?.filter(Boolean) ?? [];
  if (roleGates.length > 0) {
    return `Assigned to ${roleGates.map(humaniseAssignmentLabel).join(', ')}.`;
  }

  const actor = projectedState.metadata?.actor?.trim();
  if (actor) {
    return `Assigned to ${humaniseAssignmentLabel(actor)}.`;
  }

  return 'Assignment comes from the service blueprint definition.';
}

/**
 * @internal Composition detail of <prism-service-blueprint-editor>; not part of the public API surface.
 */
@customElement('prism-stage-preview')
export class PrismStagePreviewElement extends LitElement {
  @property({ attribute: false })
  stage: AuthoredStage | null = null;

  @property({ attribute: false })
  projectedState: ProjectedServiceBlueprintState | null = null;

  @property({ attribute: false })
  outgoingTransitions: ProjectedServiceBlueprintTransition[] = [];

  @property({ type: String })
  previewState: 'idle' | 'loading' | 'ready' | 'error' = 'idle';

  @property({ type: String })
  errorMessage = '';

  render() {
    const stage = this.stage;
    const projectedState = this.projectedState;
    const loading = this.previewState === 'loading';

    return html`
      <section class="preview-shell" aria-labelledby="serviceBlueprint-stage-preview-title" data-prism-stage-preview>
        <div class="preview-header">
          <div>
            <p class="preview-eyebrow">Preview and runtime format</p>
            <h2 id="serviceBlueprint-stage-preview-title" class="preview-title">Stage preview</h2>
            <p class="preview-summary">
              ${stage
                ? html`Showing <strong>${stage.displayName}</strong> as a read-only runtime preview.`
                : 'Select a stage to preview the projected runtime output.'}
            </p>
          </div>

        </div>

        ${!stage
          ? html`
              <div class="preview-empty" data-prism-preview-empty>
                <p class="govuk-body">Choose a stage in the workspace to preview the runtime shell, form fields, and next-step actions.</p>
              </div>
            `
          : nothing}

        ${loading
          ? html`
              <div class="preview-loading" role="status" aria-live="polite" data-prism-preview-loading>
                ${projectedState ? 'Updating preview…' : 'Rendering preview…'}
              </div>
            `
          : nothing}

        ${this.previewState === 'error' && !projectedState
          ? html`
              <div class="preview-error" role="alert" data-prism-preview-error>
                ${this.errorMessage || 'The runtime preview could not be rendered.'}
              </div>
            `
          : nothing}

        ${stage && projectedState
          ? this._renderPreviewSurface(projectedState)
          : nothing}
      </section>
    `;
  }

  private _renderPreviewSurface(projectedState: ProjectedServiceBlueprintState): TemplateResult {
    const shellLabel = shellLabelFor(projectedState);
    const formsEngine = projectedState.metadata?.actions?.some(action => action.type.startsWith('forms.'));
    const previewAssignment = assignmentCopy(projectedState);

    return html`
      <article class="preview-surface" data-prism-preview-surface-panel>
        <div class="preview-surface-header">
          <div>
            <p class="preview-surface-copy" data-prism-preview-assignment>${previewAssignment}</p>
            <h3 class="preview-stage-name" data-prism-preview-stage-name>${projectedState.displayName}</h3>
            ${projectedState.metadata?.description
              ? html`<p class="preview-stage-description">${projectedState.metadata.description}</p>`
              : nothing}
          </div>
          <div class="preview-meta">
            <span class="preview-chip" data-prism-preview-shell>${shellLabel}</span>
            ${formsEngine
              ? html`<span class="preview-chip preview-chip-muted">Forms engine</span>`
              : nothing}
            <span class="preview-chip preview-chip-muted" data-prism-preview-readonly>Read-only</span>
          </div>
        </div>

        <div class="preview-runtime">
          ${(projectedState.components ?? []).map(component => this._renderComponent(component))}
          ${this._renderActions(this.outgoingTransitions)}
        </div>
      </article>
    `;
  }

  private _renderComponent(component: ProjectedComponent): TemplateResult {
    switch (component.type) {
      case 'fieldset':
        return this._renderFieldset(component);
      case 'summary-list':
        return this._renderSummaryList(component);
      case 'accordion':
        return html`
          <div class="preview-accordion">
            ${component.sections.map(section => html`
              <section class="preview-accordion-section">
                <h4 class="govuk-heading-s">${section.heading}</h4>
                ${section.summary ? html`<p class="govuk-body-s preview-accordion-summary">${section.summary}</p>` : nothing}
                ${section.children.map(child => this._renderComponent(child))}
              </section>
            `)}
          </div>
        `;
      case 'panel':
        return html`
          <div class="govuk-panel govuk-panel--confirmation">
            <h3 class="govuk-panel__title">${component.heading}</h3>
          </div>
        `;
      case 'waiting':
        return this._renderWaiting(component);
      case 'task-list':
        return this._renderTaskList(component);
      case 'body':
        return html`<p class="govuk-body">${component.content}</p>`;
      case 'heading':
        return html`<h4 class="govuk-heading-m">${component.content}</h4>`;
      case 'inset-text':
        return html`<div class="govuk-inset-text">${component.content}</div>`;
      case 'warning-text':
        return html`
          <div class="govuk-warning-text">
            <span class="govuk-warning-text__icon" aria-hidden="true">!</span>
            <strong class="govuk-warning-text__text">${component.content}</strong>
          </div>
        `;
      case 'details':
        return html`
          <div class="govuk-details" role="group" aria-label=${component.heading || 'Additional information'}>
            <div class="govuk-details__summary">
              <span class="govuk-details__summary-text">${component.heading}</span>
            </div>
            <div class="govuk-details__text">${component.content}</div>
          </div>
        `;
      case 'notification-banner':
        return html`
          <div class="govuk-notification-banner" role="region" aria-label=${component.heading || 'Information'}>
            <div class="govuk-notification-banner__header">
              <h4 class="govuk-notification-banner__title">${component.heading || 'Information'}</h4>
            </div>
            <div class="govuk-notification-banner__content">
              <p class="govuk-body">${component.content}</p>
            </div>
          </div>
        `;
      default:
        return this._renderInput(component);
    }
  }

  private _renderFieldset(component: ProjectedFieldsetComponent): TemplateResult {
    return html`
      <fieldset class="govuk-fieldset preview-fieldset">
        ${component.legend
          ? html`
              <legend class=${`govuk-fieldset__legend govuk-fieldset__legend--${component.legendSize ?? 'm'}`}>
                ${component.legend}
              </legend>
            `
          : nothing}
        ${component.children.map(child => this._renderComponent(child))}
      </fieldset>
    `;
  }

  private _renderSummaryList(component: ProjectedSummaryListComponent): TemplateResult {
    const rows = component.children.filter(isProjectedInputComponent);

    return html`
      ${component.title ? html`<h4 class="govuk-heading-m">${component.title}</h4>` : nothing}
      <dl class="govuk-summary-list">
        ${rows.map(field => html`
          <div class="govuk-summary-list__row">
            <dt class="govuk-summary-list__key">${field.label}</dt>
            <dd class="govuk-summary-list__value">${summaryValueFor(field)}</dd>
            <dd class="govuk-summary-list__actions"><span class="preview-summary-action">Change</span></dd>
          </div>
        `)}
      </dl>
    `;
  }

  private _renderWaiting(component: ProjectedWaitingComponent): TemplateResult {
    return html`
      <div class="govuk-notification-banner" role="region" aria-label="Waiting">
        <div class="govuk-notification-banner__header">
          <h4 class="govuk-notification-banner__title">Information</h4>
        </div>
        <div class="govuk-notification-banner__content">
          <p class="govuk-body">${component.content || 'We are processing this stage.'}</p>
          ${component.expectedWaitSeconds > 0
            ? html`<p class="govuk-body">This usually takes about ${component.expectedWaitSeconds} seconds.</p>`
            : nothing}
        </div>
      </div>
    `;
  }

  private _renderTaskList(component: ProjectedTaskListComponent): TemplateResult {
    if (!component.sections?.length) {
      return html`<p class="govuk-body">No tasks available yet.</p>`;
    }

    return html`
      <ul class="govuk-task-list">
        ${component.sections.map(section => html`
          <li class="govuk-task-list__item govuk-task-list__item--header">
            <span class="govuk-heading-s govuk-!-margin-bottom-0">${section.heading}</span>
          </li>
          ${section.tasks.map(task => html`
            <li class="govuk-task-list__item govuk-task-list__item--with-link">
              <div class="govuk-task-list__name-and-hint">${task.label}</div>
              <div class="govuk-task-list__status"><strong class="govuk-tag govuk-tag--grey">Not started</strong></div>
            </li>
          `)}
        `)}
      </ul>
    `;
  }

  private _renderActions(transitions: ProjectedServiceBlueprintTransition[]): TemplateResult | typeof nothing {
    if (!transitions.length) {
      return nothing;
    }

    return html`
      <div class="govuk-button-group preview-actions">
        ${transitions.map(transition => html`
          <button
            type="button"
            class="govuk-button govuk-button--secondary"
            data-prism-preview-action=${transition.trigger}
            disabled
            title=${transitionSummary(transition)}
          >
            ${transition.trigger}
          </button>
        `)}
      </div>
    `;
  }

  private _renderInput(component: ProjectedInputComponent): TemplateResult {
    if (component.type === 'radio' || component.type === 'checkboxlist') {
      const itemClass = component.type === 'radio' ? 'govuk-radios' : 'govuk-checkboxes';
      const inputClass = component.type === 'radio' ? 'govuk-radios__input' : 'govuk-checkboxes__input';
      const labelClass = component.type === 'radio' ? 'govuk-radios__label' : 'govuk-checkboxes__label';
      const wrapperClass = component.type === 'radio' ? 'govuk-radios__item' : 'govuk-checkboxes__item';
      const inputType = component.type === 'radio' ? 'radio' : 'checkbox';

      return html`
        <div class="govuk-form-group">
          <fieldset class="govuk-fieldset">
            <legend class="govuk-fieldset__legend govuk-fieldset__legend--s">${component.label}</legend>
            ${component.hint ? html`<div class="govuk-hint">${component.hint}</div>` : nothing}
            <div class=${itemClass}>
              ${(component.options ?? []).map(option => html`
                <div class=${wrapperClass}>
                  <input class=${inputClass} type=${inputType} disabled />
                  <label class=${labelClass}>${option}</label>
                </div>
              `)}
            </div>
          </fieldset>
        </div>
      `;
    }

    if (component.type === 'boolean') {
      return html`
        <div class="govuk-form-group">
          <div class="govuk-checkboxes">
            <div class="govuk-checkboxes__item">
              <input class="govuk-checkboxes__input" type="checkbox" disabled />
              <label class="govuk-label govuk-checkboxes__label">${component.label}</label>
            </div>
          </div>
          ${component.hint ? html`<div class="govuk-hint">${component.hint}</div>` : nothing}
        </div>
      `;
    }

    const inputId = `preview-${component.fieldKey}`;

    return html`
      <div class="govuk-form-group">
        <label class="govuk-label govuk-label--s" for=${inputId}>${component.label}</label>
        ${component.hint ? html`<div class="govuk-hint">${component.hint}</div>` : nothing}
        ${component.type === 'textarea'
          ? html`<textarea id=${inputId} class="govuk-textarea" rows="5" disabled></textarea>`
          : component.type === 'select'
            ? html`
                <select id=${inputId} class="govuk-select" disabled>
                  <option>-- Select --</option>
                  ${(component.options ?? []).map(option => html`<option>${option}</option>`)}
                </select>
              `
            : html`
                <input
                  id=${inputId}
                  class="govuk-input"
                  type=${component.type === 'email'
                    ? 'email'
                    : component.type === 'date'
                      ? 'date'
                      : component.type === 'number' || component.type === 'decimal'
                        ? 'number'
                        : 'text'}
                  disabled
                />
              `}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      border-top: 2px solid #b1b4b6;
      background: #ffffff;
    }

    .preview-shell {
      display: grid;
      gap: 1rem;
      padding: 1rem;
    }

    .preview-header {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
    }

    .preview-eyebrow {
      margin: 0 0 0.25rem;
      color: #1d4ed8;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .preview-title {
      margin: 0;
      font-size: 1rem;
    }

    .preview-summary,
    .preview-stage-description,
    .preview-surface-copy,
    .preview-empty .govuk-body {
      margin: 0.35rem 0 0;
      color: #505a5f;
      font-size: 0.875rem;
      line-height: 1.5;
    }

    .preview-empty,
    .preview-error,
    .preview-loading {
      border-radius: 8px;
      padding: 0.875rem 1rem;
      font-size: 0.9375rem;
      line-height: 1.5;
    }

    .preview-empty {
      background: #f8f8f8;
      border: 1px dashed #b1b4b6;
    }

    .preview-error {
      background: #fbe9e7;
      color: #a42414;
      border: 1px solid #d4351c;
    }

    .preview-loading {
      background: #f0f4f9;
      color: #1d70b8;
      border: 1px solid #1d70b8;
    }

    .preview-surface {
      border: 1px solid #d8dde3;
      border-radius: 12px;
      overflow: hidden;
    }

    .preview-surface-header {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
      padding: 1rem;
      border-bottom: 1px solid #d8dde3;
      background: #eef4fb;
    }

    .preview-stage-name {
      margin: 0.25rem 0 0;
      font-size: 1.5rem;
      line-height: 1.2;
    }

    .preview-meta {
      display: inline-flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      justify-content: flex-end;
    }

    .preview-chip {
      display: inline-flex;
      align-items: center;
      border-radius: 999px;
      background: #003078;
      color: #ffffff;
      padding: 0.25rem 0.6rem;
      font-size: 0.75rem;
      font-weight: 700;
      white-space: nowrap;
    }

    .preview-chip-muted {
      background: #d8dde3;
      color: #0b0c0c;
    }

    .preview-runtime {
      display: grid;
      gap: 1rem;
      padding: 1rem;
      background: #ffffff;
    }

    .preview-accordion {
      display: grid;
      gap: 1rem;
    }

    .preview-accordion-section {
      padding: 1rem;
      border-radius: 8px;
      background: #f8f8f8;
      border: 1px solid #d8dde3;
    }

    .preview-accordion-summary {
      margin-top: -0.5rem;
      color: #505a5f;
    }

    .preview-fieldset,
    .preview-actions {
      margin: 0;
    }

    .preview-summary-action {
      color: #6f777b;
      text-decoration: underline;
    }

    .govuk-input[disabled],
    .govuk-select[disabled],
    .govuk-textarea[disabled] {
      background: #f8f8f8;
      color: #0b0c0c;
    }

    .preview-actions .govuk-button[disabled] {
      cursor: not-allowed;
      opacity: 1;
      background: #f3f2f1;
      color: #0b0c0c;
      border-color: #b1b4b6;
    }

    @media (max-width: 840px) {
      .preview-header,
      .preview-surface-header {
        flex-direction: column;
      }

      .preview-meta {
        justify-content: flex-start;
      }
    }
  `;
}

function isProjectedInputComponent(component: ProjectedComponent): component is ProjectedInputComponent {
  return [
    'text',
    'number',
    'decimal',
    'select',
    'radio',
    'checkboxlist',
    'date',
    'email',
    'textarea',
    'boolean',
  ].includes(component.type);
}

function summaryValueFor(component: ProjectedInputComponent): string {
  if (component.type === 'checkboxlist') {
    return 'No options selected';
  }

  if (component.type === 'boolean') {
    return 'Not answered';
  }

  return component.options?.[0] ?? 'Not answered';
}

function shellLabelFor(state: ProjectedServiceBlueprintState): string {
  const stageType = state.metadata?.stageType ?? '';
  switch (stageType) {
    case 'CheckAnswers':
      return 'Check answers shell';
    case 'Confirmation':
      return 'Confirmation shell';
    case 'TaskList':
      return 'Task list shell';
    case 'Question':
    default:
      return 'Question shell';
  }
}

function transitionSummary(route?: ProjectedServiceBlueprintTransition): string {
  const condition = route?.condition;
  return condition ? `Transition condition: ${condition}` : 'Read-only transition action';
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-stage-preview': PrismStagePreviewElement;
  }
}
