# Welcome to Umbraco Prism 👋

> **You can close this tab at any time** — it's just here to help you get oriented.

---

## ⏳ What's happening right now?

The Aspire stack is starting in the background. This launches four services — **Keycloak** (identity), **MockBusinessApp** (workflow API), **TestSite** (the Umbraco site), and the **Aspire Dashboard** — all wired together automatically.

**A browser tab will open automatically** on port 3000 with a live status page. Watch that tab — each service ticks from 🔄 to ✅ as it becomes ready.

### How long will this take?

| Scenario | Estimated time |
|---|---|
| **First time** (Codespace just created, packages being built) | ~5–8 minutes |
| **Subsequent starts** (Codespace resumed, pre-built) | ~2–4 minutes |

Go make a brew. ☕

---

## ✅ When everything is ready

The status page will show all four services green and surface direct links. Here's a preview of what you'll have:

| Service | What it is |
|---|---|
| **TestSite** | The Umbraco site — multi-tenant, GOV.UK Design System styled |
| **Aspire Dashboard** | Telemetry, logs, and health for all running services |
| **Keycloak** | OIDC identity provider (pre-seeded with demo users) |

### Login credentials

| Account | Email | Password | Used for |
|---|---|---|---|
| Demo member | `demo@prism.local` | `password` | TestSite SSO login |
| Umbraco admin | `admin@prism.local` | `PrismLocal!12345` | Umbraco backoffice |
| Keycloak admin | `admin` | `admin` | Keycloak admin console |

---

## 🗺️ Try the demo journey

1. Go to the **TestSite** link in the status page
2. Click **Sign in** and log in as `demo@prism.local` / `password`
3. Navigate to **My Workflows** and start a planning application
4. Complete the multi-step form — including the check-answers review at the end

---

## 💡 Useful tips

- **Ports panel** (VS Code sidebar) — shows all forwarded ports with their labels and public URLs once they're ready
- **Status page** (port 3000) — stays live and updates in real time as services start or restart
- **AppHost logs** — if anything looks stuck, run `tail -f /tmp/prism-apphost.log` in the terminal

---

*This welcome file lives at `CODESPACES.md` in the repo root.*
