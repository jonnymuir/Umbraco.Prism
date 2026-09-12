// Subscribe/waitlist demo actions for VinylRecord.cshtml. Wired via event delegation reading
// data-* attributes (genre/content key are per-page values, set by the server) rather than
// inline onclick="..." handlers — CSP's script-src restrictions apply to inline event handler
// attributes exactly like literal <script> bodies, so this needs no unsafe-inline exception.
document.addEventListener('click', event => {
    const subscribeButton = event.target.closest('[data-action="subscribe-genre"]');
    if (subscribeButton) {
        subscribeToGenre(subscribeButton.dataset.genre);
        return;
    }

    const waitlistButton = event.target.closest('[data-action="join-waitlist"]');
    if (waitlistButton) {
        joinWaitlist(waitlistButton.dataset.contentKey);
    }
});

function subscribeToGenre(genre) {
    // TODO: Wire to PrismNotificationService API
    alert('Subscribe to ' + genre + ' notifications - will call /umbraco/api/prismnotification/subscribe');
    console.log('Subscribe to genre:', genre);

    // Example API call (to be implemented):
    // fetch('/umbraco/api/prismnotification/subscribe', {
    //     method: 'POST',
    //     headers: { 'Content-Type': 'application/json' },
    //     body: JSON.stringify({
    //         userId: 'current-user-id',  // From auth context
    //         tenantId: 'current-tenant',  // From tenant context
    //         genre: genre
    //     })
    // });
}

function joinWaitlist(contentKey) {
    // TODO: Wire to waitlist API (when Blathers implements it)
    alert('Join waitlist for this vinyl - feature to be implemented');
    console.log('Join waitlist for:', contentKey);
}
