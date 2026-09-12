// Downstream API demo for memberDashboard.cshtml — calls DownstreamDemoController and renders
// the result. 100% static (no per-request server values), externalized (not spliced inline) per
// SEC-PT2-004; the call/reset buttons are wired by id below rather than inline onclick="..."
// attributes, which are subject to the same CSP script-src restriction as inline <script> bodies.
const downstreamDemoEndpoint = '/api/prism/downstream-demo';

function getApiElements() {
    return {
        button: document.getElementById('api-btn'),
        loading: document.getElementById('api-loading'),
        result: document.getElementById('api-result'),
        badge: document.getElementById('api-status-badge'),
        url: document.getElementById('api-url-label'),
        timing: document.getElementById('api-timing'),
        bodyLabel: document.getElementById('api-body-label'),
        contentType: document.getElementById('api-content-type'),
        body: document.getElementById('api-body'),
        summary: document.getElementById('api-summary')
    };
}

function setApiButtonBusy(isBusy) {
    const { button } = getApiElements();
    if (!button) return;

    button.disabled = isBusy;
    button.setAttribute('aria-busy', isBusy ? 'true' : 'false');
    button.textContent = isBusy ? 'Calling Mock Business App API…' : 'Call Mock Business App API';
}

function readApiBody(payload, rawText, fallbackMessage) {
    if (payload && typeof payload.diagnosticBody === 'string' && payload.diagnosticBody.trim()) {
        return payload.diagnosticBody.trim();
    }

    if (payload && typeof payload.body === 'string' && payload.body.trim()) {
        return payload.body.trim();
    }

    if (payload && typeof payload.error === 'string' && payload.error.trim()) {
        return payload.error.trim();
    }

    if (payload && typeof payload.message === 'string' && payload.message.trim()) {
        return payload.message.trim();
    }

    if (typeof rawText === 'string' && rawText.trim()) {
        return rawText.trim();
    }

    return fallbackMessage;
}

function createApiSummary(statusCode, statusText, contentType) {
    const normalizedContentType = typeof contentType === 'string'
        ? contentType.toLowerCase()
        : '';

    if (statusText === 'Invalid Response') {
        if (normalizedContentType.includes('html')) {
            return 'The downstream service returned HTML instead of JSON. Inspect the raw response below.';
        }

        return `The downstream service returned ${contentType || 'non-JSON content'} instead of JSON.`;
    }

    if (statusCode >= 200 && statusCode < 300) {
        return 'Mock Business App responded successfully.';
    }

    if (statusCode === 401 || statusCode === 403) {
        return 'Your Prism session is no longer valid. Sign in again, then retry the call.';
    }

    if (statusCode === 0 || statusText === 'Timeout' || statusText === 'Network Error') {
        return 'We could not reach the Mock Business App. Check that it is running, then try again.';
    }

    if (statusCode >= 500) {
        return 'The Mock Business App returned a server error. Please try again in a moment.';
    }

    return 'The Mock Business App call did not complete successfully.';
}

function renderApiResult(model) {
    const elements = getApiElements();
    if (!elements.loading || !elements.badge || !elements.result || !elements.url || !elements.timing || !elements.bodyLabel || !elements.contentType || !elements.body || !elements.summary) {
        return;
    }

    const ok = model.statusCode >= 200 && model.statusCode < 300;
    elements.loading.style.display = 'none';
    elements.result.style.display = 'block';
    elements.badge.textContent = `${model.statusCode} ${model.statusText}`;
    elements.badge.style.background = ok ? 'rgba(22,163,74,0.12)' : 'rgba(239,68,68,0.12)';
    elements.badge.style.color = ok ? 'var(--prism-success,#16a34a)' : 'var(--prism-danger,#ef4444)';
    elements.url.textContent = model.url;
    elements.timing.textContent = typeof model.elapsedMs === 'number' ? `${model.elapsedMs}ms` : '';
    elements.bodyLabel.textContent = model.bodyLabel || 'Response body';
    elements.contentType.textContent = model.contentType || 'unknown';
    elements.body.textContent = model.body;
    elements.summary.textContent = model.summary;
    elements.summary.style.color = ok ? 'var(--prism-success,#16a34a)' : 'var(--prism-danger,#ef4444)';
}

async function parseApiResponse(res) {
    const rawText = await res.text();
    let payload = null;

    if (rawText.trim()) {
        try {
            payload = JSON.parse(rawText);
        } catch {
            payload = null;
        }
    }

    const statusCode = Number.isFinite(payload?.statusCode) ? payload.statusCode : (res.status || 0);
    const statusText = typeof payload?.statusText === 'string' && payload.statusText.trim()
        ? payload.statusText.trim()
        : (res.statusText || (res.ok ? 'OK' : 'Request Failed'));
    const url = typeof payload?.url === 'string' && payload.url.trim()
        ? payload.url.trim()
        : (res.url || downstreamDemoEndpoint);
    const contentType = typeof payload?.contentType === 'string' && payload.contentType.trim()
        ? payload.contentType.trim()
        : (res.headers.get('content-type')?.split(';')[0] || 'unknown');
    const elapsedMs = Number.isFinite(payload?.elapsedMs) ? payload.elapsedMs : null;
    const hasDiagnosticBody = typeof payload?.diagnosticBody === 'string' && payload.diagnosticBody.trim();
    const diagnosticBodyTruncated = payload?.diagnosticBodyTruncated === true;
    const summary = typeof payload?.summary === 'string' && payload.summary.trim()
        ? payload.summary.trim()
        : createApiSummary(statusCode, statusText, contentType);
    const body = readApiBody(payload, rawText, summary);

    return {
        statusCode,
        statusText,
        url,
        elapsedMs,
        contentType,
        body,
        summary,
        bodyLabel: hasDiagnosticBody
            ? `Downstream diagnostics${diagnosticBodyTruncated ? ' (truncated)' : ''}`
            : 'Response body'
    };
}

async function callDownstreamApi() {
    const elements = getApiElements();
    if (!elements.loading || !elements.result) return;

    setApiButtonBusy(true);
    elements.result.style.display = 'none';
    elements.loading.style.display = 'block';

    try {
        const res = await fetch(downstreamDemoEndpoint, { credentials: 'include' });
        const model = await parseApiResponse(res);
        renderApiResult(model);
    } catch (err) {
        renderApiResult({
            statusCode: 0,
            statusText: 'Client Error',
            url: downstreamDemoEndpoint,
            elapsedMs: null,
            contentType: 'none',
            body: err instanceof Error ? err.message : String(err),
            summary: 'The dashboard could not complete the request. Please try again.'
        });
    } finally {
        setApiButtonBusy(false);
    }
}

function resetApiDemo() {
    const elements = getApiElements();
    if (!elements.result || !elements.loading || !elements.body || !elements.summary || !elements.timing || !elements.bodyLabel || !elements.contentType || !elements.url || !elements.badge) {
        return;
    }

    setApiButtonBusy(false);
    elements.loading.style.display = 'none';
    elements.result.style.display = 'none';
    elements.badge.textContent = '';
    elements.url.textContent = '';
    elements.timing.textContent = '';
    elements.bodyLabel.textContent = '';
    elements.contentType.textContent = '';
    elements.summary.textContent = '';
    elements.body.textContent = '';
}

document.getElementById('api-btn')?.addEventListener('click', callDownstreamApi);
document.getElementById('api-reset-btn')?.addEventListener('click', resetApiDemo);
