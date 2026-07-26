// ⚠️ MOBILE BOUNDARY: No @umbraco-cms imports allowed in this directory.
//
// Generic progressive enhancement for any file-upload field. Independent of prism-live-form.ts
// (which only boots when a touchpoint declares a calculations block) — a touchpoint can have
// file-upload fields with no calculations at all, so this boots on its own, gated purely on
// whether [data-prism-file-upload] exists on the page (see PrismServiceRequestViewModel.HasFileUploadField
// for the server-side half of that gate).
//
// On choosing a file, uploads it immediately via XMLHttpRequest (not fetch — upload.onprogress
// is what makes a real, accessible progress bar possible) to CmsServiceRequestFileUploadController,
// ahead of the touchpoint's own Continue button. A hidden input then carries the server-issued token
// as the field's actual submitted value; PrismServiceRequestPageController.HandlePost resolves it back
// to the already-saved file. It contains no domain knowledge — the service blueprint JSON decides
// which fields exist; this runtime only wires up whatever it finds.

const PROGRESS_ANNOUNCE_STEP = 10; // percentage points between aria-live progress announcements

interface UploadResult {
  token: string;
  fileName: string;
  sizeBytes: number;
  downloadUrl: string;
}

function injectStylesOnce(): void {
  const id = 'prism-file-upload-styles';
  if (document.getElementById(id)) return;

  const style = document.createElement('style');
  style.id = id;
  // No official GOV.UK Design System pattern for an upload progress bar — kept deliberately
  // close to govuk-frontend's own visual language (its grey/blue palette) rather than
  // introducing a new one.
  style.textContent = `
    .prism-file-upload-progress-track {
      background: #b1b4b6;
      border-radius: 5px;
      height: 10px;
      overflow: hidden;
      margin: 10px 0;
      max-width: 400px;
    }
    .prism-file-upload-progress-fill {
      background: #1d70b8;
      height: 100%;
      width: 0%;
      transition: width 120ms linear;
    }
  `;
  document.head.appendChild(style);
}

function getAntiforgeryToken(): string {
  const input = document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]');
  return input?.value ?? '';
}

function formatMaxSize(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(0)}MB` : `${Math.ceil(bytes / 1024)}KB`;
}

function fieldOf(host: HTMLElement, hook: string): HTMLElement | null {
  return host.querySelector<HTMLElement>(`[data-prism-file-upload-${hook}]`);
}

function setError(host: HTMLElement, message: string | null): void {
  const errorEl = fieldOf(host, 'error');
  if (!errorEl) return;
  errorEl.textContent = message ?? '';
  errorEl.hidden = !message;
}

function setProgress(host: HTMLElement, percent: number, lastAnnounced: { value: number }): void {
  const fill = fieldOf(host, 'progress-fill');
  const bar = fieldOf(host, 'progress-bar');
  const announce = fieldOf(host, 'progress-announce');
  if (fill) fill.style.width = `${percent}%`;
  bar?.setAttribute('aria-valuenow', String(percent));
  if (announce && percent - lastAnnounced.value >= PROGRESS_ANNOUNCE_STEP) {
    lastAnnounced.value = percent;
    announce.textContent = `Uploading, ${percent}% complete`;
  }
}

function showUploading(host: HTMLElement): void {
  const progressEl = fieldOf(host, 'progress');
  const inputEl = fieldOf(host, 'input') as HTMLInputElement | null;
  const uploadedEl = fieldOf(host, 'uploaded');
  if (progressEl) progressEl.hidden = false;
  if (inputEl) inputEl.hidden = true;
  if (uploadedEl) uploadedEl.hidden = true;
  setProgress(host, 0, { value: -PROGRESS_ANNOUNCE_STEP });
}

function showUploaded(host: HTMLElement, result: UploadResult): void {
  const uploadedEl = fieldOf(host, 'uploaded');
  const filenameEl = fieldOf(host, 'filename');
  const viewLink = fieldOf(host, 'view-link') as HTMLAnchorElement | null;
  const inputEl = fieldOf(host, 'input') as HTMLInputElement | null;
  const tokenEl = fieldOf(host, 'token') as HTMLInputElement | null;
  const progressEl = fieldOf(host, 'progress');

  if (filenameEl) filenameEl.textContent = result.fileName;
  if (viewLink) {
    viewLink.href = result.downloadUrl;
    viewLink.hidden = false;
  }
  if (uploadedEl) uploadedEl.hidden = false;
  if (inputEl) {
    // Disabled, not just hidden — an unselected <input type="file"> still submits an
    // empty-but-present part under this same "fields[...]" name, which would silently
    // overwrite this real reference with a zero-byte one on the eventual stage submission.
    inputEl.hidden = true;
    inputEl.disabled = true;
  }
  if (progressEl) progressEl.hidden = true;
  if (tokenEl) {
    tokenEl.value = result.token;
    tokenEl.disabled = false;
  }
  setError(host, null);
}

/** Back to "nothing chosen yet" — a fresh field, a failed upload, or "Choose a different file". */
function showEmpty(host: HTMLElement): void {
  const uploadedEl = fieldOf(host, 'uploaded');
  const inputEl = fieldOf(host, 'input') as HTMLInputElement | null;
  const tokenEl = fieldOf(host, 'token') as HTMLInputElement | null;
  const progressEl = fieldOf(host, 'progress');

  if (uploadedEl) uploadedEl.hidden = true;
  if (inputEl) {
    inputEl.hidden = false;
    inputEl.disabled = false;
    inputEl.value = '';
  }
  if (progressEl) progressEl.hidden = true;
  if (tokenEl) {
    tokenEl.value = '';
    tokenEl.disabled = true;
  }
}

function uploadFile(host: HTMLElement, file: File): void {
  const uploadUrl = host.dataset.prismUploadUrl ?? '';
  const nonce = host.dataset.prismNonce ?? '';
  const maxSize = Number(host.dataset.prismMaxSize ?? '0');
  const accept = (host.dataset.prismAccept ?? '').split(',').map(s => s.trim()).filter(Boolean);
  const label = host.dataset.prismLabel || 'This file';

  if (maxSize > 0 && file.size > maxSize) {
    setError(host, `${label} must be smaller than ${formatMaxSize(maxSize)}.`);
    showEmpty(host);
    return;
  }
  if (accept.length > 0) {
    const dot = file.name.lastIndexOf('.');
    const extension = dot >= 0 ? file.name.slice(dot) : '';
    if (!accept.some(a => a.toLowerCase() === extension.toLowerCase())) {
      setError(host, `${label} must be one of: ${accept.join(', ')}.`);
      showEmpty(host);
      return;
    }
  }

  setError(host, null);
  showUploading(host);

  const formData = new FormData();
  formData.append('file', file, file.name);
  formData.append('nonce', nonce);
  formData.append('__RequestVerificationToken', getAntiforgeryToken());

  const lastAnnounced = { value: -PROGRESS_ANNOUNCE_STEP };
  const xhr = new XMLHttpRequest();
  xhr.open('POST', uploadUrl, true);
  xhr.upload.addEventListener('progress', event => {
    if (!event.lengthComputable) return;
    setProgress(host, Math.round((event.loaded / event.total) * 100), lastAnnounced);
  });
  xhr.addEventListener('load', () => {
    if (xhr.status >= 200 && xhr.status < 300) {
      try {
        showUploaded(host, JSON.parse(xhr.responseText) as UploadResult);
        return;
      } catch {
        // fall through to the generic error below
      }
    }
    setError(host, xhr.responseText || 'Something went wrong uploading this file. Try again.');
    showEmpty(host);
  });
  xhr.addEventListener('error', () => {
    setError(host, 'Something went wrong uploading this file. Try again.');
    showEmpty(host);
  });
  xhr.send(formData);
}

function bootField(host: HTMLElement): void {
  const inputEl = fieldOf(host, 'input') as HTMLInputElement | null;
  const changeButton = fieldOf(host, 'change');

  inputEl?.addEventListener('change', () => {
    const file = inputEl.files?.[0];
    if (file) uploadFile(host, file);
  });

  changeButton?.addEventListener('click', () => {
    showEmpty(host);
    inputEl?.focus();
  });
}

function boot(): void {
  const fields = document.querySelectorAll<HTMLElement>('[data-prism-file-upload]');
  if (fields.length === 0) return;
  injectStylesOnce();
  fields.forEach(bootField);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', boot);
} else {
  boot();
}
