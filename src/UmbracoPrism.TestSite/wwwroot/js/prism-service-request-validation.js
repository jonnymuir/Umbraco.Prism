/**
 * prism-service-request-validation.js
 *
 * Attaches a live character counter to any textarea[minlength]/[maxlength] on a service
 * request form. Field validation itself is server-side only (see the comment in init()) —
 * this file used to also do client-side blur/submit validation, but that whole path was
 * unreachable dead code (defined, never called) left over from before that decision; removed
 * rather than kept around as a trap for the next person who assumes calling it does something.
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
            // Suppress native browser validation UI (we provide our own server-side)
            form.noValidate = true;

            // Add character counters to textareas with length constraints
            attachCharacterCounters(form);

            // Note: Validation is handled server-side. The server renders the GDS
            // error summary (role="alert") via <prism-error-summary> and per-field
            // <p class="govuk-error-message"> entries when validation fails. Doing
            // the same on the client (blur or submit) caused layout shift between
            // mousedown and mouseup that broke submit clicks, and produced
            // competing error summaries that fought the server-rendered one.
        });
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

})();
