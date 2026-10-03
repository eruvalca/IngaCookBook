// Set the canvas before the first paint; Fluent receives the same mode on initialization.
(() => {
    let mode = 'system';
    try { mode = localStorage.getItem('ingacookbook.appearance') ?? 'system'; } catch { /* Storage may be disabled. */ }
    const dark = mode === 'dark' || (mode !== 'light' && matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.dataset.appearance = dark ? 'dark' : 'light';
    document.documentElement.style.colorScheme = dark ? 'dark' : 'light';
})();
