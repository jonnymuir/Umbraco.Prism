// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.
// This component loads on every member-facing page view — keep it lean.
import { LitElement, html, css, svg, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { ifDefined } from 'lit/directives/if-defined.js';

interface NavItem {
  label: string;
  href: string;
  icon?: string;
  target?: string;
}

// Built-in icon set — banking/fintech/HR tab-bar staples (24×24 viewBox)
const ICONS: Record<string, string> = {
  home: 'M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z',
  dashboard: 'M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z',
  account:
    'M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 ' +
    '1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z',
  settings:
    'M12 15.5a3.5 3.5 0 0 1 0-7 3.5 3.5 0 0 1 0 7m7.43-2.07c.04-.32.07-.64.07-.93 ' +
    '0-.29-.03-.62-.07-.93l2-1.56c.18-.14.23-.41.12-.62l-1.9-3.28a.5.5 0 0 0-.6-.21l-2.36.95a6.6 ' +
    '6.6 0 0 0-1.59-.92L14.7 4.5a.49.49 0 0 0-.46-.41h-3.84a.49.49 0 0 0-.47.41l-.36 2.43a6.6 6.6 ' +
    '0 0 0-1.59.92l-2.36-.95a.5.5 0 0 0-.6.21L3.13 10.38c-.11.21-.06.48.12.62l2 1.56c-.04.31-.07.63' +
    '-.07.94s.03.62.07.93l-2 1.56c-.18.14-.23.41-.12.62l1.9 3.28c.12.21.37.28.6.21l2.36-.95c.49.38 ' +
    '1.01.7 1.59.92l.36 2.43c.04.24.22.41.47.41h3.84c.24 0 .43-.17.46-.41l.36-2.43a6.6 6.6 0 0 0 ' +
    '1.59-.92l2.36.95a.5.5 0 0 0 .6-.21l1.9-3.28a.5.5 0 0 0-.12-.62l-2-1.56z',
  transactions:
    'M20 4H4c-1.11 0-2 .9-2 2v12c0 1.1.89 2 2 2h16c1.11 0 2-.9 2-2V6c0-1.1-.89-2-2-2zm0 14H4v-6h16' +
    'v6zm0-10H4V6h16v2z',
  notifications:
    'M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5' +
    '-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z',
  more: 'M6 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm12 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2' +
    '-.9-2-2-2zm-6 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z',
};

/**
 * A glass-morphism mobile tab bar for Prism tenant portals.
 *
 * Usage:
 *   <prism-mobile-nav
 *     items='[{"label":"Home","href":"/","icon":"home"},
 *             {"label":"Account","href":"/account","icon":"/media/abc/account.svg"}]'
 *     current-path="/account">
 *   </prism-mobile-nav>
 *
 * The `icon` field accepts either:
 *   - A named built-in key: `home`, `dashboard`, `account`, `settings`,
 *     `transactions`, `notifications`, `more`
 *   - Any URL path: `/media/...`, `https://...`, `data:...`
 *     (renders as an `<img>` element)
 *
 * By default the host is `display:none`. The parent page is expected to apply:
 *   html.prism-mobile prism-mobile-nav { display: block; }
 *
 * CSS custom properties (all optional — sensible defaults ship out of the box):
 *   --prism-mobile-nav-bg               Background of the bar        (default: rgba(255,255,255,0.95))
 *   --prism-mobile-nav-border-color     Top-border colour             (default: rgba(0,0,0,0.08))
 *   --prism-mobile-nav-blur             Backdrop blur amount          (default: 20px)
 *   --prism-mobile-nav-z-index          Stack order                   (default: 1000)
 *   --prism-mobile-nav-gap              Gap between items             (default: 6px)
 *   --prism-mobile-nav-padding-h        Horizontal padding            (default: 12px)
 *   --prism-mobile-nav-padding-v        Vertical padding (top)        (default: 10px)
 *   --prism-mobile-nav-item-min-height  Minimum tap-target height     (default: 48px)
 *   --prism-mobile-nav-item-radius      Item border-radius            (default: 10px)
 *   --prism-mobile-nav-item-bg          Item background (inactive)    (default: transparent)
 *   --prism-mobile-nav-item-border      Item border colour (inactive) (default: transparent)
 *   --prism-mobile-nav-item-color       Item text/icon colour         (default: rgba(60,60,67,0.6))
 *   --prism-mobile-nav-item-hover-bg    Item hover background         (default: rgba(0,0,0,0.05))
 *   --prism-mobile-nav-item-hover-color Item hover colour             (default: rgba(60,60,67,0.85))
 *   --prism-mobile-nav-item-active-bg   Active item background        (default: transparent)
 *   --prism-mobile-nav-item-active-border Active item border colour   (default: transparent)
 *   --prism-mobile-nav-item-active-color  Active item text/icon color (default: var(--prism-primary,#007aff))
 *   --prism-mobile-nav-icon-size        SVG icon dimensions           (default: 22px)
 *   --prism-mobile-nav-label-size       Label font-size               (default: 11px)
 *   --prism-mobile-nav-label-weight     Label font-weight             (default: 500)
 *   --prism-mobile-nav-transition       Transition shorthand          (default: 200ms ease)
 */
@customElement('prism-mobile-nav')
export class PrismMobileNavElement extends LitElement {
  /** JSON-serialised array of NavItem objects. */
  @property({ type: String })
  items: string = '[]';

  /** Current page path used to determine which item is active. */
  @property({ type: String, attribute: 'current-path' })
  currentPath: string = '';

  /** Accessible label for the <nav> landmark. */
  @property({ type: String, attribute: 'nav-label' })
  navLabel: string = 'Mobile navigation';

  private get _items(): NavItem[] {
    try {
      const parsed = JSON.parse(this.items);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  private _isActive(href: string): boolean {
    if (!this.currentPath) return false;
    return href.toLowerCase() === this.currentPath.toLowerCase();
  }

  private _isIconUrl(icon: string): boolean {
    return icon.startsWith('/') || icon.startsWith('http') || icon.startsWith('data:');
  }

  private _renderIcon(iconName: string | undefined) {
    if (!iconName) return nothing;

    // Media library URL — render as <img>
    if (this._isIconUrl(iconName)) {
      return html`
        <img class="nav-icon nav-icon--img"
             src="${iconName}"
             alt=""
             aria-hidden="true"
             width="22"
             height="22" />
      `;
    }

    // Named built-in icon — look up SVG path
    const path = ICONS[iconName];
    if (!path) return nothing;
    return svg`
      <svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        <path d="${path}" fill="currentColor" />
      </svg>
    `;
  }

  render() {
    const items = this._items;
    return html`
      <nav role="navigation" aria-label="${this.navLabel}">
        ${items.map(item => {
          const active = this._isActive(item.href);
          return html`
            <a
              class="nav-item${active ? ' nav-item--active' : ''}"
              href="${item.href}"
              aria-current="${ifDefined(active ? 'page' : undefined)}"
              target="${ifDefined(item.target === '_blank' ? '_blank' : undefined)}"
              rel="${ifDefined(item.target === '_blank' ? 'noopener noreferrer' : undefined)}"
            >
              ${this._renderIcon(item.icon)}
              <span class="nav-label">${item.label}</span>
            </a>
          `;
        })}
      </nav>
    `;
  }

  static styles = css`
    /*
     * Host defaults to hidden. The outer page enables visibility via:
     *   html.prism-mobile prism-mobile-nav { display: block; }
     */
    :host {
      display: none;
      position: fixed;
      left: 0;
      right: 0;
      bottom: 0;
      z-index: var(--prism-mobile-nav-z-index, 1000);
    }

    nav {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(56px, 1fr));
      gap: var(--prism-mobile-nav-gap, 6px);
      padding:
        var(--prism-mobile-nav-padding-v, 10px)
        var(--prism-mobile-nav-padding-h, 12px)
        calc(var(--prism-mobile-nav-padding-v, 10px) + env(safe-area-inset-bottom));
      background: var(--prism-mobile-nav-bg, rgba(255, 255, 255, 0.95));
      border-top: 1px solid var(--prism-mobile-nav-border-color, rgba(0, 0, 0, 0.08));
      backdrop-filter: blur(var(--prism-mobile-nav-blur, 20px));
      -webkit-backdrop-filter: blur(var(--prism-mobile-nav-blur, 20px));
    }

    .nav-item {
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      gap: 4px;
      min-height: var(--prism-mobile-nav-item-min-height, 48px);
      padding: 6px 4px;
      border-radius: var(--prism-mobile-nav-item-radius, 10px);
      text-decoration: none;
      color: var(--prism-mobile-nav-item-color, rgba(60, 60, 67, 0.6));
      background: var(--prism-mobile-nav-item-bg, transparent);
      border: 1px solid var(--prism-mobile-nav-item-border, transparent);
      transition:
        color var(--prism-mobile-nav-transition, 200ms ease),
        background var(--prism-mobile-nav-transition, 200ms ease),
        border-color var(--prism-mobile-nav-transition, 200ms ease);
      cursor: pointer;
      -webkit-tap-highlight-color: transparent;
      outline-offset: 2px;
    }

    .nav-item:hover {
      color: var(--prism-mobile-nav-item-hover-color, rgba(60, 60, 67, 0.85));
      background: var(--prism-mobile-nav-item-hover-bg, rgba(0, 0, 0, 0.05));
    }

    .nav-item:focus-visible {
      outline: 2px solid var(--prism-mobile-nav-item-active-color, var(--prism-primary, #007aff));
    }

    .nav-item--active {
      color: var(--prism-mobile-nav-item-active-color, var(--prism-primary, #007aff));
      background: var(--prism-mobile-nav-item-active-bg, transparent);
      border-color: var(--prism-mobile-nav-item-active-border, transparent);
    }

    .nav-icon {
      width: var(--prism-mobile-nav-icon-size, 22px);
      height: var(--prism-mobile-nav-icon-size, 22px);
      flex-shrink: 0;
    }

    .nav-icon--img {
      width: var(--prism-mobile-nav-icon-size, 22px);
      height: var(--prism-mobile-nav-icon-size, 22px);
      object-fit: contain;
      flex-shrink: 0;
      /* Apply the same colour treatment for inactive state via CSS filter where possible.
         For SVG media files, editors should upload them in a neutral colour. */
      opacity: 0.6;
      transition: opacity var(--prism-mobile-nav-transition, 200ms ease);
    }

    .nav-item--active .nav-icon--img {
      opacity: 1;
    }

    .nav-item:hover .nav-icon--img {
      opacity: 0.85;
    }

    .nav-label {
      font-size: var(--prism-mobile-nav-label-size, 11px);
      font-weight: var(--prism-mobile-nav-label-weight, 500);
      line-height: 1;
      letter-spacing: 0.01em;
      white-space: nowrap;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'prism-mobile-nav': PrismMobileNavElement;
  }
}
