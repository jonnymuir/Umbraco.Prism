import { css } from 'lit';

export const tenantModalStyles = css`
  :host {
    display: block;
    width: 700px;
    height: 100%;
    min-height: 550px;
    background-color: var(--uui-color-surface);
    position: relative;
    resize: both;
    overflow: auto;
    max-width: 95vw;
    max-height: 90vh;
  }
  :host(.maximized) {
    position: fixed !important;
    inset: 0 !important;
    width: 100vw !important;
    height: 100vh !important;
    max-width: 100vw !important;
    max-height: 100vh !important;
    resize: none !important;
    z-index: 10000;
    border-radius: 0;
    overflow-y: auto;
  }
  .dialog-headline {
    display: flex;
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    gap: var(--uui-size-space-3, 9px);
    flex-shrink: 0;
    padding: var(--uui-size-space-3, 9px) 0;
    background: var(--uui-color-surface);
  }
  .dialog-headline-actions {
    display: flex;
    flex-direction: row;
    gap: 8px;
    align-items: center;
  }
  .dialog-headline-icons {
    display: flex;
    flex-direction: row;
    gap: 6px;
    align-items: center;
    flex-shrink: 0;
  }
  .dialog-action-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background: none;
    border: 1px solid var(--uui-color-border-standalone, #c2c2c2);
    cursor: pointer;
    padding: 0 var(--uui-size-space-4, 12px);
    height: 30px;
    font-size: var(--uui-type-small-size, 13px);
    font-family: inherit;
    color: var(--uui-color-text, #060606);
    border-radius: var(--uui-border-radius, 3px);
    transition: background-color 0.1s, border-color 0.1s, color 0.1s;
  }
  .dialog-action-btn:hover {
    background-color: var(--uui-color-surface-emphasis, rgba(0, 0, 0, 0.06));
  }
  .dialog-action-btn:focus-visible {
    outline: 2px solid var(--uui-color-focus, #3879d9);
    outline-offset: 1px;
  }
  .dialog-action-btn--primary {
    background-color: var(--uui-color-positive, #2bc37b);
    border-color: var(--uui-color-positive, #2bc37b);
    color: var(--uui-color-positive-contrast, #fff);
  }
  .dialog-action-btn--primary:hover {
    background-color: var(--uui-color-positive-emphasis, #27a96b);
    border-color: var(--uui-color-positive-emphasis, #27a96b);
  }
  .dialog-icon-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background: none;
    border: none;
    cursor: pointer;
    padding: 4px;
    color: var(--uui-color-text-alt, #605e5c);
    border-radius: var(--uui-border-radius, 3px);
    line-height: 0;
    transition: background-color 0.1s, color 0.1s;
  }
  .dialog-icon-btn:hover {
    background-color: var(--uui-color-surface-emphasis, rgba(0, 0, 0, 0.06));
    color: var(--uui-color-text, #060606);
  }
  .dialog-icon-btn:focus-visible {
    outline: 2px solid var(--uui-color-focus, #3879d9);
    outline-offset: 1px;
  }
  uui-tab-group {
    position: sticky;
    top: 0;
    z-index: 10;
    background: var(--uui-color-surface);
    border-bottom: 1px solid var(--uui-color-border-standalone);
  }
  .container { 
    min-height: 350px;
  }
  .override-input {
    width: 100%;
  }
  .image-picker {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }
  .image-picker__preview {
    max-height: 60px;
    max-width: 100%;
    object-fit: cover;
    border-radius: 4px;
    border: 1px solid var(--uui-color-border);
  }
  .image-picker__actions {
    display: flex;
    gap: 0.5rem;
    align-items: center;
    flex-wrap: wrap;
  }
  .field { 
    display: flex;
    flex-direction: column;
    margin-bottom: var(--uui-size-space-5); 
  }
  uui-label { 
    margin-bottom: var(--uui-size-space-2); 
    font-weight: bold; 
  }
  uui-input { width: 100%; }
  .description { 
    color: var(--uui-color-text-alt); 
    margin-bottom: var(--uui-size-space-5); 
    font-size: 0.9rem;
  }
  small { 
    margin-top: var(--uui-size-space-2); 
    color: var(--uui-color-text-alt); 
  }
  .section-divider {
    height: 1px;
    background: var(--uui-color-border);
    margin: var(--uui-size-space-6) 0 var(--uui-size-space-5);
  }
  .help-text {
    color: var(--uui-color-text-alt);
    margin-bottom: var(--uui-size-space-4);
    font-size: 0.85rem;
  }
  h3 {
    margin: 0 0 var(--uui-size-space-3);
    font-size: 1rem;
    font-weight: 600;
  }
  .helper-actions {
    display: flex;
    gap: var(--uui-size-space-3);
    margin-bottom: var(--uui-size-space-4);
    flex-wrap: wrap;
  }
  .mobile-asset-preview {
    margin-top: var(--uui-size-space-2);
    max-width: 160px;
    max-height: 120px;
    border: 1px solid var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface-alt);
    object-fit: contain;
    padding: 6px;
  }
  .error-text {
    color: var(--uui-color-danger-standalone);
  }
  .info-banner {
    background: var(--uui-color-surface-alt);
    border: 1px solid var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    padding: var(--uui-size-space-3) var(--uui-size-space-4);
    font-size: 0.85rem;
    color: var(--uui-color-text-alt);
  }
  .token-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.85rem;
    margin-top: var(--uui-size-space-3);
  }
  .token-table th {
    text-align: left;
    padding: 0.35rem 0.6rem;
    background: var(--uui-color-surface-alt);
    border-bottom: 2px solid var(--uui-color-border);
    font-weight: 600;
  }
  .token-table td {
    padding: 0.35rem 0.6rem;
    border-bottom: 1px solid var(--uui-color-border-standalone);
    vertical-align: middle;
  }
  .token-badge {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.15rem 0.5rem;
    border-radius: 999px;
    font-size: 0.78rem;
    font-weight: 600;
  }
  .token-badge--ok {
    background: color-mix(in srgb, var(--uui-color-positive) 15%, transparent);
    color: var(--uui-color-positive-standalone);
  }
  .token-badge--missing {
    background: color-mix(in srgb, var(--uui-color-warning) 15%, transparent);
    color: var(--uui-color-warning-standalone);
  }
  .resolved-value {
    max-width: 220px;
    display: inline-block;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    vertical-align: bottom;
  }
  .token-missing {
    color: var(--uui-color-text-alt);
  }
  .token-snippet {
    background: var(--uui-color-surface-alt);
    border: 1px solid var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    padding: var(--uui-size-space-3) var(--uui-size-space-4);
    font-family: monospace;
    font-size: 0.82rem;
    white-space: pre;
    overflow-x: auto;
    margin: 0.4rem 0 0.75rem;
  }
  .missing-tokens-hint {
    margin-top: var(--uui-size-space-4);
    padding: var(--uui-size-space-3) var(--uui-size-space-4);
    background: color-mix(in srgb, var(--uui-color-warning) 8%, transparent);
    border: 1px solid color-mix(in srgb, var(--uui-color-warning) 40%, transparent);
    border-radius: var(--uui-border-radius);
    font-size: 0.85rem;
  }
  .section-title {
    margin: var(--uui-size-space-5) 0 var(--uui-size-space-2);
  }
  .checkbox-field {
    gap: var(--uui-size-space-2);
  }
  .generated-helper {
    margin-top: var(--uui-size-space-4);
    display: flex;
    flex-direction: column;
    gap: var(--uui-size-space-2);
    padding: var(--uui-size-space-3);
    border: 1px solid var(--uui-color-border);
    border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface-alt);
  }
  .command-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--uui-size-space-2);
    flex-wrap: wrap;
  }
  .toggle-label {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;
    margin-bottom: 0.5rem;
  }
  .toggle-hint {
    font-size: 0.8rem;
    color: #666;
  }
  .toggle-switch {
    position: relative;
    display: inline-block;
    width: 46px;
    height: 24px;
    cursor: pointer;
  }
  .toggle-switch input {
    opacity: 0;
    width: 0;
    height: 0;
    position: absolute;
  }
  .toggle-slider {
    position: absolute;
    inset: 0;
    background-color: #ccc;
    border-radius: 24px;
    transition: background-color 0.2s;
  }
  .toggle-switch input:checked + .toggle-slider {
    background-color: #2563eb;
  }
  .toggle-slider::before {
    content: '';
    position: absolute;
    height: 18px;
    width: 18px;
    left: 3px;
    bottom: 3px;
    background-color: white;
    border-radius: 50%;
    transition: transform 0.2s;
  }
  .toggle-switch input:checked + .toggle-slider::before {
    transform: translateX(22px);
  }
  .toggle-switch input:focus-visible + .toggle-slider {
    outline: 2px solid var(--uui-color-focus, #3879d9);
    outline-offset: 2px;
  }
  @media (prefers-reduced-motion: reduce) {
    .dialog-action-btn,
    .dialog-icon-btn,
    .toggle-slider,
    .toggle-slider::before {
      transition: none;
    }
  }
`;
