// Set up event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
const sessionRecovery = document.getElementById("components-session-recovery");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);
reconnectModal.addEventListener("close", () => {
    // Reviewing inputs (including dismissing with Escape) must leave a recovery
    // action available without relying on a live circuit or a newer release.
    if (!reconnectModal.open && reconnectModal.classList.contains("components-reconnect-rejected")) {
        sessionRecovery.hidden = false;
    }
});

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

document.getElementById("components-reload-button").addEventListener("click", () => location.reload());
document.getElementById("components-session-refresh-button").addEventListener("click", () => location.reload());
document.getElementById("components-review-inputs-button").addEventListener("click", () => reconnectModal.close());

function handleReconnectStateChanged(event) {
    if (event.detail.state === "show") {
        sessionRecovery.hidden = true;
        reconnectModal.showModal();
    } else if (event.detail.state === "hide") {
        document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        sessionRecovery.hidden = true;
        reconnectModal.close();
    } else if (event.detail.state === "failed") {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (event.detail.state === "rejected") {
        showRefreshRequired();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

    try {
        // Reconnect will asynchronously return:
        // - true to mean success
        // - false to mean we reached the server, but it rejected the connection (e.g., unknown circuit ID)
        // - exception to mean we didn't reach the server (this can be sync or async)
        const successful = await Blazor.reconnect();
        if (!successful) {
            // We have been able to reach the server, but the circuit is no longer available.
            // Offer an explicit refresh if the saved circuit cannot be resumed.
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                showRefreshRequired();
            } else {
                reconnectModal.close();
            }
        }
    } catch (err) {
        // We got an exception, server is currently unavailable
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            showRefreshRequired();
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

function showRefreshRequired() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    sessionRecovery.hidden = true;
    reconnectModal.className = "components-reconnect-rejected";
    if (!reconnectModal.open) reconnectModal.showModal();
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
