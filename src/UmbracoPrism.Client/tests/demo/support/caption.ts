import type { Page } from '@playwright/test';

// Playwright's video recording captures whatever's actually rendered on the page, so a caption
// bar injected via page.evaluate() is captured exactly like any other UI element — this works
// identically on Umbraco backoffice pages, TestSite, the workflow editor, and the ttyd terminal
// page, since it's just DOM injection into whatever page is currently open.
export async function showCaption(page: Page, text: string, holdMs = 2200): Promise<void> {
  await page.evaluate(t => {
    let bar = document.getElementById('demo-caption');
    if (!bar) {
      bar = document.createElement('div');
      bar.id = 'demo-caption';
      Object.assign(bar.style, {
        position: 'fixed',
        left: '50%',
        bottom: '6%',
        transform: 'translateX(-50%)',
        maxWidth: '80%',
        background: 'rgba(0,0,0,.72)',
        color: '#fff',
        font: '15px system-ui, sans-serif',
        padding: '8px 16px',
        borderRadius: '4px',
        zIndex: '999999',
        textAlign: 'center'
      } satisfies Partial<CSSStyleDeclaration>);
      document.body.appendChild(bar);
    }
    bar.textContent = t;
  }, text);
  await page.waitForTimeout(holdMs);
}

export async function clearCaption(page: Page): Promise<void> {
  await page.evaluate(() => {
    document.getElementById('demo-caption')?.remove();
  });
}
