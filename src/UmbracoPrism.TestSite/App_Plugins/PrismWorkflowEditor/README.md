# Prism Workflow Editor — Backoffice Section

Umbraco v17 backoffice section that embeds the standalone workflow editor (shipped by Isabelle / the Business App) inside an iframe. The editor application stays fully decoupled from Umbraco; this section provides **discoverability** for human editors.

---

## Files

```
App_Plugins/PrismWorkflowEditor/
├── umbraco-package.json                   ← Package manifest (loaded automatically)
├── web-components/
│   └── prism-workflow-editor-host.js      ← Lit element — thin iframe host
└── README.md                              ← This file
```

---

## How it works

1. Umbraco discovers `umbraco-package.json` at startup and registers five extensions:
   - **Section** (`Umb.Section.PrismWorkflowEditor`) — adds "Workflow Editor" to the backoffice navigation.
   - **SectionSidebarApp** — menu sidebar scoped to the section.
   - **Menu** — `Umb.Menu.PrismWorkflowEditor`.
   - **MenuItem** — "Planning Application" (V1 only has one workflow).
   - **Dashboard** — renders `<prism-workflow-editor-host>` in the main content pane.
2. The Lit element (`prism-workflow-editor-host`) checks whether the authoring base URL is reachable. If yes, it renders a full-height iframe pointing at `workflow-editor.html?workflow=planning`. If not, it shows a friendly "Editor not yet built" message.

---

## Enabling the section for a fresh install

After first-run setup in Umbraco:

1. Navigate to **Settings → Users → User Groups → Administrators** (or whichever group your editors belong to).
2. Under **Allowed Sections**, add **"Workflow Editor"**.
3. Save. The section tab appears immediately on next page load.

> **V1 note:** Section access is managed via Umbraco user-group configuration, not manifest conditions. The default admin account has access to all sections once granted.

---

## Pointing at the Business App during development

The Lit element defaults to `https://localhost:7245` (the MockBusinessApp dev server address). Override this at runtime without rebuilding by injecting a small script block in your Umbraco layout or browser console:

```html
<script>
  window.PrismWorkflowEditorConfig = { authoringBaseUrl: 'https://localhost:7245' };
</script>
```

Or from the browser console (temporary, resets on reload):

```js
window.PrismWorkflowEditorConfig = { authoringBaseUrl: 'http://localhost:5200' };
```

Then reload the Workflow Editor section.

---

## No build step required

The JS file uses bare module specifiers (`@umbraco-cms/backoffice/*`) that are resolved by Umbraco v17's built-in import map at runtime. There is no Vite/webpack step needed. If you add TypeScript in a future slice, introduce a `vite.config.ts` in this directory and update the `umbraco-package.json` to point at the compiled output.

---

## Related documents

- `docs/design/workflow-editor-v1/03-umbraco-integration.md` — integration architecture (Decision 03).
- `docs/design/workflow-editor-v1/README.md` — three-plane architecture overview.
- `src/UmbracoPrism.MockBusinessApp/` — hosts the workflow engine and (in parallel) ships `workflow-editor.html`.
