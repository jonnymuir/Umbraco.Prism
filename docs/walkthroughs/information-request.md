# Information Request Service Blueprint

A data request form demonstrating date inputs, a detailed request textarea, and urgency triage.

## Overview

The information request service blueprint (`information-request`) handles user requests for information with:

- **Date inputs** for capturing date of birth
- **Textareas** for detailed request descriptions
- **Urgency triage radios** for standard, urgent, and critical handling
- **Under-review status flow** before final completion

## Initial Form

![Information Request initial state — personal details, request type, and urgency fields](../images/walkthroughs/information-request/01-initial.png)

The form collects:

1. **Personal details** – Name, date of birth, email
2. **Request details** – Type of request and detailed description
3. **Urgency** – Standard, urgent, or critical review priority

### Filled Form

![Information Request filled — Jane Smith, data subject access request, urgent urgency selected](../images/walkthroughs/information-request/02-form-filled.png)

A completed form shows personal details, the selected request type, and the description. In this example: **First name:** Jane, **Last name:** Smith, **Date of birth:** 12/03/1985, **Email:** jane.smith@example.com, **Request type:** Data subject access request, with detailed description and **Urgency:** Urgent (2 working days) selected.

### After Submission

![Confirmation screen — "Your request is being reviewed" success panel](../images/walkthroughs/information-request/03-under-review.png)

Successful submission transitions the instance to the `under-review` state and displays a confirmation panel. The user is informed their request is under review and can expect a response within the selected urgency window.

### Service Desk handoff (development only)

Because this walkthrough pauses in an `under-review` state, the next actor in the local demo is the development-only [Service Blueprint Administration](service-request-administration.md) panel. From there a reviewer can **Request Changes** to send the member back to the authored form, or **Approve** to push the service blueprint to its terminal completion state.

## V2.0 Schema Example

The date input uses the polymorphic `date` component:

```json
{
  "type": "date",
  "fieldKey": "date-of-birth",
  "label": "Date of birth",
  "hint": "For example, 15 03 1985",
  "required": true
}
```

The urgency field demonstrates a straightforward radio-driven priority choice:

```json
{
  "type": "radios",
  "fieldKey": "urgency",
  "label": "Request urgency",
  "required": true,
  "options": [
    { "value": "standard", "label": "Standard (5-7 working days)" },
    { "value": "urgent", "label": "Urgent (2 working days)" },
    { "value": "critical", "label": "Critical (same day)" }
  ]
}
```

## Service Blueprint Seed JSON

**Location:** `src/UmbracoPrism.MockBusinessApp/service-blueprints/information-request.json`

**Definition key:** `information-request`  
**Display name:** Request Information  
**States:** `collecting-info` → `under-review` → `complete`

## Key Takeaways

✅ **Date inputs** – GDS-style date components with validation  
✅ **Textareas** – Multi-line text input with character limits  
✅ **Urgency triage** – Clear priority options for operator review  
✅ **Review service blueprint** – Public submission followed by reviewer action in the local harness

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`information-request.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/information-request.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.claude/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
