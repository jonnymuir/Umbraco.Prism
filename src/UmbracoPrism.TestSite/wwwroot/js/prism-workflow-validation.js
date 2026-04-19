/**
 * prism-workflow-validation.js
 * Client-side validation for Prism workflow forms
 * WCAG 2.2 AA compliant, progressively enhanced, vanilla JS
 */

(function() {
    'use strict';

    // Wait for DOM to be ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function init() {
        const forms = document.querySelectorAll('form');
        
        forms.forEach(form => {
            // Suppress native browser validation UI (we provide our own)
            form.noValidate = true;

            // Attach blur validation to all form controls
            attachBlurValidation(form);

            // Add character counters to textareas with length constraints
            attachCharacterCounters(form);

            // Intercept form submission for smart error handling
            form.addEventListener('submit', handleFormSubmit);
        });
    }

    /**
     * Attach blur event handlers to all validatable inputs
     */
    function attachBlurValidation(form) {
        const inputs = form.querySelectorAll('input, select, textarea');
        
        inputs.forEach(input => {
            // Skip buttons, hidden inputs, and checkbox/radio (they don't blur in the same way)
            if (input.type === 'submit' || input.type === 'button' || input.type === 'hidden') {
                return;
            }

            input.addEventListener('blur', function() {
                validateField(this);
            });

            // Also validate on change for select, checkbox, radio
            if (input.tagName === 'SELECT' || input.type === 'checkbox' || input.type === 'radio') {
                input.addEventListener('change', function() {
                    validateField(this);
                });
            }
        });
    }

    /**
     * Validate a single field using HTML5 Constraint Validation API.
     * For radio inputs, validates the whole group by name.
     */
    function validateField(input) {
        const isValid = input.validity.valid;
        const fieldWrapper = findFieldWrapper(input);

        // Radio buttons: use group name so all radios share one error element
        const fieldKey = input.type === 'radio'
            ? input.name.replace(/^fields\[/, '').replace(/\]$/, '')
            : (input.id || input.name.replace(/^fields\[/, '').replace(/\]$/, ''));
        const errorId = `${fieldKey}-error`;
        const label = input.getAttribute('data-label') || fieldKey;

        if (!isValid) {
            const errorMessage = getValidationMessage(input, label);
            showError(input, fieldWrapper, errorId, errorMessage, input.type === 'radio');
        } else {
            clearError(input, fieldWrapper, errorId);
        }
    }

    /**
     * Get a friendly validation message based on ValidityState
     */
    function getValidationMessage(input, label) {
        const validity = input.validity;

        if (validity.valueMissing) {
            return `${label} is required.`;
        }
        if (validity.typeMismatch) {
            if (input.type === 'email') {
                return `${label} must be a valid email address.`;
            }
            return `${label} is not in the expected format.`;
        }
        if (validity.tooShort) {
            return `${label} must be at least ${input.minLength} characters.`;
        }
        if (validity.tooLong) {
            return `${label} must be no more than ${input.maxLength} characters.`;
        }
        if (validity.patternMismatch) {
            return `${label} is not in the expected format.`;
        }
        if (validity.rangeUnderflow) {
            return `${label} must be at least ${input.min}.`;
        }
        if (validity.rangeOverflow) {
            return `${label} must be no more than ${input.max}.`;
        }
        if (validity.stepMismatch) {
            return `${label} is not a valid value.`;
        }
        if (validity.badInput) {
            return `${label} is not in the expected format.`;
        }

        // Fallback
        return `${label} is invalid.`;
    }

    /**
     * Show error message and update field state
     */
    function showError(input, fieldWrapper, errorId, message, isRadioGroup) {
        // For radio groups, mark aria-invalid on the fieldset instead of the input
        if (isRadioGroup) {
            const fieldset = input.closest('fieldset.prism-fieldset');
            if (fieldset) fieldset.setAttribute('aria-invalid', 'true');
        } else {
            input.setAttribute('aria-invalid', 'true');
        }

        // Add error class to wrapper
        if (fieldWrapper) {
            fieldWrapper.classList.add('prism-form-group--error');
        }

        // Find or create error element
        let errorElement = document.getElementById(errorId);
        
        if (!errorElement) {
            // Create new error element
            errorElement = document.createElement('p');
            errorElement.id = errorId;
            errorElement.className = 'prism-field-error';
            errorElement.setAttribute('role', 'alert');

            // Insert after label/hint but before input
            const insertPoint = findErrorInsertionPoint(input);
            if (insertPoint && insertPoint.parentNode) {
                insertPoint.parentNode.insertBefore(errorElement, insertPoint);
            }

            // Update aria-describedby
            updateAriaDescribedBy(input, errorId, true);
        }

        errorElement.textContent = message;
    }

    /**
     * Clear error message and update field state
     */
    function clearError(input, fieldWrapper, errorId) {
        // Set aria-invalid to false (not removed - WCAG requirement)
        input.setAttribute('aria-invalid', 'false');

        // Remove error class from wrapper
        if (fieldWrapper) {
            fieldWrapper.classList.remove('prism-form-group--error');
        }

        // Hide error element if it exists (but don't remove it - keep for server errors)
        const errorElement = document.getElementById(errorId);
        if (errorElement) {
            errorElement.textContent = '';
            errorElement.style.display = 'none';
        }
    }

    /**
     * Find the parent field wrapper (.prism-form-group)
     */
    function findFieldWrapper(input) {
        return input.closest('.prism-form-group');
    }

    /**
     * Find the correct insertion point for error message
     * (after label/hint, before input)
     */
    function findErrorInsertionPoint(input) {
        // For fieldsets (radio/checkbox groups), insert after hint or legend
        const fieldset = input.closest('fieldset.prism-fieldset');
        if (fieldset) {
            const hint = fieldset.querySelector('.prism-hint');
            if (hint) return hint.nextSibling;
            const legend = fieldset.querySelector('legend');
            return legend ? legend.nextSibling : input;
        }

        // For regular inputs, insert after hint or label
        const wrapper = findFieldWrapper(input);
        if (wrapper) {
            const hint = wrapper.querySelector('.prism-hint');
            if (hint) return hint.nextSibling;
            const label = wrapper.querySelector('label');
            return label ? label.nextSibling : input;
        }

        return input;
    }

    /**
     * Update aria-describedby to include/exclude error element
     */
    function updateAriaDescribedBy(input, errorId, include) {
        const describedBy = input.getAttribute('aria-describedby') || '';
        const parts = describedBy.split(' ').filter(id => id);

        if (include && !parts.includes(errorId)) {
            parts.push(errorId);
        } else if (!include) {
            const index = parts.indexOf(errorId);
            if (index > -1) parts.splice(index, 1);
        }

        if (parts.length > 0) {
            input.setAttribute('aria-describedby', parts.join(' '));
        } else {
            input.removeAttribute('aria-describedby');
        }
    }

    /**
     * Attach character counters to textareas with length constraints
     */
    function attachCharacterCounters(form) {
        const textareas = form.querySelectorAll('textarea[minlength], textarea[maxlength]');

        textareas.forEach(textarea => {
            const minLength = textarea.minLength || 0;
            const maxLength = textarea.maxLength || Infinity;
            
            // Create counter element
            const counter = document.createElement('p');
            counter.className = 'prism-field-hint prism-field-char-count';
            counter.setAttribute('aria-live', 'polite');
            counter.setAttribute('aria-atomic', 'true');

            // Insert after textarea
            if (textarea.nextSibling) {
                textarea.parentNode.insertBefore(counter, textarea.nextSibling);
            } else {
                textarea.parentNode.appendChild(counter);
            }

            // Update counter function
            function updateCounter() {
                const currentLength = textarea.value.length;
                const remaining = maxLength < Infinity ? maxLength : null;

                let text = `${currentLength}`;
                if (remaining !== null) {
                    text += ` / ${remaining}`;
                }
                text += ' characters';

                counter.textContent = text;

                // Apply warning/error classes based on threshold
                counter.classList.remove('prism-field-char-count--warning', 'prism-field-char-count--error');
                
                if (remaining !== null) {
                    const percentUsed = (currentLength / remaining) * 100;
                    
                    if (currentLength >= remaining) {
                        counter.classList.add('prism-field-char-count--error');
                    } else if (percentUsed > 80) {
                        counter.classList.add('prism-field-char-count--warning');
                    }
                }
            }

            // Initialize and attach listener
            updateCounter();
            textarea.addEventListener('input', updateCounter);
        });
    }

    /**
     * Handle form submission - scroll to first error
     */
    function handleFormSubmit(event) {
        const form = event.target;

        // Check for client-side validation errors
        const isClientValid = form.checkValidity();

        // Find all fields with errors (client or server)
        const errorFields = Array.from(form.querySelectorAll('[aria-invalid="true"], .prism-form-group--error input, .prism-form-group--error select, .prism-form-group--error textarea'));

        // If there are client-side errors, prevent submission
        if (!isClientValid) {
            event.preventDefault();

            // Validate all fields — deduplicate radio groups by name to avoid multiple errors
            const seenRadioNames = new Set();
            const inputs = form.querySelectorAll('input, select, textarea');
            inputs.forEach(input => {
                if (input.type === 'submit' || input.type === 'button' || input.type === 'hidden') return;
                if (input.type === 'radio') {
                    if (seenRadioNames.has(input.name)) return;
                    seenRadioNames.add(input.name);
                }
                validateField(input);
            });

            // Collect errors for summary: one entry per error element
            const errorEntries = [];
            form.querySelectorAll('.prism-field-error').forEach(el => {
                if (el.textContent.trim()) {
                    errorEntries.push({ id: el.id, message: el.textContent.trim() });
                }
            });
            showErrorSummary(form, errorEntries);

            // Re-find error fields after validation
            const updatedErrorFields = Array.from(form.querySelectorAll('[aria-invalid="true"]'));
            scrollToErrorSummary(form);
            
            return;
        }

        // Remove any client-side summary if the form is now valid
        removeErrorSummary(form);

        // If form is valid but there are server-rendered errors, scroll to them
        if (errorFields.length > 0) {
            scrollToFirstError(errorFields);
        }
    }

    /**
     * Show or update the GDS error summary at the top of the form
     */
    function showErrorSummary(form, errors) {
        const SUMMARY_ID = 'prism-client-error-summary';
        let summary = document.getElementById(SUMMARY_ID);

        if (!summary) {
            summary = document.createElement('div');
            summary.id = SUMMARY_ID;
            summary.className = 'govuk-error-summary';
            summary.setAttribute('data-module', 'govuk-error-summary');
            form.insertBefore(summary, form.firstChild);
        }

        const items = errors.map(e => {
            const href = e.id ? ` href="#${e.id}"` : '';
            return `<li><a${href}>${e.message}</a></li>`;
        }).join('');

        summary.innerHTML = `<div role="alert">
            <h2 class="govuk-error-summary__title">There is a problem</h2>
            <div class="govuk-error-summary__body">
                <ul class="govuk-list govuk-error-summary__list">${items}</ul>
            </div>
        </div>`;
    }

    /**
     * Remove the client-side error summary from the form
     */
    function removeErrorSummary(form) {
        const summary = document.getElementById('prism-client-error-summary');
        if (summary) summary.remove();
    }

    /**
     * Scroll to the error summary at the top of the form
     */
    function scrollToErrorSummary(form) {
        const summary = document.getElementById('prism-client-error-summary');
        if (summary) {
            summary.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    /**
     * Scroll to and focus the first error field
     */
    function scrollToFirstError(errorFields) {
        if (errorFields.length === 0) return;

        const firstErrorField = errorFields[0];
        const wrapper = findFieldWrapper(firstErrorField);

        // Scroll to the field wrapper
        const target = wrapper || firstErrorField;
        target.scrollIntoView({ behavior: 'smooth', block: 'center' });

        // Focus the input after a short delay (to allow smooth scroll)
        setTimeout(() => {
            firstErrorField.focus();
        }, 300);
    }

})();
