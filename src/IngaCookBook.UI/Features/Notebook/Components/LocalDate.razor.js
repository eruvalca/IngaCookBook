export function initializeLocalDates() {
    const format = new Intl.DateTimeFormat(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
    function update() {
        for (const element of document.querySelectorAll('time[data-local-date]')) {
            const date = new Date(element.dateTime);
            if (Number.isNaN(date.getTime())) continue;
            const text = format.format(date);
            // Avoid observing our own update indefinitely. Also restore localization
            // when an interactive render or enhanced SSR replaces the fallback.
            if (element.textContent !== text) element.textContent = text;
        }
    }
    update();
    new MutationObserver(update).observe(document.body, {
        subtree: true, childList: true, characterData: true,
        attributes: true, attributeFilter: ['datetime']
    });
}
