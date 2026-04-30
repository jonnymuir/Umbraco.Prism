# Using GDS Design System Components in Workflow Steps

A comprehensive reference for using GOV.UK Design System components in Umbraco.Prism workflow step partials.

## Overview

**govuk-frontend 5.9.0 is already installed. No setup needed.**

Every workflow step partial has full access to all 38 GDS components. The CSS and JavaScript are automatically loaded on every page, and all JS-enhanced components are initialized automatically.

## How GDS is Bundled

Prism handles the entire GDS setup for you:

1. **Dependency declared:** `src/UmbracoPrism.TestSite/package.json` includes `"govuk-frontend": "^5.9.0"`
2. **MSBuild runs `npm ci`:** An `InstallGovukFrontend` target in the `.csproj` runs before every build
3. **Assets copied:** `govuk-frontend.min.css` and `govuk-frontend.min.js` are automatically copied to `wwwroot/css/` and `wwwroot/js/`
4. **Master layout loads them:** `Master.cshtml` includes both files and calls `window.GOVUKFrontend.initAll()` on every page

**Result:** All GDS components work out of the box. Just add the CSS classes and (for JS components) the `data-module` attribute.

## Verifying GDS is Loaded

Open your browser's DevTools (F12) on any workflow page:

1. **Console tab:** Type `window.GOVUKFrontend` and press Enter. You should see an object with methods like `initAll()`.
2. **Network tab:** Refresh the page and filter by "govuk". You should see `govuk-frontend.min.css` and `govuk-frontend.min.js` loaded with 200 status.
3. **Elements tab:** Inspect a button or input. You should see GDS classes like `govuk-button` or `govuk-input` applied.

If any of these checks fail, the build may have failed to copy the GDS assets. Run `npm ci` manually in the TestSite directory and check for errors.

## Component Catalogue

This section provides usage examples for the most useful GDS components in workflow contexts.

---

## Form Elements

### Button

**Purpose:** Primary, secondary, and warning action buttons.

**CSS classes:**
- `govuk-button` — primary button (green, high emphasis)
- `govuk-button govuk-button--secondary` — secondary button (grey, medium emphasis)
- `govuk-button govuk-button--warning` — warning button (red, destructive actions)
- `govuk-button--start` — start button (with arrow icon)

**Requires JS:** Yes, add `data-module="govuk-button"` for proper focus and keyboard handling.

**Example:**

```cshtml
<!-- Primary button (default) -->
<button type="submit" class="govuk-button" data-module="govuk-button">
    Continue
</button>

<!-- Secondary button -->
<button type="button" class="govuk-button govuk-button--secondary" data-module="govuk-button">
    Save draft
</button>

<!-- Warning button -->
<button type="submit" class="govuk-button govuk-button--warning" data-module="govuk-button">
    Delete application
</button>

<!-- Start button (with arrow) -->
<a href="/start" class="govuk-button govuk-button--start" data-module="govuk-button">
    Start now
    <svg class="govuk-button__start-icon" xmlns="http://www.w3.org/2000/svg" width="17.5" height="19" viewBox="0 0 33 40">
        <path fill="currentColor" d="M0 0h13l20 20-20 20H0l20-20z" />
    </svg>
</a>

<!-- Button group (multiple buttons side-by-side) -->
<div class="govuk-button-group">
    <button type="submit" class="govuk-button" data-module="govuk-button">Continue</button>
    <button type="submit" name="Action" value="back" class="govuk-button govuk-button--secondary" data-module="govuk-button">Back</button>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/button/

---

### Text Input

**Purpose:** Single-line text entry (name, email, short answers).

**CSS classes:**
- `govuk-input` — base input class
- `govuk-input--width-{n}` — fixed width variants (2, 3, 4, 5, 10, 20, 30 characters)
- `govuk-input--error` — error state (red border)

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-form-group">
    <label class="govuk-label" for="full-name">
        Full name
    </label>
    <input class="govuk-input" id="full-name" name="full-name" type="text" />
</div>

<!-- Email input with hint -->
<div class="govuk-form-group">
    <label class="govuk-label" for="email">
        Email address
    </label>
    <div id="email-hint" class="govuk-hint">
        We'll use this to send you a confirmation
    </div>
    <input class="govuk-input govuk-input--width-20" 
           id="email" 
           name="email" 
           type="email" 
           aria-describedby="email-hint" />
</div>

<!-- Input with error -->
<div class="govuk-form-group govuk-form-group--error">
    <label class="govuk-label" for="postcode">
        Postcode
    </label>
    <p id="postcode-error" class="govuk-error-message">
        <span class="govuk-visually-hidden">Error:</span> Enter a valid postcode
    </p>
    <input class="govuk-input govuk-input--width-10 govuk-input--error" 
           id="postcode" 
           name="postcode" 
           type="text" 
           aria-describedby="postcode-error" />
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/text-input/

---

### Textarea

**Purpose:** Multi-line text entry (descriptions, comments, long answers).

**CSS classes:**
- `govuk-textarea` — base textarea class
- `govuk-textarea--error` — error state

**Requires JS:** No (unless using character count, see below)

**Example:**

```cshtml
<div class="govuk-form-group">
    <label class="govuk-label" for="description">
        Describe the issue
    </label>
    <div id="description-hint" class="govuk-hint">
        Include as much detail as possible
    </div>
    <textarea class="govuk-textarea" 
              id="description" 
              name="description" 
              rows="5" 
              aria-describedby="description-hint"></textarea>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/textarea/

---

### Character Count

**Purpose:** Textarea with live character/word count display.

**CSS classes:**
- `govuk-character-count` — wrapper
- `govuk-textarea govuk-js-character-count` — textarea inside
- `govuk-character-count__message` — counter display

**Requires JS:** Yes, add `data-module="govuk-character-count"` and `data-maxlength="500"` (or `data-maxwords="150"`).

**Example:**

```cshtml
<div class="govuk-character-count" data-module="govuk-character-count" data-maxlength="500">
    <div class="govuk-form-group">
        <label class="govuk-label" for="more-detail">
            Can you provide more detail?
        </label>
        <div id="more-detail-hint" class="govuk-hint">
            Do not include personal or financial information
        </div>
        <textarea class="govuk-textarea govuk-js-character-count" 
                  id="more-detail" 
                  name="more-detail" 
                  rows="5" 
                  aria-describedby="more-detail-hint"></textarea>
    </div>
    <div class="govuk-hint govuk-character-count__message">
        You can enter up to 500 characters
    </div>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/character-count/

---

### Radios

**Purpose:** Single choice from a list of options.

**CSS classes:**
- `govuk-radios` — wrapper
- `govuk-radios__item` — each radio option
- `govuk-radios__input` — radio input
- `govuk-radios__label` — radio label
- `govuk-radios--inline` — display radios side-by-side (for short lists)

**Requires JS:** No (unless using conditional reveal, then add `data-module="govuk-radios"`)

**Example:**

```cshtml
<div class="govuk-form-group">
    <fieldset class="govuk-fieldset">
        <legend class="govuk-fieldset__legend govuk-fieldset__legend--l">
            <h1 class="govuk-fieldset__heading">
                Where do you live?
            </h1>
        </legend>
        <div class="govuk-radios">
            <div class="govuk-radios__item">
                <input class="govuk-radios__input" id="england" name="country" type="radio" value="england" />
                <label class="govuk-label govuk-radios__label" for="england">
                    England
                </label>
            </div>
            <div class="govuk-radios__item">
                <input class="govuk-radios__input" id="scotland" name="country" type="radio" value="scotland" />
                <label class="govuk-label govuk-radios__label" for="scotland">
                    Scotland
                </label>
            </div>
            <div class="govuk-radios__item">
                <input class="govuk-radios__input" id="wales" name="country" type="radio" value="wales" />
                <label class="govuk-label govuk-radios__label" for="wales">
                    Wales
                </label>
            </div>
        </div>
    </fieldset>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/radios/

---

### Checkboxes

**Purpose:** Multiple choices from a list of options.

**CSS classes:**
- `govuk-checkboxes` — wrapper
- `govuk-checkboxes__item` — each checkbox option
- `govuk-checkboxes__input` — checkbox input
- `govuk-checkboxes__label` — checkbox label
- `govuk-checkboxes--small` — smaller checkboxes (for long lists)

**Requires JS:** No (unless using conditional reveal, then add `data-module="govuk-checkboxes"`)

**Example:**

```cshtml
<div class="govuk-form-group">
    <fieldset class="govuk-fieldset">
        <legend class="govuk-fieldset__legend govuk-fieldset__legend--l">
            <h1 class="govuk-fieldset__heading">
                Which types of waste do you transport?
            </h1>
        </legend>
        <div class="govuk-checkboxes">
            <div class="govuk-checkboxes__item">
                <input class="govuk-checkboxes__input" id="waste-animal" name="waste" type="checkbox" value="animal" />
                <label class="govuk-label govuk-checkboxes__label" for="waste-animal">
                    Animal waste
                </label>
            </div>
            <div class="govuk-checkboxes__item">
                <input class="govuk-checkboxes__input" id="waste-chemical" name="waste" type="checkbox" value="chemical" />
                <label class="govuk-label govuk-checkboxes__label" for="waste-chemical">
                    Chemical waste
                </label>
            </div>
            <div class="govuk-checkboxes__item">
                <input class="govuk-checkboxes__input" id="waste-construction" name="waste" type="checkbox" value="construction" />
                <label class="govuk-label govuk-checkboxes__label" for="waste-construction">
                    Construction waste
                </label>
            </div>
        </div>
    </fieldset>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/checkboxes/

---

### Date Input

**Purpose:** Date entry (day, month, year as separate fields).

**CSS classes:**
- `govuk-date-input` — wrapper
- `govuk-date-input__item` — each field (day, month, year)
- `govuk-date-input__label` — field label
- `govuk-date-input__input` — field input

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-form-group">
    <fieldset class="govuk-fieldset">
        <legend class="govuk-fieldset__legend govuk-fieldset__legend--l">
            <h1 class="govuk-fieldset__heading">
                When did you start the work?
            </h1>
        </legend>
        <div id="start-date-hint" class="govuk-hint">
            For example, 27 3 2024
        </div>
        <div class="govuk-date-input" id="start-date">
            <div class="govuk-date-input__item">
                <div class="govuk-form-group">
                    <label class="govuk-label govuk-date-input__label" for="start-date-day">
                        Day
                    </label>
                    <input class="govuk-input govuk-date-input__input govuk-input--width-2" 
                           id="start-date-day" 
                           name="start-date-day" 
                           type="text" 
                           inputmode="numeric" />
                </div>
            </div>
            <div class="govuk-date-input__item">
                <div class="govuk-form-group">
                    <label class="govuk-label govuk-date-input__label" for="start-date-month">
                        Month
                    </label>
                    <input class="govuk-input govuk-date-input__input govuk-input--width-2" 
                           id="start-date-month" 
                           name="start-date-month" 
                           type="text" 
                           inputmode="numeric" />
                </div>
            </div>
            <div class="govuk-date-input__item">
                <div class="govuk-form-group">
                    <label class="govuk-label govuk-date-input__label" for="start-date-year">
                        Year
                    </label>
                    <input class="govuk-input govuk-date-input__input govuk-input--width-4" 
                           id="start-date-year" 
                           name="start-date-year" 
                           type="text" 
                           inputmode="numeric" />
                </div>
            </div>
        </div>
    </fieldset>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/date-input/

---

### Select (Dropdown)

**Purpose:** Single choice from a long list of options (prefer radios for short lists).

**CSS classes:**
- `govuk-select` — base select class
- `govuk-select--error` — error state

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-form-group">
    <label class="govuk-label" for="sort">
        Sort by
    </label>
    <select class="govuk-select" id="sort" name="sort">
        <option value="published">Recently published</option>
        <option value="updated" selected>Recently updated</option>
        <option value="views">Most views</option>
        <option value="comments">Most comments</option>
    </select>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/select/

---

### File Upload

**Purpose:** File selection (single or multiple files).

**CSS classes:**
- `govuk-file-upload` — base file input class
- `govuk-file-upload--error` — error state

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-form-group">
    <label class="govuk-label" for="file-upload-1">
        Upload a file
    </label>
    <input class="govuk-file-upload" id="file-upload-1" name="file-upload-1" type="file" />
</div>

<!-- Multiple files -->
<div class="govuk-form-group">
    <label class="govuk-label" for="file-upload-2">
        Upload your documents
    </label>
    <div id="file-upload-2-hint" class="govuk-hint">
        You can upload up to 10 files
    </div>
    <input class="govuk-file-upload" 
           id="file-upload-2" 
           name="file-upload-2" 
           type="file" 
           multiple 
           accept=".pdf,.jpg,.png" 
           aria-describedby="file-upload-2-hint" />
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/file-upload/

---

### Password Input

**Purpose:** Password entry with show/hide toggle.

**CSS classes:**
- `govuk-password-input` — wrapper
- `govuk-input govuk-password-input__input govuk-js-password-input-input` — password field
- `govuk-password-input__toggle` — show/hide button

**Requires JS:** Yes, add `data-module="govuk-password-input"` to wrapper.

**Example:**

```cshtml
<div class="govuk-form-group">
    <label class="govuk-label" for="password">
        Password
    </label>
    <div class="govuk-password-input" data-module="govuk-password-input">
        <input class="govuk-input govuk-password-input__input govuk-js-password-input-input" 
               id="password" 
               name="password" 
               type="password" 
               autocomplete="current-password" />
        <button type="button" 
                class="govuk-button govuk-button--secondary govuk-password-input__toggle govuk-js-password-input-toggle" 
                data-module="govuk-button" 
                hidden>
            Show
            <span class="govuk-visually-hidden">password</span>
        </button>
    </div>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/password-input/

---

## Content Components

### Summary List

**Purpose:** Display key-value pairs (review pages, check-your-answers pattern).

**CSS classes:**
- `govuk-summary-list` — wrapper
- `govuk-summary-list__row` — each key-value row
- `govuk-summary-list__key` — left column (label)
- `govuk-summary-list__value` — middle column (value)
- `govuk-summary-list__actions` — right column (change link)
- `govuk-summary-list--no-border` — remove row borders

**Requires JS:** No

**Example:**

```cshtml
<dl class="govuk-summary-list">
    <div class="govuk-summary-list__row">
        <dt class="govuk-summary-list__key">
            Name
        </dt>
        <dd class="govuk-summary-list__value">
            Sarah Philips
        </dd>
        <dd class="govuk-summary-list__actions">
            <a class="govuk-link" href="#">
                Change<span class="govuk-visually-hidden"> name</span>
            </a>
        </dd>
    </div>
    <div class="govuk-summary-list__row">
        <dt class="govuk-summary-list__key">
            Date of birth
        </dt>
        <dd class="govuk-summary-list__value">
            5 January 1978
        </dd>
        <dd class="govuk-summary-list__actions">
            <a class="govuk-link" href="#">
                Change<span class="govuk-visually-hidden"> date of birth</span>
            </a>
        </dd>
    </div>
    <div class="govuk-summary-list__row">
        <dt class="govuk-summary-list__key">
            Contact information
        </dt>
        <dd class="govuk-summary-list__value">
            72 Guild Street<br>London<br>SE23 6FH
        </dd>
        <dd class="govuk-summary-list__actions">
            <a class="govuk-link" href="#">
                Change<span class="govuk-visually-hidden"> contact information</span>
            </a>
        </dd>
    </div>
</dl>
```

**Official docs:** https://design-system.service.gov.uk/components/summary-list/

---

### Panel

**Purpose:** Confirmation/success messages (typically used on completion pages).

**CSS classes:**
- `govuk-panel` — base panel
- `govuk-panel--confirmation` — green confirmation variant
- `govuk-panel__title` — panel heading
- `govuk-panel__body` — panel content

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-panel govuk-panel--confirmation">
    <h1 class="govuk-panel__title">
        Application complete
    </h1>
    <div class="govuk-panel__body">
        Your reference number<br>
        <strong>HDJ2123F</strong>
    </div>
</div>

<p class="govuk-body">We have sent you a confirmation email.</p>
```

**Official docs:** https://design-system.service.gov.uk/components/panel/

---

### Inset Text

**Purpose:** Highlight important information (callouts, warnings, tips).

**CSS classes:**
- `govuk-inset-text` — base class

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-inset-text">
    It can take up to 8 weeks to register a lasting power of attorney if there are no mistakes in the application.
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/inset-text/

---

### Warning Text

**Purpose:** Critical warnings that users must read before proceeding.

**CSS classes:**
- `govuk-warning-text` — wrapper
- `govuk-warning-text__icon` — warning icon
- `govuk-warning-text__text` — warning content

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-warning-text">
    <span class="govuk-warning-text__icon" aria-hidden="true">!</span>
    <strong class="govuk-warning-text__text">
        <span class="govuk-visually-hidden">Warning</span>
        You can be fined up to £5,000 if you do not register.
    </strong>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/warning-text/

---

### Notification Banner

**Purpose:** Important messages at the top of pages (success, info, warnings).

**CSS classes:**
- `govuk-notification-banner` — wrapper
- `govuk-notification-banner--success` — green success variant
- `govuk-notification-banner__header` — banner header
- `govuk-notification-banner__title` — banner title
- `govuk-notification-banner__content` — banner body

**Requires JS:** No

**Example:**

```cshtml
<!-- Success banner -->
<div class="govuk-notification-banner govuk-notification-banner--success" role="alert">
    <div class="govuk-notification-banner__header">
        <h2 class="govuk-notification-banner__title" id="notification-success">
            Success
        </h2>
    </div>
    <div class="govuk-notification-banner__content">
        <h3 class="govuk-notification-banner__heading">
            Training outcome recorded and trainee withdrawn
        </h3>
        <p class="govuk-body">
            Contact <a class="govuk-notification-banner__link" href="#">example@department.gov.uk</a> if you think there's a problem.
        </p>
    </div>
</div>

<!-- Neutral banner -->
<div class="govuk-notification-banner" role="region" aria-labelledby="notification-title">
    <div class="govuk-notification-banner__header">
        <h2 class="govuk-notification-banner__title" id="notification-title">
            Important
        </h2>
    </div>
    <div class="govuk-notification-banner__content">
        <p class="govuk-notification-banner__heading">
            You have 7 days left to send your application.
        </p>
    </div>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/notification-banner/

---

### Tag

**Purpose:** Status indicators, labels, badges.

**CSS classes:**
- `govuk-tag` — base class
- `govuk-tag--grey` — grey (inactive)
- `govuk-tag--green` — green (completed)
- `govuk-tag--blue` — blue (new)
- `govuk-tag--purple` — purple (sent)
- `govuk-tag--pink` — pink (declined)
- `govuk-tag--red` — red (rejected)
- `govuk-tag--orange` — orange (pending)
- `govuk-tag--yellow` — yellow (delayed)

**Requires JS:** No

**Example:**

```cshtml
<strong class="govuk-tag">Completed</strong>
<strong class="govuk-tag govuk-tag--blue">New</strong>
<strong class="govuk-tag govuk-tag--orange">Pending</strong>
<strong class="govuk-tag govuk-tag--red">Rejected</strong>
<strong class="govuk-tag govuk-tag--grey">Inactive</strong>
```

**Official docs:** https://design-system.service.gov.uk/components/tag/

---

### Details (Expandable Section)

**Purpose:** Progressive disclosure—hide extra information until the user requests it.

**CSS classes:**
- `govuk-details` — wrapper
- `govuk-details__summary` — clickable summary text
- `govuk-details__text` — hidden content

**Requires JS:** No (uses native HTML `<details>` element)

**Example:**

```cshtml
<details class="govuk-details">
    <summary class="govuk-details__summary">
        <span class="govuk-details__summary-text">
            Help with nationality
        </span>
    </summary>
    <div class="govuk-details__text">
        We need to know your nationality so we can work out which elections you're entitled to vote in. If you cannot provide your nationality, you'll have to send copies of identity documents through the post.
    </div>
</details>
```

**Official docs:** https://design-system.service.gov.uk/components/details/

---

### Accordion

**Purpose:** Collapse/expand multiple sections of content.

**CSS classes:**
- `govuk-accordion` — wrapper
- `govuk-accordion__section` — each collapsible section
- `govuk-accordion__section-header` — section header
- `govuk-accordion__section-heading` — heading wrapper
- `govuk-accordion__section-button` — clickable button
- `govuk-accordion__section-content` — hidden content

**Requires JS:** Yes, add `data-module="govuk-accordion"` to wrapper.

**Example:**

```cshtml
<div class="govuk-accordion" data-module="govuk-accordion" id="accordion-default">
    <div class="govuk-accordion__section">
        <div class="govuk-accordion__section-header">
            <h2 class="govuk-accordion__section-heading">
                <button type="button" 
                        class="govuk-accordion__section-button" 
                        id="accordion-default-heading-1" 
                        aria-controls="accordion-default-content-1">
                    Writing well for the web
                </button>
            </h2>
        </div>
        <div id="accordion-default-content-1" 
             class="govuk-accordion__section-content" 
             aria-labelledby="accordion-default-heading-1">
            <p class="govuk-body">This is the content for section 1.</p>
        </div>
    </div>
    <div class="govuk-accordion__section">
        <div class="govuk-accordion__section-header">
            <h2 class="govuk-accordion__section-heading">
                <button type="button" 
                        class="govuk-accordion__section-button" 
                        id="accordion-default-heading-2" 
                        aria-controls="accordion-default-content-2">
                    Writing well for specialists
                </button>
            </h2>
        </div>
        <div id="accordion-default-content-2" 
             class="govuk-accordion__section-content" 
             aria-labelledby="accordion-default-heading-2">
            <p class="govuk-body">This is the content for section 2.</p>
        </div>
    </div>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/accordion/

---

### Tabs

**Purpose:** Organize related content into separate views (only one visible at a time).

**CSS classes:**
- `govuk-tabs` — wrapper
- `govuk-tabs__list` — tab navigation list
- `govuk-tabs__list-item` — each tab
- `govuk-tabs__tab` — tab link
- `govuk-tabs__panel` — tab content panel
- `govuk-tabs__panel--hidden` — hidden panel

**Requires JS:** Yes, add `data-module="govuk-tabs"` to wrapper.

**Example:**

```cshtml
<div class="govuk-tabs" data-module="govuk-tabs">
    <h2 class="govuk-tabs__title">Contents</h2>
    <ul class="govuk-tabs__list">
        <li class="govuk-tabs__list-item govuk-tabs__list-item--selected">
            <a class="govuk-tabs__tab" href="#past-day">
                Past day
            </a>
        </li>
        <li class="govuk-tabs__list-item">
            <a class="govuk-tabs__tab" href="#past-week">
                Past week
            </a>
        </li>
        <li class="govuk-tabs__list-item">
            <a class="govuk-tabs__tab" href="#past-month">
                Past month
            </a>
        </li>
    </ul>
    <div class="govuk-tabs__panel" id="past-day">
        <h2 class="govuk-heading-l">Past day</h2>
        <table class="govuk-table">
            <!-- table content -->
        </table>
    </div>
    <div class="govuk-tabs__panel govuk-tabs__panel--hidden" id="past-week">
        <h2 class="govuk-heading-l">Past week</h2>
        <table class="govuk-table">
            <!-- table content -->
        </table>
    </div>
    <div class="govuk-tabs__panel govuk-tabs__panel--hidden" id="past-month">
        <h2 class="govuk-heading-l">Past month</h2>
        <table class="govuk-table">
            <!-- table content -->
        </table>
    </div>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/tabs/

---

### Table

**Purpose:** Display tabular data.

**CSS classes:**
- `govuk-table` — base table
- `govuk-table__head` — table head
- `govuk-table__header` — column header
- `govuk-table__body` — table body
- `govuk-table__row` — table row
- `govuk-table__cell` — table cell
- `govuk-table__cell--numeric` — right-aligned numeric cell

**Requires JS:** No

**Example:**

```cshtml
<table class="govuk-table">
    <caption class="govuk-table__caption govuk-table__caption--m">Dates and amounts</caption>
    <thead class="govuk-table__head">
        <tr class="govuk-table__row">
            <th scope="col" class="govuk-table__header">Date</th>
            <th scope="col" class="govuk-table__header">Amount</th>
        </tr>
    </thead>
    <tbody class="govuk-table__body">
        <tr class="govuk-table__row">
            <td class="govuk-table__cell">First 6 weeks</td>
            <td class="govuk-table__cell govuk-table__cell--numeric">£109.80 per week</td>
        </tr>
        <tr class="govuk-table__row">
            <td class="govuk-table__cell">Next 33 weeks</td>
            <td class="govuk-table__cell govuk-table__cell--numeric">£109.80 per week</td>
        </tr>
        <tr class="govuk-table__row">
            <td class="govuk-table__cell">Total estimated pay</td>
            <td class="govuk-table__cell govuk-table__cell--numeric">£4,282.20</td>
        </tr>
    </tbody>
</table>
```

**Official docs:** https://design-system.service.gov.uk/components/table/

---

## Error Handling

### Error Summary

**Purpose:** List all validation errors at the top of a page (shown after form submission).

**CSS classes:**
- `govuk-error-summary` — wrapper
- `govuk-error-summary__title` — error summary heading
- `govuk-error-summary__list` — list of errors
- `govuk-error-summary__list-item` — each error

**Requires JS:** No (but focus management improves UX—focus the summary on page load)

**Example:**

```cshtml
<div class="govuk-error-summary" data-module="govuk-error-summary">
    <div role="alert">
        <h2 class="govuk-error-summary__title">
            There is a problem
        </h2>
        <div class="govuk-error-summary__body">
            <ul class="govuk-list govuk-error-summary__list">
                <li>
                    <a href="#full-name">Enter your full name</a>
                </li>
                <li>
                    <a href="#email">Enter a valid email address</a>
                </li>
            </ul>
        </div>
    </div>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/error-summary/

---

### Error Message

**Purpose:** Inline error message next to a specific field.

**CSS classes:**
- `govuk-error-message` — error message text
- `govuk-form-group--error` — apply to form group wrapper
- `govuk-input--error` — apply to input field

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-form-group govuk-form-group--error">
    <label class="govuk-label" for="email">
        Email address
    </label>
    <p id="email-error" class="govuk-error-message">
        <span class="govuk-visually-hidden">Error:</span> Enter an email address in the correct format, like name@example.com
    </p>
    <input class="govuk-input govuk-input--error" 
           id="email" 
           name="email" 
           type="email" 
           aria-describedby="email-error" />
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/error-message/

---

## Navigation Components

### Back Link

**Purpose:** Navigate back to the previous page.

**CSS classes:**
- `govuk-back-link` — styled back link with arrow

**Requires JS:** No (unless implementing SPA-style navigation)

**Example:**

```cshtml
<a href="#" class="govuk-back-link">Back</a>
```

**Official docs:** https://design-system.service.gov.uk/components/back-link/

---

### Breadcrumbs

**Purpose:** Show the user's location in a multi-level hierarchy.

**CSS classes:**
- `govuk-breadcrumbs` — wrapper
- `govuk-breadcrumbs__list` — breadcrumb list
- `govuk-breadcrumbs__list-item` — each breadcrumb
- `govuk-breadcrumbs__link` — breadcrumb link

**Requires JS:** No

**Example:**

```cshtml
<div class="govuk-breadcrumbs">
    <ol class="govuk-breadcrumbs__list">
        <li class="govuk-breadcrumbs__list-item">
            <a class="govuk-breadcrumbs__link" href="/">Home</a>
        </li>
        <li class="govuk-breadcrumbs__list-item">
            <a class="govuk-breadcrumbs__link" href="/section">Section</a>
        </li>
        <li class="govuk-breadcrumbs__list-item">
            <a class="govuk-breadcrumbs__link" href="/section/subsection">Subsection</a>
        </li>
    </ol>
</div>
```

**Official docs:** https://design-system.service.gov.uk/components/breadcrumbs/

---

## Best Practices

### Form Field Associations

Always link labels, hints, and errors to inputs using `aria-describedby`:

```cshtml
<div class="govuk-form-group">
    <label class="govuk-label" for="email">
        Email address
    </label>
    <div id="email-hint" class="govuk-hint">
        We'll use this to send you a receipt
    </div>
    <input class="govuk-input" 
           id="email" 
           name="email" 
           type="email" 
           aria-describedby="email-hint" />
</div>
```

For errors, include both hint and error IDs:

```cshtml
<input class="govuk-input govuk-input--error" 
       id="email" 
       name="email" 
       type="email" 
       aria-describedby="email-hint email-error" />
```

### Required Fields

Use the `required` attribute on inputs, and consider adding a visual indicator (like an asterisk) in the label:

```cshtml
<label class="govuk-label" for="full-name">
    Full name <span class="govuk-visually-hidden">(required)</span>
</label>
<input class="govuk-input" id="full-name" name="full-name" type="text" required />
```

### Keyboard Navigation

All GDS components are keyboard-accessible by default. Test by:
1. Tab through all interactive elements
2. Press Enter/Space to activate buttons and links
3. Use arrow keys in radios/checkboxes (when focused)

### Screen Reader Testing

Test with a screen reader (VoiceOver on macOS, NVDA on Windows):
1. Labels should be read aloud for every input
2. Hints should be announced after labels
3. Errors should be announced before inputs
4. Button purposes should be clear

---

## Full Official Documentation

For complete documentation, live examples, and accessibility guidance for all 38 components, see:

**https://design-system.service.gov.uk/components/**

Each component page includes:
- Live interactive examples
- Complete HTML markup
- When to use / when not to use
- Accessibility considerations
- Research and user testing notes

---

## Next Steps

- **Customise styles:** See [Customising Workflow UI](./workflow-customisation.md) for theming and CSS variable overrides
- **Create step types:** Learn how to create custom step partials using GDS components
- **Build workflows:** See [Setting Up a Prism Workflow](./workflow-setup.md) for workflow definition and state machine setup
