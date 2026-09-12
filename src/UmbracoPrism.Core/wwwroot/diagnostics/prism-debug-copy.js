// "Copy" buttons on the dev-only Prism debug panel (see PrismDebugTagHelper). Wired via event
// delegation reading each button's own data-copy-target attribute (the id of the element whose
// text to copy) rather than an inline onclick="..." handler — CSP's script-src restrictions
// apply to inline event handler attributes exactly like literal <script> bodies.
document.addEventListener('click', event => {
    const button = event.target.closest('[data-action="copy-to-clipboard"]');
    if (!button) {
        return;
    }

    const target = document.getElementById(button.dataset.copyTarget);
    if (!target) {
        return;
    }

    navigator.clipboard.writeText(target.innerText).then(() => {
        const original = button.innerText;
        button.innerText = 'Copied!';
        setTimeout(() => { button.innerText = original; }, 2000);
    });
});
