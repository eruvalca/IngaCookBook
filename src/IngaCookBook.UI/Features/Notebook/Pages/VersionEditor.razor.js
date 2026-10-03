const historyKey = 'ingaNotebookIndex';
let initialized = false;
let position = 0;
let restoring = false;
let guard;

export function initializeEditorNavigation() {
    if (initialized) return;
    initialized = true;

    // Index same-document entries before enhanced navigation creates them. Preserve
    // Blazor's own history state; never add a duplicate/sentinel history entry.
    const pushState = history.pushState.bind(history);
    const replaceState = history.replaceState.bind(history);
    position = history.state?.[historyKey] ?? 0;
    replaceState({ ...history.state, [historyKey]: position }, '');
    history.pushState = (state, unused, url) => {
        pushState({ ...state, [historyKey]: position + 1 }, unused, url);
        position++;
    };
    history.replaceState = (state, unused, url) => {
        replaceState({ ...state, [historyKey]: position }, unused, url);
    };

    window.addEventListener('popstate', event => {
        if (restoring) {
            restoring = false;
            event.stopImmediatePropagation();
            return;
        }
        const destination = event.state?.[historyKey];
        if (destination === undefined) return; // Other documents use beforeunload.
        if (destination !== position && !confirmDiscard()) {
            // popstate cannot be canceled. Stop Blazor from replacing the editor,
            // then restore the original entry without adding or deleting history.
            event.stopImmediatePropagation();
            restoring = true;
            history.go(position - destination);
            return;
        }
        position = destination;
    }, { capture: true });
}

export function updateGuard(editor, dirty, savedRevision) {
    if (!editor.isConnected) return;
    if (guard?.editor !== editor) {
        guard?.dispose();
        guard = new EditorGuard(editor);
    }
    guard.update(dirty, savedRevision);
}

export function confirmDiscard() {
    return !guard?.isDirty || window.confirm(guard.editor.dataset.discardMessage ?? 'Leave without saving your recipe changes?');
}

export function disposeGuard(editor) {
    if (guard?.editor === editor) {
        guard.dispose();
        guard = undefined;
    }
}

class EditorGuard {
    #abort = new AbortController();
    #observer;
    #dirty = false;
    #pendingInput = false;
    #savedRevision;

    constructor(editor) {
        this.editor = editor;
        const options = { capture: true, signal: this.#abort.signal };
        // Protect typed input even before a server-side change event returns.
        editor.addEventListener('input', () => { this.#pendingInput = true; }, options);
        editor.addEventListener('change', () => { this.#pendingInput = true; }, options);
        document.addEventListener('click', event => {
            if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            const link = event.composedPath().find(node => node instanceof HTMLAnchorElement);
            if (!link?.href || link.hasAttribute('download') || (link.target && link.target !== '_self')) return;
            const destination = new URL(link.href);
            // Full document navigation is protected by beforeunload. Only prompt
            // here for same-origin enhanced links, before Blazor handles the click.
            if (destination.origin !== location.origin || link.closest('[data-enhance-nav="false"]')) return;
            if (destination.pathname === location.pathname && destination.search === location.search && destination.hash) return;
            if (!confirmDiscard()) {
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        }, options);
        window.addEventListener('beforeunload', event => {
            if (this.isDirty) {
                event.preventDefault();
                event.returnValue = '';
            }
        }, { signal: this.#abort.signal });
        // Enhanced SSR may remove the element before the server can dispose it.
        this.#observer = new MutationObserver(() => {
            if (!editor.isConnected || !editor.hasAttribute('data-notebook-editor')) disposeGuard(editor);
        });
        this.#observer.observe(document.body, { childList: true, subtree: true });
        this.#observer.observe(editor, { attributes: true, attributeFilter: ['data-notebook-editor'] });
    }

    get isDirty() {
        return this.editor.isConnected && (this.#dirty || this.#pendingInput);
    }

    update(dirty, savedRevision) {
        if (dirty || savedRevision !== this.#savedRevision) this.#pendingInput = false;
        this.#dirty = dirty;
        this.#savedRevision = savedRevision;
    }

    dispose() {
        this.#abort.abort();
        this.#observer.disconnect();
    }
}
