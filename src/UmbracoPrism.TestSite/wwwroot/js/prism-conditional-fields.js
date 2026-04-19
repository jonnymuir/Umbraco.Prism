// Prism conditional field visibility — progressive enhancement
// Watches trigger fields and shows/hides dependent fields based on their value.
(function () {
  'use strict';

  function initConditionalFields() {
    const conditionalFields = document.querySelectorAll('.prism-field--conditional');
    
    conditionalFields.forEach(function (fieldWrapper) {
      const triggerKey = fieldWrapper.dataset.conditionalOn;
      const visibleWhen = fieldWrapper.dataset.visibleWhen;
      
      if (!triggerKey || !visibleWhen) return;
      
      // Find the trigger input(s) — could be radio buttons (multiple) or a select (one)
      const triggerInputs = document.querySelectorAll(
        `[name="fields[${triggerKey}]"], [name="fields[${triggerKey}][]"]`
      );
      
      if (!triggerInputs.length) return;
      
      function getCurrentValue() {
        // For radio buttons, find the checked one
        const checked = document.querySelector(`[name="fields[${triggerKey}]"]:checked`);
        if (checked) return checked.value;
        // For select, get the value of the first (and only) element
        const select = document.querySelector(`select[name="fields[${triggerKey}]"]`);
        if (select) return select.value;
        // For text inputs
        const text = document.querySelector(`input[name="fields[${triggerKey}]"], textarea[name="fields[${triggerKey}]"]`);
        if (text) return text.value;
        return '';
      }
      
      function updateVisibility() {
        const currentValue = getCurrentValue();
        const shouldShow = currentValue === visibleWhen;
        
        if (shouldShow) {
          fieldWrapper.removeAttribute('hidden');
          // Re-enable inputs so they participate in constraint validation and submission
          const inputs = fieldWrapper.querySelectorAll('input, select, textarea');
          inputs.forEach(function (input) { input.disabled = false; });
          // Focus the first input when it appears
          const firstInput = fieldWrapper.querySelector('input:not([type="hidden"]), select, textarea');
          if (firstInput) {
            // Small delay for layout reflow
            setTimeout(function () { firstInput.focus(); }, 50);
          }
        } else {
          fieldWrapper.setAttribute('hidden', '');
          // Clear value and disable inputs (prevents constraint validation & stale submission)
          const inputs = fieldWrapper.querySelectorAll('input, select, textarea');
          inputs.forEach(function (input) {
            if (input.type === 'checkbox' || input.type === 'radio') {
              input.checked = false;
            } else {
              input.value = '';
            }
            input.disabled = true;
          });
        }
        
        // Update aria-hidden on the wrapper
        fieldWrapper.setAttribute('aria-hidden', shouldShow ? 'false' : 'true');
      }
      
      // Listen to all trigger inputs
      triggerInputs.forEach(function (input) {
        input.addEventListener('change', updateVisibility);
      });
      
      // Run once on init to set correct state (e.g., when re-rendering after server validation)
      updateVisibility();
    });
  }
  
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initConditionalFields);
  } else {
    initConditionalFields();
  }
})();
