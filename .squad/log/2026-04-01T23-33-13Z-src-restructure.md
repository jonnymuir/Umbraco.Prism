# Session: 2026-04-02 — Frontend Directory Restructure

**Agent:** Isabelle (Frontend Dev)
**Timestamp:** 2026-04-01T23:33:13Z
**Task:** Split `src/UmbracoPrism.Client/src/` into `backoffice/` and `mobile/` subdirectories

## Result

✅ Complete, build clean.

- Moved 10 backoffice components + shared utilities → `src/backoffice/`
- Moved 2 mobile component files → `src/mobile/`
- Added ESLint flat config to enforce Umbraco-free boundary on mobile
- All build outputs unchanged (same filenames, same sizes)

## Key Learning

ESLint 9 flat config (`eslint.config.mjs`) with `no-restricted-imports` scoped to `src/mobile/**` provides architectural guard rails — mobile code cannot accidentally import from `@umbraco-cms/backoffice`.
