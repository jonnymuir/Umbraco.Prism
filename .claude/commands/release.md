---
description: Cut a release — infer semver bump from commits, update version files, draft CHANGELOG, and open a PR
---

Cut a release for Umbraco Prism. Follow these steps exactly:

## 1. Find the last release tag

```bash
git tag --list 'v*' --sort=-version:refname | head -1
```

If no tags exist, treat all history as unreleased.

## 2. Read commits since last tag

```bash
git log {last_tag}..HEAD --format="%H %s%n%b" --no-merges
```

Read both the subject line and body of each commit — the body may contain `BREAKING CHANGE:` notes.

## 3. Infer the semver bump

Analyse the full commit log:

- **major** → any commit with `BREAKING CHANGE` in the body, or `!` after the type (e.g. `feat!:`, `fix!:`)
- **minor** → any `feat:` commit (no breaking change)
- **patch** → `fix:`, `perf:`, `docs:`, `chore:`, `refactor:`, `test:`, or no conventional prefix

Take the highest signal found. If no conventional commit signals are present, default to **patch** and note the uncertainty when reporting.

Compute the new version by incrementing the appropriate component of the current version and resetting lower components to zero.

## 4. Announce the plan

Before making any changes, tell the user:
- The last tag and number of commits found
- The inferred bump type and why (which commit(s) drove it)
- The new version number (`vX.Y.Z`)

Ask for confirmation before proceeding: **"Shall I cut vX.Y.Z as a {bump type} release?"**

Wait for a yes/go-ahead before continuing.

## 5. Create a release branch

```bash
git checkout -b release/v{new_version}
```

## 6. Update version files

Keep these two files in sync — update both:

- `src/UmbracoPrism.Core/UmbracoPrism.Core.csproj` → `<Version>{new_version}</Version>`
- `src/UmbracoPrism.Client/package.json` → `"version": "{new_version}"`

## 7. Draft the CHANGELOG entry

Write a new section at the **top** of `CHANGELOG.md` (immediately after the `# Changelog` heading and intro paragraph), using this format:

```markdown
## [v{new_version}] — {YYYY-MM-DD}

### Breaking Changes
- ...

### New Features
- ...

### Bug Fixes & Improvements
- ...

---
```

Omit any section that has no entries.

**Drafting standards — apply every one:**
- **Plain English:** No commit hashes, no internal references, no jargon
- **Developer-first:** Each bullet answers "what changed and why does it matter to me?"
- **Active voice, present tense:** "Adds support for X", "Fixes Y when Z"
- **One change per bullet**, short sentences, no walls of text
- **No filler:** Skip pure internal chore entries (dependency bumps, CI tweaks) unless they affect developers using the package
- Re-read the entry as if you are a developer encountering it cold. If anything is unclear, rewrite it.

## 8. Run pre-release checks

Run these in order and stop if either fails:

```bash
npm run check:marketplace
```
```bash
dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests
```

Fix any failures before proceeding.

## 9. Show the CHANGELOG draft

Print the full draft CHANGELOG entry and say:

> "Here's the draft for v{new_version}. Let me know if you want to edit anything, or say go ahead to commit."

Wait for approval or edits. Apply any changes the user requests, then re-show the affected section before committing.

## 10. Commit

Stage only these files:

```bash
git add src/UmbracoPrism.Core/UmbracoPrism.Core.csproj src/UmbracoPrism.Client/package.json CHANGELOG.md
git commit -m "chore: release v{new_version}"
```

## 11. Push and open a PR

```bash
git push -u origin release/v{new_version}
gh pr create \
  --title "Release: v{new_version}" \
  --body "..."
```

PR body should include:
- The bump type and what drove it
- The full release notes (copy from the CHANGELOG section)
- The post-merge tagging instruction (see step 12)

## 12. Report and remind

Return the PR URL and remind the user:

> After the PR is merged to main, tag the merge commit to trigger the NuGet publish:
>
> ```bash
> git checkout main && git pull
> git tag v{new_version}
> git push origin v{new_version}
> ```
>
> This triggers `package-release.yml` which builds, packs, and publishes the NuGet package.
