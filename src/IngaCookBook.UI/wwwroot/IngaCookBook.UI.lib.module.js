import { initializeNavigation } from './Layout/MainLayout.razor.js';
import { initializeAppearance } from './Layout/Appearance.razor.js';
import { initializeLocalDates } from './Features/Notebook/Components/LocalDate.razor.js';
import { listenForInstallation, initializeInstallation } from './Layout/InstallApp.razor.js';
import { initializeUpdates } from './Layout/AppUpdate.razor.js';

export function beforeWebStart() {
    listenForInstallation();
}

export function afterWebStarted(blazor) {
    initializeLocalDates();
    initializeNavigation(blazor);
    initializeAppearance(blazor);
    initializeInstallation(blazor);
    initializeUpdates(blazor);
}
