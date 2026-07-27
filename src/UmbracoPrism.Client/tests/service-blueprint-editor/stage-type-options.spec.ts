import { expect, test } from '@playwright/test';
// TODO Slice E: re-cert after gateway-pill rendering + simulation reshape. See .squad/decisions/inbox/copilot-slice-d-close-out.md.

function storyUrl(storyId: string): string {
  return `/iframe.html?id=${storyId}&viewMode=story`;
}

// Slice 3b.1 closed the TypeScript StageKind enum to match the C# enum. The
// editor-only `Waiting` and `StatusTimeline` kinds were retired (the server's
// PROJ140 validator rejects them on save). Tangy SHOULD-FIX #5 asks that the
// stage-type list no longer offers those values to authors.
test.describe('Retired stage types are no longer offered to authors', () => {
  test.fixme('author cannot pick a retired stage type (Waiting, StatusTimeline) from the list-view stage-type select', async ({ page }) => {
    await page.goto(storyUrl('service-blueprint-editor-service-blueprint-graph--workspace-canvas'));

    const graph = page.locator('prism-service-blueprint-graph');
    await expect(graph).toBeVisible({ timeout: 10_000 });

    // Switch to list view to expose per-row kind selects.
    await graph.getByRole('button', { name: 'List view' }).click();

    const kindSelectValues = await graph.evaluate(node => {
      const root = (node as HTMLElement).shadowRoot;
      const selects = Array.from(root?.querySelectorAll<HTMLSelectElement>('select') ?? []);
      const kindSelect = selects.find(sel => {
        const optionValues = Array.from(sel.options).map(o => o.value);
        return optionValues.includes('Question') && optionValues.includes('CheckAnswers');
      });
      return kindSelect ? Array.from(kindSelect.options).map(o => o.value) : null;
    });

    expect(kindSelectValues).not.toBeNull();
    expect(kindSelectValues).not.toContain('Waiting');
    expect(kindSelectValues).not.toContain('StatusTimeline');
    expect(kindSelectValues).toEqual(
      expect.arrayContaining(['Question', 'CheckAnswers', 'Confirmation', 'TaskList'])
    );
  });
});
