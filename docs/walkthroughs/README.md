# Workflow Walkthroughs

Screenshot-driven walkthroughs demonstrating the polymorphic component model in action.

Each walkthrough shows:
- Real workflow pages running in the TestSite
- The fluent builder API used to define them  
- The polymorphic JSON schema that powers them

## Available Walkthroughs

### [Community Enquiry](community-enquiry.md)
Multi-section contact form with conditional radios, checkboxes, and validation.

### [Payment Demo](payment-demo.md)
Two-step workflow with stripe integration, currency formatting, and check-answers pattern.

### [Planning Notification](planning-notification.md)
Complex multi-page application with file upload, address lookup, and progressive disclosure.

### [Information Request](information-request.md)
Data request form with date picker, textarea, and conditional urgency options.

---

**Note:** These walkthroughs capture the current schema in production use. All workflows use the polymorphic component model (`type` discriminator, `children[]` arrays, `conditionalChildren` on Radios/Checkboxes).
