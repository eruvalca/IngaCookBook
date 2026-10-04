import { initializeEditorNavigation } from '../_content/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.js';

// Enhanced navigation starts before Blazor finishes loading JS initializers.
// Index the initial history entry first, including when a cook follows an SSR
// link immediately. Late initialization would leave Back destinations unguarded.
initializeEditorNavigation();
Blazor.start();
