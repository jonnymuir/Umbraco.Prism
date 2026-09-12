// Subscribe demo action for VinylGenreLanding.cshtml. Wired via event delegation reading the
// button's own data-genre attribute (a per-page value set by the server) rather than an inline
// onclick="..." handler — see vinyl-record-actions.js's own comment for why.
document.addEventListener('click', event => {
    const subscribeButton = event.target.closest('[data-action="toggle-subscription"]');
    if (subscribeButton) {
        toggleSubscription(subscribeButton.dataset.genre);
    }
});

function toggleSubscription(genre) {
    // This would call the Prism notification subscription API
    // For now, just show an alert
    alert('Subscription feature will be wired to /umbraco/api/prismnotification/subscribe endpoint');
    console.log('Toggle subscription for genre:', genre);

    // TODO: Integrate with PrismNotificationController
    // fetch('/umbraco/api/prismnotification/subscribe', {
    //     method: 'POST',
    //     headers: { 'Content-Type': 'application/json' },
    //     body: JSON.stringify({ genre: genre })
    // });
}
