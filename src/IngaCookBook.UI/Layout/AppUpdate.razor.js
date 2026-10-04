const checkInterval = 5 * 60 * 1000;
let initialized = false;
let loadedRelease;
let availableRelease;
let dismissedRelease;
let checking = false;
let lastCheck = 0;

export function initializeUpdates(blazor) {
    if (initialized) return;
    initialized = true;
    // Keep the version belonging to the loaded runtime, even after enhanced
    // navigation patches the document with HTML from a newer deployment.
    loadedRelease = document.querySelector('meta[name="app-release"]')?.content;
    if (!loadedRelease) return;
    document.addEventListener('click', event => {
        const path = event.composedPath();
        if (path.some(node => node instanceof Element && node.matches('[data-update-refresh]'))) {
            // Explicit user action only. Normal reload preserves the notebook's
            // existing beforeunload confirmation for unsaved inputs.
            location.reload();
        } else if (path.some(node => node instanceof Element && node.matches('[data-update-later]'))) {
            dismissedRelease = availableRelease;
            render();
        }
    });
    document.addEventListener('visibilitychange', () => { if (!document.hidden) void check(); });
    window.addEventListener('online', () => { void check(); });
    blazor.addEventListener('enhancedload', () => { render(); void check(); });
    setInterval(() => { void check(); }, checkInterval);
    void check();
}

async function check() {
    if (checking || document.hidden || Date.now() - lastCheck < checkInterval) return;
    checking = true;
    lastCheck = Date.now();
    try {
        const response = await fetch(new URL('app-version', document.baseURI), {
            cache: 'no-store', credentials: 'same-origin', signal: AbortSignal.timeout(10000)
        });
        if (!response.ok) return;
        const release = (await response.text()).trim();
        if (!/^[a-f0-9]{64}$/.test(release)) return;
        availableRelease = release !== loadedRelease ? release : undefined;
        render();
    } catch {
        // Connectivity and restarts must not interrupt cooking or imply a new release.
    } finally {
        checking = false;
    }
}

function render() {
    for (const panel of document.querySelectorAll('[data-app-update]')) {
        panel.hidden = !availableRelease || availableRelease === dismissedRelease;
    }
}
