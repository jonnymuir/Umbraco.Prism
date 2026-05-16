import type { Meta, StoryObj } from '@storybook/web-components';
import { expect } from '@storybook/test';
import './prism-workflow-editor.js';
import type { PrismWorkflowEditorElement } from './prism-workflow-editor.js';
import { PLANNING_WORKFLOW } from './fixtures/index.js';

/**
 * Stubs window.fetch for authoring API URLs so stories work fully offline.
 * Called from each story's render function; the original fetch is restored
 * shortly after to avoid cross-story contamination.
 */
function stubFetchFor(el: PrismWorkflowEditorElement): void {
  const originalFetch = window.fetch;
  const API_RE = /\/api\/workflow-authoring\/workflows/;

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const urlStr =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : (input as Request).url;
    if (API_RE.test(urlStr)) {
      const method = (init?.method ?? 'GET').toUpperCase();
      if (method === 'GET')
        return new Response(JSON.stringify(PLANNING_WORKFLOW), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      if (method === 'POST') {
        const body = init?.body ? JSON.parse(init.body as string) : {};
        return new Response(JSON.stringify(body), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      return new Response(null, { status: 204 });
    }
    return originalFetch(input, init);
  };

  // Restore after story element is removed from the DOM
  const observer = new MutationObserver(() => {
    if (!document.contains(el)) {
      window.fetch = originalFetch;
      observer.disconnect();
    }
  });
  observer.observe(document.body, { childList: true, subtree: true });
}

function makeEditor(): PrismWorkflowEditorElement {
  const el = document.createElement('prism-workflow-editor') as PrismWorkflowEditorElement;
  // Inject the fixture directly — no API fetch needed
  el.initialWorkflow = PLANNING_WORKFLOW;
  el.workflowKey = 'planning-application';
  el.style.cssText = 'display: block; width: 1200px; height: 700px;';
  // Also stub fetch so preview/apply calls work offline if triggered
  stubFetchFor(el);
  return el;
}

const meta: Meta = {
  title: 'Workflow Editor/Editor Host',
  component: 'prism-workflow-editor',
  tags: ['autodocs'],
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-required-children', enabled: true },
          { id: 'aria-dialog-name', enabled: true },
        ],
      },
    },
    layout: 'fullscreen',
  },
  render: () => makeEditor(),
};

export default meta;
type Story = StoryObj;

export const PlanningWorkflow: Story = {
  name: 'Planning Workflow',
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 200));
    const el = canvasElement.querySelector('prism-workflow-editor') as PrismWorkflowEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;

    // Root container is present with correct test hooks
    const container = root.querySelector('[data-prism-component="workflow-editor"]');
    await expect(container).not.toBeNull();

    // Workflow name appears in the header
    const title = root.querySelector('.editor-title');
    await expect(title?.textContent?.trim()).toBe('Planning Application');

    // Graph panel is rendered
    const graph = root.querySelector('prism-workflow-graph');
    await expect(graph).not.toBeNull();

    // Inspector panel is rendered
    const inspector = root.querySelector('prism-step-inspector');
    await expect(inspector).not.toBeNull();

    // Conversation pane is rendered
    const conversation = root.querySelector('prism-conversation-pane');
    await expect(conversation).not.toBeNull();

    // Modal is NOT open by default
    const backdrop = root.querySelector('.modal-backdrop');
    await expect(backdrop).toBeNull();
  },
};

export const WithStageSelected: Story = {
  name: 'Stage Selected',
  render: () => {
    const el = makeEditor();
    // Trigger stage selection after upgrade
    requestAnimationFrame(async () => {
      await el.updateComplete;
      el.shadowRoot
        ?.querySelector('prism-workflow-graph')
        ?.dispatchEvent(
          new CustomEvent('stage-selected', {
            detail: { stageKey: 'declaration' },
            bubbles: true,
            composed: true,
          })
        );
    });
    return el;
  },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 300));
    const el = canvasElement.querySelector('prism-workflow-editor') as PrismWorkflowEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;
    const inspector = root.querySelector('prism-step-inspector');
    await expect(inspector).not.toBeNull();
  },
};

export const ModalOpen: Story = {
  name: 'Proposal Modal Open',
  render: () => {
    const el = makeEditor();
    requestAnimationFrame(async () => {
      await el.updateComplete;
      // Fire an nl-request that matches the V1 canned prompt
      el.shadowRoot
        ?.querySelector('prism-conversation-pane')
        ?.dispatchEvent(
          new CustomEvent('nl-request', {
            detail: { text: 'insert ID&V before submission' },
            bubbles: true,
            composed: true,
          })
        );
    });
    return el;
  },
  play: async ({ canvasElement }) => {
    await new Promise(r => setTimeout(r, 800));
    const el = canvasElement.querySelector('prism-workflow-editor') as PrismWorkflowEditorElement;
    await el.updateComplete;

    const root = el.shadowRoot!;

    // Verify the canvas container is still present
    const container = root.querySelector('[data-prism-component="workflow-editor"]');
    await expect(container).not.toBeNull();
  },
};
