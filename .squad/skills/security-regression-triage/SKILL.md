# Skill: Security Regression Triage

**Confidence:** high
**Owner:** Copper / Tangy / reviewers diagnosing security tests
**Purpose:** Separate real exploit coverage from stale or placeholder tests so the team fixes actual security defects first.

---

## Workflow

1. **Trace attacker input to the sink.**
   - For auth redirects, follow user input from controller query string → auth properties/state → callback handler → final redirect.
   - Do not stop at the first method if the security boundary spans multiple handlers.

2. **Ask whether the test executes production code.**
   - If the assertion is based on a hardcoded helper or a synthetic expression unrelated to runtime behavior, it is not authoritative.
   - Placeholder tests can be useful reminders, but they should not be treated as proof of a regression.

3. **Classify by exploitability, not by intent.**
   - **Production code should be changed:** attacker-controlled input reaches a sensitive sink with insufficient validation.
   - **Test should be rewritten:** the security concern may be real, but the current test does not exercise the real path.
   - **Stale false positive:** production behavior already satisfies the security goal and the test is out of date.

4. **Recommend the next test shape.**
   - Prefer behavioral/integration tests that assert:
     - malicious inputs are rejected or normalized,
     - safe local values still work,
     - default production behavior is safe without relying on comments or TODO helpers.

---

## Prism-specific pattern

- `returnUrl` is security-sensitive across both `AccountController` and `PrismOidcConfiguration`.
- A check on the authenticated `LocalRedirect(...)` branch alone is insufficient if unauthenticated users carry the value through OIDC state and the callback later issues `Response.Redirect(...)`.
- For operationally sensitive debug surfaces, default-production suppression matters more than whether the code uses `#if DEBUG`; explicit opt-in may be acceptable if the default state is non-rendering.
