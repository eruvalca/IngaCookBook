# Recipe notebook verification

Validation uses the repository's .NET 10 MTP/xUnit/Shouldly stack. PostgreSQL
tests own disposable Testcontainers. Aspire and Chromium tests run isolated
copies of the actual AppHost, remove development volume mounts, and dispose
their resources. No test targets a development database.

## Requirement coverage

| Requirement | Evidence |
| --- | --- |
| Exact recipe versions, repeated batches/tastings, standard selection, corrections, independent promotion | `NotebookPersistenceTests.RecipeLifecyclePreservesBatchesCorrectionsStandardsAndIndependentPromotion` checks persisted quantities, scores, histories, provenance, and independent metric identities. |
| Focused experiments with warnings and an explanation for related changes | `NotebookPersistenceTests.VariationsRequireExplanationAndStaleEditsCannotOverwriteSavedWork`; `NotebookWorkflowTests.CookCanRecordEvaluateCompareAndPrintAnExperiment` exercises the warning and rejected/accepted saves through the UI. |
| Workspace isolation, including photos | `NotebookPersistenceTests.WorkspaceIsolationProtectsRecipesWritesAndPhotos` verifies denial of another owner's reads, edits, uploads, and downloads. |
| Changes to recipe-wide metrics | `NotebookPersistenceTests.CriteriaChangesLeaveNewScoresBlankAndDeleteRemovedScores` verifies missing historical scores and removal of scores/notes while general observations survive. |
| Pinned recipes inside recipes | `NotebookPersistenceTests.LinkedRecipesKeepSavedContentWhenSourceIsCorrected`; `NotebookComponentsTests.RecipeSheetRendersPinnedSubrecipeAndPreservesTextWithoutInterpretingMarkup`. |
| Compatible-unit costs and incomplete estimates | `NotebookRulesTests.CompatiblePurchaseUnitsProduceProportionalCost`, `CostNeverGuessesAcrossMeasurementFamilies`, and `MissingPricesRemainUnknownAndNestedRecipesUseTheirSavedYield` assert exact monetary results and unknown costs. |
| Comparison and chronological tasting selection | `NotebookRulesTests.ComparisonDetectsIngredientChangesAndReorderedPreparationWithoutCountingPrices`, `LatestEvaluationIsExplicitAndSameDayTastingsChooseLastRecorded`, and the browser workflow verify changes, order, tie-breaking, and displayed results. |
| Concurrency and SQL constraints | `NotebookPersistenceTests.ConcurrentEditsAllowOnlyOneWriterAndRetainItsContent` races independent contexts; `DatabaseConstraintsRejectInvalidAmountsAndScores` deliberately bypasses service validation and verifies PostgreSQL constraint rejection and retained data. |
| Inputs cannot change during a save/reload | `NotebookFormSaveTests` holds writes and reloads open, asserts Fluent/native inputs are disabled, and checks re-enabling and retained values after success/rejection. The Chromium workflow holds a WebAssembly save request open and checks the actual native controls at both widths. |
| Unsaved editor protection across enhanced SSR navigation | `NotebookWorkflowTests.EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates` cancels version/sidebar links, reload, and Back/Forward, asserts retained inputs and document identity, confirms discarding, and verifies that saved edits can leave without a prompt under Server and WebAssembly. |
| Browser-local dates and retained date selections | The same Chromium regression fixes the browser clock near midnight UTC in Chicago, records a batch/tasting, and checks the persisted local date under both renderers. `NotebookFormSaveTests.JournalWaitsForBrowserDateAndPreservesSelectedDatesAfterSaving` verifies blank disabled dates until browser initialization and retained manual dates after a save/reload. |
| Precise nested recipe costs | `NotebookRulesTests.NestedRecipeCostsRoundOnlyForDisplay` pins a $4.125 nested cost, $16.50 at four times the yield, and $4.125 when reused at another nesting level. |
| Account deletion and durable photo cleanup | `PhotoCleanupTests` checks real Identity deletion, cleanup retries and due times, another owner's retained data, concurrency rejection, and rollback when the queue insert fails. PostgreSQL retries are enabled as in the app. The Aspire startup test waits for the actual worker to remove a queued blob and its snapshot while retaining another prefix. |
| Recoverable Azure failures | `PhotoStorageFailureTests` injects ordinary/aggregate Azure failures and failed compensation, then checks rejected status, retained earlier photos, unchanged metadata, exact cleanup keys, and eventual cleanup. Unexpected exceptions, cancellation, and mixed aggregates remain errors rather than successful saves. The upload component checks partial-success messaging and stops later files. |
| Photo signatures, limits, and partial failure | `NotebookRulesTests.PhotoSignatureRecognizesOnlyTheSupportedHeaders`, `PhotoSignatureRejectsTextAndActiveContent`, `NotebookPersistenceTests.InvalidPhotosNeverWriteAndConcurrentPhotoConflictRemovesOnlyItsUpload`, and `NotebookComponentsTests.InterruptedPhotoSequenceReportsEarlierSavesAndStopsLaterUploads`. |
| Actual Azure-compatible upload/download, desktop/mobile usability, print view, light/dark/system, branch view, Auto's browser renderer | `NotebookWorkflowTests.CookCanRecordEvaluateCompareAndPrintAnExperiment` runs at 1280 and 390 pixels, decodes a stored photo, verifies page width, print visibility, theme changes, branch labels, and a saved/reloaded WebAssembly edit. |
| SSR navigation and account compatibility | `NavigationTests.FluentNavigationPreservesDocumentAndCounterWorks` at both widths, the registration/workspace browser flow, and the existing account component/unit suites. |
| Authenticated minimal APIs, antiforgery, migrations, storage/database readiness, OpenAPI | `ApplicationStartupTests.AppHostCompletesMigrationsAndServesHealthyApplication` and the notebook browser workflow exercise real startup and HTTP behavior, including anonymous 401 and missing-antiforgery 400 responses. |

The test-gap and assertion-quality review traced observable outcomes against
specific assertions. It led to additional checks for concurrent edits, database
constraints, photo compensation, interrupted upload messaging, same-date
tastings across batches, save-time input locking, transactional deletion,
cleanup retries, and nested-cost precision. No mutation score or coverage
percentage is claimed.
bUnit verifies rendered behavior; it does not stand in for browser JavaScript,
layout, storage, or PostgreSQL checks.

## Commands

The final local run passed **475 tests**, with **0 failed** and **0 skipped**:

| Suite | Passed |
| --- | ---: |
| Unit | 206 |
| Component | 243 |
| PostgreSQL integration | 17 |
| Aspire integration | 1 |
| Chromium browser | 8 |

The full solution build completed with zero warnings and zero errors. The
formatter's verification pass was clean. The browser workflow also verifies that
a quantity of `100.1256 g` survives a save and appears unchanged in comparison,
and comparison tables fit the viewport without hiding columns behind scrolling.

```powershell
dotnet format IngaCookBook.slnx --severity warn
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
dotnet build IngaCookBook.slnx
dotnet test --solution IngaCookBook.slnx --no-build
```

`--no-build` is used only after a successful matching build. Browser screenshots,
traces, and test logs remain under ignored test-output directories. The existing
Identity files mostly have formatter-required import ordering. Account deletion
now calls the transactional deletion service; its password checks, failed-delete
behavior, sign-out ordering, and redirects remain covered by component tests.

Documentation review updated the product guide, runtime/storage/migration
instructions, and test-layer descriptions. Existing agent/build conventions
remain applicable. Deployment, real Azure credentials, production email, backup
operations, and load testing are outside this local implementation verification.
