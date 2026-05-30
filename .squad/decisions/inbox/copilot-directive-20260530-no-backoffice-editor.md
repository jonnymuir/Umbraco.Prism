### 2026-05-30: User directive — no Umbraco backoffice editor, now or in future
**By:** Jonny Muir (via Copilot)
**What:** The workflow editor must NOT be hosted inside the Umbraco backoffice — not now, not later. The TestSite App_Plugins dashboard and any "drop it into the back office" recipe should be deleted. Boundary: TestSite (Umbraco v17 runtime) consumes published workflows at runtime; MockBusinessApp is the reference back office that hosts the authoring editor; UmbracoPrism.WorkflowEditor is the componentised library both consume.
**Why:** User request — captured for team memory. This supersedes Brewster's "mount the editor as a native v17 web component" DX recommendation. Reviewer findings that depended on the in-backoffice path are now moot.
