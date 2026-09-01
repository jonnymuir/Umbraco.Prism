# Cloudflare Maintenance & Error Pages

When the Umbraco backend goes down, Cloudflare sits at the edge and can intercept the 502/504 responses before they reach your users. This guide covers two approaches: static error pages and Workers.

## Why Handle This at Cloudflare?

Umbraco Prism serves multi-tenant portals through a single backend instance. Both browser users and mobile apps (iOS/Android Capacitor) connect through Cloudflare tunnel/proxy. If the backend is down:

- Raw 502/504 responses create bad UX
- Mobile apps can't parse HTML errors as structured data
- No changes needed to app or backend code, Cloudflare handles it all

---

## Option A: Custom Error Pages (Simple)

Best for: Quick setup, single branded HTML page for all users.

### Setup

1. **Go to Cloudflare Dashboard** → Your Domain → **Custom Pages**
2. Under **"500 Class Errors"**, click the card for **502, 503, or 504**
3. Click **"Create Custom Page"** or **"Edit"** (if one exists)
4. Paste the HTML below into the editor
5. Click **"Save and Deploy"**

### Example HTML

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Maintenance</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            background: linear-gradient(135deg, #1a1a1a 0%, #0d0d0d 100%);
            color: #e0e0e0;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
        }
        .container {
            text-align: center;
            max-width: 500px;
            background: rgba(255, 255, 255, 0.05);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 12px;
            padding: 60px 30px;
            backdrop-filter: blur(10px);
        }
        h1 { font-size: 2.5em; margin-bottom: 15px; color: #fff; }
        p { font-size: 1.1em; margin-bottom: 25px; line-height: 1.6; color: #b0b0b0; }
        .code { font-family: monospace; color: #888; margin: 10px 0; }
        a {
            display: inline-block;
            margin-top: 20px;
            padding: 12px 30px;
            background: #0066ff;
            color: white;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            transition: background 0.3s;
        }
        a:hover { background: #0052cc; }
    </style>
</head>
<body>
    <div class="container">
        <h1>⚙️ Maintenance</h1>
        <p>We're performing scheduled maintenance. We'll be back online shortly.</p>
        <p class="code">Error code: [cf_error]</p>
        <button onclick="location.reload()">
            <a onclick="event.preventDefault(); location.reload();">Refresh Page</a>
        </button>
    </div>
</body>
</html>
```

### Limitations

- **Same page for all users:** Browser and mobile apps both receive HTML
- **No structured data:** Mobile apps expecting JSON will get HTML
- **No intelligence:** Can't distinguish API calls from page views

**Use when:** Simple site, no mobile app, or mobile app can gracefully handle HTML error pages.

---

## Option B: Cloudflare Worker (Recommended for Prism)

Best for: Separate responses for browsers vs APIs. Returns JSON to mobile apps, HTML to browsers.

### How It Works

A Worker intercepts all 5xx responses and returns:
- **HTML** if the request came from a browser (`Accept: text/html`)
- **JSON** if the request came from a mobile app or API (`Accept: application/json`)

### Example Worker

Create a new Worker with this code:

```javascript
export default {
  async fetch(request, env) {
    try {
      // Forward the request to the origin
      const response = await fetch(request);

      // If it's a 5xx error, handle it
      if (response.status >= 500) {
        const acceptHeader = request.headers.get('accept') || '';
        
        // Return JSON for API/mobile requests
        if (acceptHeader.includes('application/json')) {
          return new Response(
            JSON.stringify({
              maintenance: true,
              message: 'Service temporarily unavailable',
              status: response.status,
              timestamp: new Date().toISOString()
            }),
            {
              status: 503,
              headers: { 'Content-Type': 'application/json' }
            }
          );
        }

        // Return HTML for browser requests
        const html = `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Maintenance</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            background: linear-gradient(135deg, #1a1a1a 0%, #0d0d0d 100%);
            color: #e0e0e0;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
        }
        .container {
            text-align: center;
            max-width: 500px;
            background: rgba(255, 255, 255, 0.05);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 12px;
            padding: 60px 30px;
            backdrop-filter: blur(10px);
        }
        h1 { font-size: 2.5em; margin-bottom: 15px; color: #fff; }
        p { font-size: 1.1em; margin-bottom: 25px; line-height: 1.6; color: #b0b0b0; }
        a {
            display: inline-block;
            margin-top: 20px;
            padding: 12px 30px;
            background: #0066ff;
            color: white;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
        }
        a:hover { background: #0052cc; }
    </style>
</head>
<body>
    <div class="container">
        <h1>⚙️ Maintenance</h1>
        <p>We're performing scheduled maintenance. We'll be back online shortly.</p>
        <a href="javascript:location.reload()">Refresh Page</a>
    </div>
</body>
</html>`;

        return new Response(html, {
          status: 503,
          headers: { 'Content-Type': 'text/html' }
        });
      }

      return response;
    } catch (error) {
      // Network error (origin unreachable)
      const acceptHeader = request.headers.get('accept') || '';

      if (acceptHeader.includes('application/json')) {
        return new Response(
          JSON.stringify({
            maintenance: true,
            message: 'Service temporarily unavailable',
            error: 'Network error'
          }),
          {
            status: 503,
            headers: { 'Content-Type': 'application/json' }
          }
        );
      }

      return new Response(
        `<!DOCTYPE html>
<html><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"><title>Error</title></head>
<body style="font-family: sans-serif; background: #0d0d0d; color: #e0e0e0; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0;">
<div style="text-align: center;">
<h1>⚙️ Maintenance</h1>
<p>We're performing scheduled maintenance. We'll be back online shortly.</p>
</div>
</body></html>`,
        {
          status: 503,
          headers: { 'Content-Type': 'text/html' }
        }
      );
    }
  }
};
```

### Deploy the Worker

1. **Cloudflare Dashboard** → **Workers & Pages** → **Create Application**
2. Choose **Create Worker**
3. Paste the code above into the editor
4. Click **Deploy**
5. **Go to the Worker details** → **Routes** → **Add Route**
6. Enter your domain: `yourdomain.com/*`
7. Select this Worker
8. Click **Save**

### Testing

**Browser (HTML):**
```bash
curl -H "Accept: text/html" https://yourdomain.com
# Returns: <html>...</html>
```

**Mobile/API (JSON):**
```bash
curl -H "Accept: application/json" https://yourdomain.com
# Returns: {"maintenance": true, "message": "...", ...}
```

---

## When to Use Which

| Scenario | Use | Why |
|----------|-----|-----|
| Static site, no mobile app | Custom Pages | Simplest. One upload, done. |
| Mobile app (Capacitor/iOS/Android) | Worker | Serves JSON to apps, HTML to browsers. Apps parse it properly. |
| Marketing site + API | Worker | Keeps concerns separate. |
| Need estimated time? | Worker + KV | Worker can read from Workers KV to show ETA. |

---

## Planned vs Unexpected Downtime

### Planned Maintenance

1. **Set up the Worker/page in advance** (see above)
2. **Customize the message:** `"We'll be back at 2 PM EST"` or `"Estimated 30 minutes"`
3. **Optional:** Use Worker + KV to store maintenance end time and display it dynamically
4. **No app changes needed**: user sees the page automatically

### Unexpected Downtime

1. **Nothing to do.** Cloudflare automatically serves the error page if the backend is unreachable.
2. Mobile app receives JSON and can display a native "Service Down" UI.
3. Browser users see the maintenance page.

---

## Example: Mobile App Handling (Capacitor/React Native)

Once your Worker returns `{ "maintenance": true }`, your mobile app can handle it:

```typescript
const response = await fetch('https://yourdomain.com/api/data', {
  headers: { 'Accept': 'application/json' }
});

const data = await response.json();

if (data.maintenance) {
  // Show native modal or snackbar
  showAlert('Service Unavailable', data.message);
  return;
}

// Normal flow
useData(data);
```

---

## Troubleshooting

**I deployed the Worker but it's not catching errors:**
- Check the Route pattern matches your domain (`yourdomain.com/*`)
- Verify the Worker is enabled (green toggle)
- Wait a few seconds for DNS propagation

**Mobile app still crashes on JSON:**
- Ensure `Accept: application/json` is sent in request headers
- Check the Worker logs: **Workers & Pages** → Your Worker → **Real-time logs**

**Custom error page not showing:**
- Wait 1–2 minutes for Cloudflare to deploy
- Hard refresh the browser (Cmd+Shift+R / Ctrl+Shift+R)
- Check Custom Pages section, ensure page is assigned to 502/503/504

---

## Next Steps

- **For Prism:** Use **Worker** (recommended). It handles browser and mobile gracefully.
- **Customize the HTML:** Add your logo, colors, or copy as needed.
- **Add monitoring:** Use Cloudflare Analytics or Opsgenie to know when the backend is down.
