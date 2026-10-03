const preferenceKey = 'ingacookbook.appearance';
let initialized = false;

function preference() {
    try {
        const value = localStorage.getItem(preferenceKey);
        return ['system', 'light', 'dark'].includes(value) ? value : 'system';
    } catch {
        return 'system';
    }
}

export function initializeAppearance(blazor) {
    if (initialized) return;
    initialized = true;
    let selected = preference();
    const media = matchMedia('(prefers-color-scheme: dark)');
    const apply = () => {
        const mode = selected;
        const dark = mode === 'dark' || (mode === 'system' && media.matches);
        document.documentElement.dataset.appearance = dark ? 'dark' : 'light';
        document.documentElement.style.colorScheme = dark ? 'dark' : 'light';
        document.body.dataset.theme = dark ? 'dark' : 'light';
        const theme = blazor.theme ?? globalThis.Microsoft?.FluentUI?.Blazor?.Utilities?.Theme;
        theme?.setBrandThemeFromSettings({
            color: '#E4786D', hueTorsion: 0, vibrancy: 0,
            mode: dark ? 'dark' : 'light', isExact: false
        });
        for (const picker of document.querySelectorAll('[data-theme-picker]')) {
            picker.value = mode;
        }
    };
    document.addEventListener('change', event => {
        if (event.target instanceof HTMLSelectElement && event.target.matches('[data-theme-picker]')) {
            selected = event.target.value;
            try { localStorage.setItem(preferenceKey, event.target.value); } catch { /* Storage may be disabled. */ }
            apply();
        }
    });
    document.addEventListener('click', event => {
        if (event.target instanceof Element && event.target.closest('[data-print-recipe]')) {
            window.print();
        }
    });
    media.addEventListener('change', () => { if (selected === 'system') apply(); });
    window.addEventListener('storage', event => { if (event.key === preferenceKey) { selected = preference(); apply(); } });
    blazor.addEventListener('enhancedload', apply);
    apply();
}
