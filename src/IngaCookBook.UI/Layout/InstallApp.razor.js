let listening = false;
let initialized = false;
let installPrompt;
let installed = false;
let prompting = false;
const standalone = matchMedia('(display-mode: standalone)');

// Capture the browser's one-shot prompt before the Blazor startup completes.
export function listenForInstallation() {
    if (listening) return;
    listening = true;
    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        installPrompt = event;
        render();
    });
    window.addEventListener('appinstalled', () => {
        installed = true;
        installPrompt = undefined;
        render();
    });
}

export function initializeInstallation(blazor) {
    if (initialized) return;
    initialized = true;
    listenForInstallation();
    document.addEventListener('click', async event => {
        if (!event.composedPath().some(node => node instanceof Element && node.matches('[data-install-button]'))) return;
        if (!installPrompt || prompting) return;
        const prompt = installPrompt;
        installPrompt = undefined;
        prompting = true;
        try {
            await prompt.prompt();
            const choice = await prompt.userChoice;
            if (choice.outcome === 'accepted') installed = true;
        } catch {
            // Installation is optional. Browser-menu instructions remain available.
        } finally {
            prompting = false;
            render();
        }
    });
    standalone.addEventListener('change', render);
    blazor.addEventListener('enhancedload', render);
    render();
}

function render() {
    const hidden = installed || standalone.matches || navigator.standalone === true;
    const appleMobile = /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    for (const panel of document.querySelectorAll('[data-install-app]')) {
        panel.hidden = hidden;
        panel.querySelector('[data-install-prompt]').hidden = !installPrompt || prompting;
        const help = panel.querySelector('[data-install-help]');
        help.hidden = !!installPrompt;
        help.textContent = appleMobile
            ? 'In Safari, open Share, then choose Add to Home Screen. You may need to scroll down the Share menu.'
            : 'Use your browser’s menu to install this app or add it to your home screen, if available.';
    }
}
