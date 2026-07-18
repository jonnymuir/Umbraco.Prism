# Skill: Conventional Commits

**Confidence:** high
**Owner:** Team-wide (all agents who commit code)
**Purpose:** Enables Mabel to infer the correct semver bump automatically. Without this, release notes are unreliable.

---

## The Convention

Every commit message MUST follow this format:

```
<type>[optional scope]: <short description>

[optional body]

[optional footer — BREAKING CHANGE goes here]
```

### Commit Types

| Type | Semver Impact | When to use |
|------|--------------|-------------|
| `feat:` | **minor** bump | New feature, new public API, new capability |
| `fix:` | **patch** bump | Bug fix, regression fix, corrected behaviour |
| `perf:` | **patch** bump | Performance improvement with no API change |
| `refactor:` | **patch** bump | Internal restructure, no behaviour change |
| `docs:` | **patch** bump | Documentation only |
| `test:` | **patch** bump | Adding or fixing tests |
| `chore:` | **patch** bump | Build scripts, CI, dependency updates |
| `style:` | **patch** bump | Formatting, whitespace — no logic change |
| `feat!:` or `fix!:` | **MAJOR** bump | Breaking change (see below) |

### Scopes (optional but helpful)

Use a scope to indicate which part of the system changed:

```
feat(tenant): add per-tenant OIDC override
fix(auth): correct token expiry calculation
chore(ci): update GitHub Actions runner version
```

---

## ⚠️ Breaking Changes — Read This Carefully

A breaking change is **any change that requires consumers of the package to update their code, configuration, or infrastructure**. This includes:

- Removing or renaming a public API, method, class, or interface
- Changing the signature of a public method
- Changing required configuration keys or structure
- Changing default behaviour in a way that will break existing installations
- Removing support for a framework version (e.g. dropping Umbraco v16 support)

### How to flag a breaking change

**Method 1 — `!` after type (preferred for short descriptions):**
```
feat!: remove legacy TenantResolver in favour of IPrismTenantResolver
```

**Method 2 — `BREAKING CHANGE:` footer (preferred when explanation is needed):**
```
feat: replace TenantResolver with IPrismTenantResolver

BREAKING CHANGE: TenantResolver has been removed. Replace all usages with
IPrismTenantResolver. Register via services.AddPrismAuthentication() — the
old manual registration pattern is no longer supported.
```

Both methods trigger a **major** version bump when Mabel reads the log.

### Before you make a breaking change — think it through

Ask yourself:
1. **Is this truly necessary?** Can backward compatibility be maintained with a deprecation path?
2. **Is there a migration path?** Users need to know exactly what to change.
3. **Is it documented?** The breaking change footer must be clear enough for a developer who has never seen the internals.
4. **Has the Lead been consulted?** Breaking changes should be flagged to Tom Nook before committing — don't sneak them in.

If in doubt, **add a deprecation warning in this release** and make the breaking change in the next major.

---

## Examples

```bash
# New feature
git commit -m "feat(tenant): add per-tenant custom domain routing"

# Bug fix
git commit -m "fix(auth): prevent token refresh loop on expired sessions"

# Breaking change — short form
git commit -m "feat!: remove PrismContext.Current in favour of IPrismContextAccessor"

# Breaking change — with migration notes
git commit -m "feat: replace synchronous tenant resolution with async pipeline

BREAKING CHANGE: ITenantResolver.Resolve() is now async. All implementations
must be updated to return Task<PrismTenant>. The sync overload has been removed.
Update registrations from AddPrismTenantResolver<T>() to AddAsyncPrismTenantResolver<T>()."

# Chore — won't affect release notes for users
git commit -m "chore(ci): pin dotnet SDK to 10.0.100 in global.json"

# Docs
git commit -m "docs: add prerequisites section to README"
```

---

## What Mabel does with this

Mabel reads `git log` since the last `v*` tag and applies this logic:

| Signal found | Bump |
|-------------|------|
| Any `BREAKING CHANGE:` footer OR any `!` type | **major** |
| Any `feat:` commit | **minor** |
| Only `fix:`, `perf:`, `docs:`, `chore:`, `test:`, `refactor:`, `style:` | **patch** |
| No conventional signals at all | **patch** (with a note) |

She groups commits into CHANGELOG sections:
- `BREAKING CHANGE` → **Breaking Changes**
- `feat` → **New Features**
- `fix`, `perf` → **Bug Fixes & Improvements**
- `chore`, `docs`, `test`, `refactor`, `style` → omitted from user-facing notes unless significant

---

## Enforcement note

This is a team convention, not a linter. If commits don't follow the format, Mabel will default to **patch** and flag uncertainty in her report. Breaking changes that aren't flagged will be silently treated as patches — which means users won't be warned, and the semver bump will be wrong.

**Write commits that Mabel can read.**
