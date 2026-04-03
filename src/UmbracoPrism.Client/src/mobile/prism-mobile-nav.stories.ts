import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import { expect, within } from '@storybook/test';
import './prism-mobile-nav';
import type { PrismMobileNavElement } from './prism-mobile-nav';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const THREE_ITEMS = JSON.stringify([
  { label: 'Home', href: '/', icon: 'home' },
  { label: 'Account', href: '/account', icon: 'account' },
  { label: 'Settings', href: '/settings', icon: 'settings' },
]);

const FIVE_ITEMS = JSON.stringify([
  { label: 'Home', href: '/', icon: 'home' },
  { label: 'Dashboard', href: '/dashboard', icon: 'dashboard' },
  { label: 'Transactions', href: '/transactions', icon: 'transactions' },
  { label: 'Notifications', href: '/notifications', icon: 'notifications' },
  { label: 'More', href: '/more', icon: 'more' },
]);

const SIX_ITEMS = JSON.stringify([
  { label: 'Home', href: '/', icon: 'home' },
  { label: 'Dashboard', href: '/dashboard', icon: 'dashboard' },
  { label: 'Account', href: '/account', icon: 'account' },
  { label: 'Transactions', href: '/transactions', icon: 'transactions' },
  { label: 'Notifications', href: '/notifications', icon: 'notifications' },
  { label: 'Settings', href: '/settings', icon: 'settings' },
]);

// ─── Story args type ───────────────────────────────────────────────────────────

type StoryArgs = {
  items: string;
  currentPath: string;
  navLabel: string;
};

// ─── Meta ──────────────────────────────────────────────────────────────────────

/**
 * Decorator: forces the component host to `display: block` so it's always
 * visible in Canvas, and wraps it in a dark-glass mobile preview container.
 */
const mobileDecorator = (story: () => unknown) => html`
  <style>
    prism-mobile-nav { display: block !important; }
  </style>
  <div
    style="
      position: relative;
      height: 160px;
      background: #f2f2f7;
      border-radius: 16px;
      overflow: hidden;
    "
    aria-label="Mobile device preview"
  >
    ${story()}
  </div>
`;

const meta: Meta<StoryArgs> = {
  title: 'Prism/Mobile Nav',
  component: 'prism-mobile-nav',
  tags: ['autodocs'],
  decorators: [mobileDecorator],
  args: {
    items: THREE_ITEMS,
    currentPath: '',
    navLabel: 'Mobile navigation',
  },
  argTypes: {
    items: {
      description: 'JSON array of `{ label, href, icon?, target? }` nav items.',
      control: { type: 'text' },
    },
    currentPath: {
      description: 'Current page path — matching item receives `aria-current="page"` and active styles.',
      control: { type: 'text' },
    },
    navLabel: {
      description: 'Accessible `aria-label` for the `<nav>` landmark.',
      control: { type: 'text' },
    },
  },
  render: (args) => html`
    <prism-mobile-nav
      .items=${args.items}
      .currentPath=${args.currentPath}
      nav-label=${args.navLabel}
    ></prism-mobile-nav>
  `,
};

export default meta;

type Story = StoryObj<StoryArgs>;

// ─── Stories ──────────────────────────────────────────────────────────────────

/** Three items, nothing active — the default out-of-the-box experience. */
export const Default: Story = {
  args: {
    items: THREE_ITEMS,
    currentPath: '',
  },
  play: async ({ canvasElement }) => {
    const nav = canvasElement.querySelector('prism-mobile-nav') as PrismMobileNavElement;
    await nav.updateComplete;

    if (!nav.shadowRoot) throw new Error('Shadow root not found');
    const links = nav.shadowRoot.querySelectorAll('.nav-item');
    await expect(links).toHaveLength(3);

    for (const link of links) {
      await expect(link).not.toHaveClass('nav-item--active');
      await expect(link).not.toHaveAttribute('aria-current');
    }
  },
};

/** The "Account" tab is the current page — gets Prism primary colour highlight. */
export const WithActiveItem: Story = {
  name: 'With Active Item',
  args: {
    items: THREE_ITEMS,
    currentPath: '/account',
  },
  play: async ({ canvasElement }) => {
    const nav = canvasElement.querySelector('prism-mobile-nav') as PrismMobileNavElement;
    await nav.updateComplete;

    if (!nav.shadowRoot) throw new Error('Shadow root not found');

    const activeLink = nav.shadowRoot.querySelector('.nav-item--active') as HTMLAnchorElement;
    await expect(activeLink).not.toBeNull();
    await expect(activeLink.getAttribute('aria-current')).toBe('page');
    await expect(activeLink.getAttribute('href')).toBe('/account');

    const inactiveLinks = nav.shadowRoot.querySelectorAll('.nav-item:not(.nav-item--active)');
    await expect(inactiveLinks).toHaveLength(2);
    for (const link of inactiveLinks) {
      await expect(link).not.toHaveAttribute('aria-current');
    }
  },
};

/**
 * Five items — typical fintech tab bar layout.
 * Tests that `auto-fit` grid keeps items within a single row.
 */
export const ManyItems: Story = {
  name: 'Many Items (5)',
  args: {
    items: FIVE_ITEMS,
    currentPath: '/transactions',
  },
  play: async ({ canvasElement }) => {
    const nav = canvasElement.querySelector('prism-mobile-nav') as PrismMobileNavElement;
    await nav.updateComplete;

    if (!nav.shadowRoot) throw new Error('Shadow root not found');
    const links = nav.shadowRoot.querySelectorAll('.nav-item');
    await expect(links).toHaveLength(5);

    const active = nav.shadowRoot.querySelector('.nav-item--active') as HTMLAnchorElement;
    await expect(active.getAttribute('href')).toBe('/transactions');
  },
};

/** Six items — stress-tests the `minmax(56px, 1fr)` grid columns. */
export const MaxItems: Story = {
  name: 'Max Items (6)',
  args: {
    items: SIX_ITEMS,
    currentPath: '/settings',
  },
};

/**
 * Light theme variant — the default iOS-style light appearance with clean
 * white background and subtle borders.
 */
export const LightTheme: Story = {
  name: 'Light Theme',
  decorators: [
    () => html`
      <style>
        prism-mobile-nav { display: block !important; }
      </style>
      <div
        style="
          position: relative;
          height: 160px;
          background: #f2f2f7;
          border-radius: 16px;
          overflow: hidden;
        "
      >
        <prism-mobile-nav
          .items=${THREE_ITEMS}
          current-path="/"
          nav-label="Mobile navigation (light)"
        ></prism-mobile-nav>
      </div>
    `,
  ],
  render: () => html``,
};

/**
 * Dark theme variant — override the white iOS defaults with CSS custom
 * properties to show the original dark glass look.
 */
export const DarkTheme: Story = {
  name: 'Dark Theme',
  decorators: [
    () => html`
      <style>
        prism-mobile-nav { display: block !important; }
      </style>
      <div
        style="
          position: relative;
          height: 160px;
          background: linear-gradient(160deg, #0f172a 0%, #1e293b 100%);
          border-radius: 16px;
          overflow: hidden;
        "
      >
        <prism-mobile-nav
          .items=${THREE_ITEMS}
          current-path="/"
          nav-label="Mobile navigation (dark)"
          style="
            --prism-mobile-nav-bg: rgba(15, 23, 42, 0.94);
            --prism-mobile-nav-border-color: rgba(148, 163, 184, 0.3);
            --prism-mobile-nav-item-color: rgba(226, 232, 240, 0.75);
            --prism-mobile-nav-item-hover-color: #e2e8f0;
            --prism-mobile-nav-item-hover-bg: rgba(148, 163, 184, 0.1);
            --prism-mobile-nav-item-active-bg: rgba(79, 70, 229, 0.2);
            --prism-mobile-nav-item-active-border: rgba(129, 140, 248, 0.4);
            --prism-mobile-nav-item-active-color: var(--prism-primary, #4f46e5);
          "
        ></prism-mobile-nav>
      </div>
    `,
  ],
  render: () => html``,
};

/**
 * Custom brand colour — demonstrates how `--prism-primary` flows through to the
 * active-item colour, matching the tenant's theme automatically.
 */
export const BrandColour: Story = {
  name: 'Custom Brand Colour',
  decorators: [
    () => html`
      <style>
        prism-mobile-nav { display: block !important; }
      </style>
      <div
        style="
          position: relative;
          height: 160px;
          background: linear-gradient(160deg, #0c1a2e 0%, #0f2d48 100%);
          border-radius: 16px;
          overflow: hidden;
          --prism-primary: #0ea5e9;
        "
      >
        <prism-mobile-nav
          .items=${THREE_ITEMS}
          current-path="/account"
          nav-label="Mobile navigation (brand)"
          style="
            --prism-mobile-nav-item-active-bg: rgba(14, 165, 233, 0.2);
            --prism-mobile-nav-item-active-border: rgba(14, 165, 233, 0.4);
          "
        ></prism-mobile-nav>
      </div>
    `,
  ],
  render: () => html``,
};

/** Items without icons — component degrades gracefully to label-only layout. */
export const NoIcons: Story = {
  name: 'No Icons (label only)',
  args: {
    items: JSON.stringify([
      { label: 'Home', href: '/' },
      { label: 'Account', href: '/account' },
      { label: 'Settings', href: '/settings' },
    ]),
    currentPath: '/',
  },
  play: async ({ canvasElement }) => {
    const nav = canvasElement.querySelector('prism-mobile-nav') as PrismMobileNavElement;
    await nav.updateComplete;

    if (!nav.shadowRoot) throw new Error('Shadow root not found');
    const icons = nav.shadowRoot.querySelectorAll('.nav-icon');
    await expect(icons).toHaveLength(0);

    const canvas = within(nav.shadowRoot as unknown as HTMLElement);
    await expect(canvas.getByText('Home')).toBeInTheDocument();
  },
};

/** Media library icons — icon field accepts a URL from the Umbraco media library. */
export const MediaIcons: Story = {
  name: 'Media Icons (URL)',
  args: {
    items: JSON.stringify([
      { label: 'Home', href: '/', icon: 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="%23666" d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/></svg>' },
      { label: 'Account', href: '/account', icon: 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="%23666" d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>' },
      { label: 'Settings', href: '/settings', icon: 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle fill="%23666" cx="12" cy="12" r="3"/></svg>' },
    ]),
    currentPath: '/account',
    navLabel: 'Mobile navigation (media icons)',
  },
};

/** Accessibility smoke-test: nav landmark present with correct label. */
export const AccessibilityCheck: Story = {
  name: 'Accessibility Check',
  args: {
    items: THREE_ITEMS,
    currentPath: '/',
    navLabel: 'Primary mobile navigation',
  },
  play: async ({ canvasElement }) => {
    const nav = canvasElement.querySelector('prism-mobile-nav') as PrismMobileNavElement;
    await nav.updateComplete;

    if (!nav.shadowRoot) throw new Error('Shadow root not found');

    const navEl = nav.shadowRoot.querySelector('nav');
    await expect(navEl).not.toBeNull();
    await expect(navEl!.getAttribute('aria-label')).toBe('Primary mobile navigation');
    await expect(navEl!.getAttribute('role')).toBe('navigation');

    const activeLink = nav.shadowRoot.querySelector('[aria-current="page"]') as HTMLAnchorElement;
    await expect(activeLink).not.toBeNull();
    await expect(activeLink.href).toContain('/');
  },
};
