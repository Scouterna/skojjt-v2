// Minimal service worker for PWA installability.
// Blazor Server requires a live connection, so offline caching is not used.

self.addEventListener('install', (event) => {
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', (event) => {
    // Pass all requests through to the network.
    // Blazor Server apps need a live server connection to function.
});
