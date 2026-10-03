import { initializeNavigation } from './Layout/MainLayout.razor.js';
import { initializeAppearance } from './Layout/Appearance.razor.js';
import { initializeEditorNavigation } from './Features/Notebook/Pages/VersionEditor.razor.js';

export function afterWebStarted(blazor) {
    initializeEditorNavigation();
    initializeNavigation(blazor);
    initializeAppearance(blazor);
}
