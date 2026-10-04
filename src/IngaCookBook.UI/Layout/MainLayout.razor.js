export function initializeNavigation(blazor) {
    // The static shell can outlive its original URL during interactive routing.
    // Move keyboard focus within the current document without navigating away.
    document.addEventListener('click', event => {
        if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        if (!(event.target instanceof Element) || !event.target.closest('a.skip-link')) return;
        const main = document.getElementById('main-content');
        if (!main) return;
        event.preventDefault();
        main.focus();
        main.scrollIntoView();
    });

    // Static SSR doesn't execute FluentNavItem's interactive drawer-close handler.
    // Close the web component before Blazor patches the next page into the document.
    blazor.addEventListener('enhancednavigationstart', () => {
        for (const drawer of document.querySelectorAll('.app-layout fluent-drawer[hamburger]')) {
            if (typeof drawer.hide === 'function') {
                drawer.hide();
            }
        }
    });
}
