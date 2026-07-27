// A serviceBlueprint's `definitionKey` is the identity other content references (a `cmsServiceBlueprintPage`'s
// own `blueprintKey` property) — unlike Umbraco's own entities (webhooks, etc.), which mint a
// random GUID and let the author name it afterwards, CMS Service Blueprint needs the key chosen upfront,
// deliberately, before anything is created. Mirrors PrismCreateTenantModalElement's own
// modalContext.submit()/reject() shape (the established pattern in this codebase).

import { LitElement, css, html } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { createExtensionApiByAlias } from '@umbraco-cms/backoffice/extension-registry';
import type { UmbDetailRepository } from '@umbraco-cms/backoffice/repository';
import { UMB_CMS_SERVICE_BLUEPRINT_DETAIL_REPOSITORY_ALIAS } from '../repository/detail/manifests.js';
import { UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE, UMB_CMS_SERVICE_BLUEPRINT_EDIT_PATH_PREFIX, type CmsServiceBlueprintEntityModel } from '../entity.js';

const DEFINITION_KEY_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

@customElement('prism-create-cms-service-blueprint-modal')
export class PrismCreateCmsServiceBlueprintModalElement extends UmbElementMixin(LitElement) {
  modalContext?: { submit: () => void; reject: () => void };

  @state() private _definitionKey = '';
  @state() private _displayName = '';
  @state() private _error: string | null = null;
  @state() private _saving = false;

  private get _keyError(): string | null {
    if (!this._definitionKey.trim()) return null;
    return DEFINITION_KEY_PATTERN.test(this._definitionKey)
      ? null
      : 'Use lowercase letters, numbers, and hyphens only (e.g. "apply-for-a-licence").';
  }

  private async _handleSubmit(event: Event) {
    event.preventDefault();
    if (!this._definitionKey.trim() || !this._displayName.trim() || this._keyError) {
      return;
    }

    this._saving = true;
    this._error = null;

    try {
      const repository = await createExtensionApiByAlias<UmbDetailRepository<CmsServiceBlueprintEntityModel>>(
        this,
        UMB_CMS_SERVICE_BLUEPRINT_DETAIL_REPOSITORY_ALIAS,
      );
      const model: CmsServiceBlueprintEntityModel = {
        entityType: UMB_CMS_SERVICE_BLUEPRINT_ENTITY_TYPE,
        unique: this._definitionKey,
        definitionKey: this._definitionKey,
        displayName: this._displayName,
      };
      const { error } = await repository.create(model, null);

      if (error) {
        this._error = error instanceof Error ? error.message : 'Failed to create the service blueprint.';
        return;
      }

      this.modalContext?.submit();
      window.location.href = `/umbraco/${UMB_CMS_SERVICE_BLUEPRINT_EDIT_PATH_PREFIX}${encodeURIComponent(this._definitionKey)}`;
    } catch (err) {
      this._error = err instanceof Error ? err.message : 'Failed to create the service blueprint.';
    } finally {
      this._saving = false;
    }
  }

  render() {
    return html`
      <uui-dialog-layout class="uui-text" headline="New service blueprint">
        <form id="create-form" @submit=${this._handleSubmit}>
          <uui-form-layout-item>
            <uui-label slot="label" for="displayName" required>Display name</uui-label>
            <uui-input
              id="displayName"
              .value=${this._displayName}
              required
              placeholder="Apply for a parking permit"
              @input=${(e: InputEvent) => (this._displayName = (e.target as HTMLInputElement).value)}
            ></uui-input>
          </uui-form-layout-item>

          <uui-form-layout-item>
            <uui-label slot="label" for="definitionKey" required>Definition key</uui-label>
            <span slot="description">
              A stable, url-safe identifier — used in the page's <code>blueprintKey</code> property and cannot
              be changed later without deleting and recreating the service blueprint.
            </span>
            <uui-input
              id="definitionKey"
              .value=${this._definitionKey}
              required
              placeholder="apply-for-a-parking-permit"
              @input=${(e: InputEvent) => (this._definitionKey = (e.target as HTMLInputElement).value)}
            ></uui-input>
            ${this._keyError ? html`<p class="field-error">${this._keyError}</p>` : ''}
          </uui-form-layout-item>

          ${this._error ? html`<p class="field-error" role="alert">${this._error}</p>` : ''}
        </form>

        <uui-button slot="actions" label="Cancel" @click=${() => this.modalContext?.reject()}></uui-button>
        <uui-button
          slot="actions"
          label="Create"
          look="primary"
          color="positive"
          .state=${this._saving ? 'waiting' : undefined}
          ?disabled=${this._saving || !this._definitionKey.trim() || !this._displayName.trim() || !!this._keyError}
          @click=${this._handleSubmit}
        ></uui-button>
      </uui-dialog-layout>
    `;
  }

  static styles = css`
    uui-dialog-layout {
      max-inline-size: 60ch;
    }

    uui-form-layout-item {
      margin-bottom: var(--uui-size-space-4, 1rem);
    }

    .field-error {
      color: var(--uui-color-danger, #d42054);
      font-size: 0.85rem;
      margin: 0.25rem 0 0;
    }
  `;
}

export default PrismCreateCmsServiceBlueprintModalElement;
