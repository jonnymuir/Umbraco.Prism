# Welcome to Umbraco Prism 👋

> **You can close this tab at any time** — it's just here to help you get oriented.

---

## ⏳ Be patient — the stack is starting

The Aspire stack is launching in the background. Don't panic if things look quiet for a moment!

| Scenario | Estimated time |
|---|---|
| **First time** (Codespace just created, packages being built) | ~5–8 minutes |
| **Subsequent starts** (Codespace resumed, pre-built) | ~2–4 minutes |

**You don't need to do anything** — just watch the status page (it opens automatically on port 3000). Each service will tick from 🔄 to ✅ as it becomes ready.

Go make a brew. ☕

---

## What's happening right now?

The Aspire stack launches four services — **Keycloak** (identity), **MockBusinessApp** (workflow API), **TestSite** (the Umbraco site), and the **Aspire Dashboard** — all wired together automatically.

**A browser tab will open automatically** on port 3000 with a live status page. Watch that tab — each service ticks from 🔄 to ✅ as it becomes ready.

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

- **Ports panel** (VS Code sidebar) — shows all forwarded ports with their labels and public URLs once they're ready. The Aspire Dashboard is on port **17214** (HTTPS) in both Codespaces and local development.
- **Status page** (port 3000) — stays live and updates in real time as services start or restart. The terminal prints the full clickable URL when the server comes up.
- **If a forwarded URL downloads, opens a blank tab, or returns an empty `404`** — that usually means the Codespaces tunnel is not currently serving that port on that hostname. For the status page specifically, copy the current port **3000** URL from the **Ports** panel or rerun `bash scripts/codespaces/refresh.sh` to restart and re-register it.
- **AppHost logs** — if anything looks stuck, run `tail -f artifacts/startup-status/prism-apphost.log` in the terminal

---

## 🔄 Refreshing the stack (pull latest & restart)

Use these scripts when you need to update code or recover a broken stack without rebuilding the entire Codespace.

### When to use

| Situation | What to do |
|---|---|
| You've pulled (or want to pull) latest code and restart | `bash scripts/codespaces/refresh.sh` |
| Services are misbehaving and you want a clean restart | `bash scripts/codespaces/refresh.sh` |
| You want to stop the stack without restarting | `bash scripts/codespaces/stop.sh` |
| NuGet packages or project references changed | `bash scripts/codespaces/refresh.sh --rebuild` |
| You just want to know if everything is up | `bash scripts/codespaces/health-check.sh` |
| Downstream API / auth / backchannel behaviour looks wrong | `bash scripts/codespaces/diagnose-downstream.sh` |

### The scripts

All scripts live in `scripts/codespaces/` and should be run from the **repo root**.

#### `stop.sh` — Stop all running services

```bash
bash scripts/codespaces/stop.sh
```

Gracefully kills the Aspire AppHost (`UmbracoPrism.AppHost`) and the startup status server (port 3000). Safe to run even if services are already stopped. After stopping, ports 3000, 17214, 44345, 8443, and 7245 are freed.

#### `refresh.sh` — Stop → pull → restart

```bash
bash scripts/codespaces/refresh.sh
```

The standard refresh path. Does the least-destructive update cycle:

1. **Stop** — calls `stop.sh` to kill the running stack
2. **Pull** — `git pull origin main`
3. **npm install** — automatically runs if `package-lock.json` changed in the pull
4. **Restart** — calls `.devcontainer/on-start.sh`, the real startup contract (starts status page on port 3000, waits for Docker, launches AppHost)

**With `--rebuild`** — use this after pulling changes that add/remove NuGet packages, change project structure, or when you want a clean compile:

```bash
bash scripts/codespaces/refresh.sh --rebuild
```

This adds `dotnet restore` + `dotnet build` before the restart.

**With `--no-start`** — stop and update but don't restart:

```bash
bash scripts/codespaces/refresh.sh --no-start
```

#### `health-check.sh` — Confirm the stack is healthy

```bash
bash scripts/codespaces/health-check.sh
```

Probes all four readiness endpoints and prints a ✅/❌ summary:

| Service | Endpoint |
|---|---|
| Status server | `http://localhost:3000/api/status` |
| Aspire Dashboard | `https://localhost:17214` |
| TestSite | `https://localhost:44345/api/prism/downstream-demo/seed-contract-ready` |
| Keycloak | `https://localhost:8443/realms/prism-dev/.well-known/openid-configuration` |
| MockBusinessApp | `https://localhost:7245/debug/auth` |

Exits `0` when all services are ready, `1` if any are not.

#### `diagnose-downstream.sh` — Inspect downstream API, auth, and tunnel behaviour

```bash
bash scripts/codespaces/diagnose-downstream.sh
```

Use this when the **Call Mock Business App API** flow is failing, timing out, or returning confusing Codespaces results.

It:
- checks local TestSite / MockBusinessApp / Keycloak endpoints from inside the Codespace
- reads safe runtime diagnostics from MockBusinessApp `/debug/auth`
- probes TestSite's same-origin downstream endpoints in a non-browser-safe way (`401` without your browser cookie is expected)
- probes public `app.github.dev` URLs without hiding redirects or tunnel/auth HTML pages
- prints only safe environment values and avoids dumping cookies or bearer tokens

If you already have a fresh bearer token and want the script to try authenticated downstream calls too:

```bash
PRISM_BEARER_TOKEN='<access-token>' bash scripts/codespaces/diagnose-downstream.sh
```

This script is intentionally **Codespaces-oriented**. It is most useful when run from the Codespace terminal after the AppHost is already running.

### Confirming the stack is healthy

After running `refresh.sh`, the status page (port 3000) updates automatically. You can also run:

```bash
bash scripts/codespaces/health-check.sh
```

All five entries should be ✅. If Keycloak or TestSite are still pending, wait 30–60 seconds and try again — the Aspire cold-start can take 3–5 minutes on a resumed Codespace.

You can also watch the AppHost log directly:

```bash
tail -f artifacts/startup-status/prism-apphost.log
```

### When a full Codespace rebuild is actually necessary

A `refresh.sh` is enough for the vast majority of updates. Rebuild the whole Codespace (via **Codespaces → Rebuild Container** in VS Code or the GitHub UI) only when:

- The `.devcontainer/devcontainer.json` or a feature has changed (Docker image or features update)
- The `.devcontainer/on-create.sh` has changed (one-time setup steps like `dotnet dev-certs --trust` or the initial `dotnet restore`)
- You hit a broken Docker-in-Docker state that `stop.sh` can't fix (symptom: `docker info` hangs or fails repeatedly)
- The `.NET SDK` or `Node.js` version constraint in the devcontainer has changed

A full rebuild takes 5–10 minutes and resets the Umbraco SQLite database — the demo data will be reseeded automatically on next start.

---

*This welcome file lives at `CODESPACES.md` in the repo root.*
