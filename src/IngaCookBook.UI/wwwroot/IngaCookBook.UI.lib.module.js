import { initializeNavigation } from './Layout/MainLayout.razor.js';
import { initializeAppearance } from './Layout/Appearance.razor.js';
import { initializeEditorNavigation } from './Features/Notebook/Pages/VersionEditor.razor.js';
import { initializeLocalDates } from './Features/Notebook/Components/LocalDate.razor.js';
import { listenForInstallation, initializeInstallation } from './Layout/InstallApp.razor.js';
import { initializeUpdates } from './Layout/AppUpdate.razor.js';

export function beforeWebStart() {
    listenForInstallation();
}

export function afterWebStarted(blazor) {
    initializeEditorNavigation();
    initializeLocalDates();
    initializeNavigation(blazor);
    initializeAppearance(blazor);
    initializeInstallation(blazor);
    initializeUpdates(blazor);
}
