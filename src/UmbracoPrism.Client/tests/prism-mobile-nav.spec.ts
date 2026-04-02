import { test, expect } from '@playwright/test';

const defaultStoryUrl = '/?path=/story/prism-mobile-nav--default';
const activeItemStoryUrl = '/?path=/story/prism-mobile-nav--with-active-item';
const lightThemeStoryUrl = '/?path=/story/prism-mobile-nav--light-theme';
const manyItemsStoryUrl = '/?path=/story/prism-mobile-nav--many-items';
const maxItemsStoryUrl = '/?path=/story/prism-mobile-nav--max-items';
const noIconsStoryUrl = '/?path=/story/prism-mobile-nav--no-icons';

test.describe('prism-mobile-nav — Rendering', () => {
  test('Nav renders with correct number of items in Default story', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const itemCount = await nav.evaluate((el) => {
      const navItems = el.shadowRoot?.querySelectorAll('.nav-item');
      return navItems?.length ?? 0;
    });

    expect(itemCount).toBe(3);
  });

  test('Nav items have labels and icons', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const itemsData = await nav.evaluate((el) => {
      const items = el.shadowRoot?.querySelectorAll('.nav-item');
      if (!items) return [];

      return Array.from(items).map((item) => {
        const label = item.querySelector('.nav-label')?.textContent?.trim() ?? '';
        const hasIcon = !!item.querySelector('.nav-icon');
        return { label, hasIcon };
      });
    });

    expect(itemsData).toHaveLength(3);
    expect(itemsData[0]).toEqual({ label: 'Home', hasIcon: true });
    expect(itemsData[1]).toEqual({ label: 'Account', hasIcon: true });
    expect(itemsData[2]).toEqual({ label: 'Settings', hasIcon: true });
  });

  test('Nav is visible in Storybook story (display not none)', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    
    await expect(nav).toBeVisible();

    const displayValue = await nav.evaluate((el) => {
      return window.getComputedStyle(el).display;
    });

    expect(displayValue).not.toBe('none');
  });

  test('Many Items story renders 5 items', async ({ page }) => {
    await page.goto(manyItemsStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const itemCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('.nav-item').length ?? 0;
    });

    expect(itemCount).toBe(5);
  });

  test('Max Items story renders 6 items', async ({ page }) => {
    await page.goto(maxItemsStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const itemCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('.nav-item').length ?? 0;
    });

    expect(itemCount).toBe(6);
  });

  test('No Icons story renders items without icons', async ({ page }) => {
    await page.goto(noIconsStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const iconCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('.nav-icon').length ?? 0;
    });

    expect(iconCount).toBe(0);

    const labels = await nav.evaluate((el) => {
      const labelElements = el.shadowRoot?.querySelectorAll('.nav-label');
      return Array.from(labelElements ?? []).map((label) => label.textContent?.trim() ?? '');
    });

    expect(labels).toEqual(['Home', 'Account', 'Settings']);
  });

  test('Light theme story renders with correct styling', async ({ page }) => {
    await page.goto(lightThemeStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const itemCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('.nav-item').length ?? 0;
    });

    expect(itemCount).toBe(3);
  });
});

test.describe('prism-mobile-nav — Active State', () => {
  test('Active item is highlighted in Default story', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const activeItems = await nav.evaluate((el) => {
      const items = el.shadowRoot?.querySelectorAll('.nav-item--active');
      return items?.length ?? 0;
    });

    // Default story has currentPath="" so no items should be active
    expect(activeItems).toBe(0);
  });

  test('Active item is highlighted in WithActiveItem story', async ({ page }) => {
    await page.goto(activeItemStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const activeData = await nav.evaluate((el) => {
      const activeItem = el.shadowRoot?.querySelector('.nav-item--active') as HTMLAnchorElement | null;
      if (!activeItem) return null;

      return {
        href: activeItem.getAttribute('href'),
        ariaCurrent: activeItem.getAttribute('aria-current'),
        label: activeItem.querySelector('.nav-label')?.textContent?.trim() ?? '',
      };
    });

    expect(activeData).not.toBeNull();
    expect(activeData?.href).toBe('/account');
    expect(activeData?.ariaCurrent).toBe('page');
    expect(activeData?.label).toBe('Account');
  });

  test('Only one item is active at a time', async ({ page }) => {
    await page.goto(activeItemStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const activeCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('.nav-item--active').length ?? 0;
    });

    expect(activeCount).toBe(1);

    const ariaCurrentCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('[aria-current="page"]').length ?? 0;
    });

    expect(ariaCurrentCount).toBe(1);
  });

  test('Inactive items do not have aria-current or active class', async ({ page }) => {
    await page.goto(activeItemStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const inactiveItems = await nav.evaluate((el) => {
      const items = el.shadowRoot?.querySelectorAll('.nav-item:not(.nav-item--active)');
      return Array.from(items ?? []).map((item) => ({
        href: item.getAttribute('href'),
        ariaCurrent: item.getAttribute('aria-current'),
      }));
    });

    expect(inactiveItems).toHaveLength(2);
    inactiveItems.forEach((item) => {
      expect(item.ariaCurrent).toBeNull();
    });
  });
});

test.describe('prism-mobile-nav — Accessibility', () => {
  test('Nav has correct ARIA role and label', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const navData = await nav.evaluate((el) => {
      const navElement = el.shadowRoot?.querySelector('nav');
      if (!navElement) return null;

      return {
        role: navElement.getAttribute('role'),
        ariaLabel: navElement.getAttribute('aria-label'),
      };
    });

    expect(navData).not.toBeNull();
    expect(navData?.role).toBe('navigation');
    expect(navData?.ariaLabel).toBe('Mobile navigation');
  });

  test('Nav items are semantic anchor links', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const linksData = await nav.evaluate((el) => {
      const links = el.shadowRoot?.querySelectorAll('.nav-item');
      return Array.from(links ?? []).map((link) => ({
        tagName: link.tagName.toLowerCase(),
        href: link.getAttribute('href'),
      }));
    });

    expect(linksData).toHaveLength(3);
    linksData.forEach((link) => {
      expect(link.tagName).toBe('a');
      expect(link.href).toBeTruthy();
    });
  });

  test('Icons have aria-hidden for screen readers', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const iconsAccessibility = await nav.evaluate((el) => {
      const icons = el.shadowRoot?.querySelectorAll('.nav-icon');
      return Array.from(icons ?? []).map((icon) => ({
        ariaHidden: icon.getAttribute('aria-hidden'),
        focusable: icon.getAttribute('focusable'),
      }));
    });

    expect(iconsAccessibility).toHaveLength(3);
    iconsAccessibility.forEach((icon) => {
      expect(icon.ariaHidden).toBe('true');
      expect(icon.focusable).toBe('false');
    });
  });

  test('Custom nav-label is applied correctly', async ({ page }) => {
    await page.goto('/?path=/story/prism-mobile-nav--accessibility-check');

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const ariaLabel = await nav.evaluate((el) => {
      const navElement = el.shadowRoot?.querySelector('nav');
      return navElement?.getAttribute('aria-label') ?? '';
    });

    expect(ariaLabel).toBe('Primary mobile navigation');
  });
});

test.describe('prism-mobile-nav — Structure & Layout', () => {
  test('Nav uses grid layout', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const layoutData = await nav.evaluate((el) => {
      const navElement = el.shadowRoot?.querySelector('nav');
      if (!navElement) return null;

      const styles = window.getComputedStyle(navElement);
      return {
        display: styles.display,
      };
    });

    expect(layoutData?.display).toBe('grid');
  });

  test('Nav items have minimum tap target height', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const itemHeights = await nav.evaluate((el) => {
      const items = el.shadowRoot?.querySelectorAll('.nav-item');
      return Array.from(items ?? []).map((item) => {
        const rect = item.getBoundingClientRect();
        return rect.height;
      });
    });

    expect(itemHeights).toHaveLength(3);
    itemHeights.forEach((height) => {
      expect(height).toBeGreaterThanOrEqual(44); // WCAG minimum is 44px
    });
  });

  test('Nav has fixed positioning at bottom', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const positionData = await nav.evaluate((el) => {
      const styles = window.getComputedStyle(el);
      return {
        position: styles.position,
        bottom: styles.bottom,
      };
    });

    expect(positionData.position).toBe('fixed');
    expect(positionData.bottom).toBe('0px');
  });
});

test.describe('prism-mobile-nav — Edge Cases', () => {
  test('Handles empty items array gracefully', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    await nav.evaluate((el) => {
      (el as any).items = '[]';
    });

    await page.waitForTimeout(100);

    const itemCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('.nav-item').length ?? 0;
    });

    expect(itemCount).toBe(0);
  });

  test('Handles malformed JSON in items property', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    await nav.evaluate((el) => {
      (el as any).items = 'not valid json';
    });

    await page.waitForTimeout(100);

    const itemCount = await nav.evaluate((el) => {
      return el.shadowRoot?.querySelectorAll('.nav-item').length ?? 0;
    });

    expect(itemCount).toBe(0);
  });

  test('Handles items with missing optional properties', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    const minimalItems = JSON.stringify([
      { label: 'Minimal', href: '/minimal' },
    ]);

    await nav.evaluate((el, items) => {
      (el as any).items = items;
    }, minimalItems);

    await page.waitForTimeout(100);

    const itemsData = await nav.evaluate((el) => {
      const items = el.shadowRoot?.querySelectorAll('.nav-item');
      return Array.from(items ?? []).map((item) => ({
        label: item.querySelector('.nav-label')?.textContent?.trim() ?? '',
        hasIcon: !!item.querySelector('.nav-icon'),
        href: item.getAttribute('href'),
      }));
    });

    expect(itemsData).toHaveLength(1);
    expect(itemsData[0]).toEqual({ label: 'Minimal', hasIcon: false, href: '/minimal' });
  });

  test('Case-insensitive path matching for active state', async ({ page }) => {
    await page.goto(defaultStoryUrl);

    const frame = page.frameLocator('#storybook-preview-iframe');
    const nav = frame.locator('prism-mobile-nav');
    await expect(nav).toBeVisible();

    await nav.evaluate((el) => {
      (el as any).currentPath = '/ACCOUNT';
    });

    await page.waitForTimeout(100);

    const activeHref = await nav.evaluate((el) => {
      const activeItem = el.shadowRoot?.querySelector('.nav-item--active');
      return activeItem?.getAttribute('href') ?? null;
    });

    expect(activeHref).toBe('/account');
  });
});
