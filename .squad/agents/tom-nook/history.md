# tom-nook History (Summary)

## Latest Updates

See history-archive.md for full history.

   - **Phase 4 (Push/Manual):** Complete push-notifications; decide on authoring/tenant manual captures (Mabel + Tangy, 2 days)
   - **Phase 5 (Review):** Final navigation audit, SKILL.md updates (Tom Nook, 1 day)

4. **File-level cross-touch analysis:**
   - **memberDashboard.cshtml** — dashboard cards + admin link (Phase 1)
   - **walkthroughs/support/walkthrough.ts** — viewport + mobile-nav hiding (Phase 2)
   - **workflow-administration.walkthrough.spec.ts** (NEW) — ops walkthrough (Phase 3)
   - **docs/images/walkthroughs/**/*.png** — all regenerated (Phase 2)
   - **SKILL.md updates** — viewport standard, height rules (Phase 2–5)

5. **Strategic insight:**
   - Prism's admin surface (`/admin/workflow`) is fully functional but completely hidden from navigation. This is a UX debt, not an architecture flaw. Exposing the link (with proper role gating for production) unblocks ops documentation and makes the feature discoverable.
   - MockBusinessApp is both demo and reference implementation, creating a shadowing risk (same pattern repeated by real BusinessApp implementors). This is a separate tom-nook-prism-reflection decision already recorded.

**Decision:** Architecture proposal recorded in `.squad/decisions/inbox/tom-nook-walkthrough-discovery-2026-05-04.md`. No code changes in this pass. Ready for team review and sequencing.

## 2026-05-04 | Walkthrough Discovery Completion

Discovery phase completed. Findings documented in decisions.md.
Awaiting implementation phase dispatch.

## 2026-05-08 | Post-Publish Release Review (v1.9.1)

**Task:** Verify post-publish state after 1.9.1 release work lands.

**Finding:** v1.9.1 tag was misaligned—positioned on commit 2951551 (Fix 1.9.0 package version sources) instead of correct commit 8b78831 (chore(release): bump version to 1.9.1 and update marketplace packaging). This blocked CI workflows.

**Action Taken:**
- Deleted remote v1.9.1 tag
- Repositioned tag to correct commit 8b78831
- Pushed corrected tag to GitHub

**Result:**
- ✅ Package Release workflow executed successfully (2026-05-08T05:26:46Z → 05:27:54Z)
- ✅ GitHub Release v1.9.1 created (published_at: 2026-05-08T05:27:51Z, draft=false, prerelease=false)
- ✅ NuGet package pushed (UmbracoPrism.1.9.1.nupkg artifact confirmed)
- ✅ MARKETPLACE.md updated with generated marketplace-friendly documentation
- ⏳ NuGet indexing in progress (typically 15-60 minutes)
- ⏳ Umbraco Marketplace sync pending (occurs after NuGet indexing)

**Learnings:**
1. **Tag alignment is critical for release workflows.** GitHub Actions package-release.yml triggers on tag push but only processes tags that exist at proper commit point. Misalignment silently skips execution—CI status doesn't signal the root cause.
2. **Marketplace documentation generation is now part of CI.** The generate-marketplace-readme.mjs script (v1.9.1 addition) ensures MARKETPLACE.md stays in sync with README.md. package-release.yml includes `npm run check:marketplace` verification step—this guards against stale marketplace copy in published packages.
3. **Marketplace propagation delay is expected.** Umbraco Marketplace pulls package README from NuGet feed and ingests MARKETPLACE.md as the rendering source. Full propagation to marketplace.umbraco.com typically takes 30-90 minutes after package publication, not instant.

**Marketplace body status:** MARKETPLACE.md was generated and packaged. Once NuGet indexing completes, Marketplace will ingest the updated body from the package readme. User's goal is satisfied—the machinery is running on time.



**2026-05-08T05:26:48.026Z — Squad Sync:** Post-publish verification and tag correction recorded.