# Recipe notebook verification

The separate [exploratory kitchen workflow log](workflow-qa.md) retains the initial
browser observations, reproduction steps, and screenshots, plus the October 3
follow-up fixes and their regression coverage. Its second exploratory pass on
October 3 records fresh workflow evidence, unsaved-form finding B3, and batch
correction improvement I5. Both are addressed in its second findings follow-up,
with the regression evidence below.

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
| Abandoned drafts and recipe removal | `DeletingOnlyDraftRemovesRecipeAndQueuesOnlyItsPhotos` checks database cascades, workspace retention, inaccessible deleted photos, and cleanup restricted to the deleted version. `DeletingVariationRetainsBaselineAndSiblingAndRejectsStaleWrites` checks retained content, provenance, and stale/repeated writes. |
| Preserve history and reject unauthorized deletion | `DeletionProtectsPreservedAndReferencedVersions` covers batches, standards, child variations, and independent promotion. `DeletionRejectsStaleMissingAndOtherWorkspaceRequestsWithoutCleanup` verifies rejection without a cleanup job. |
| Atomic deletion and concurrent preservation | `CleanupQueueFailureRollsBackDraftDeletion` injects check/FK failures and verifies rollback without disguising unexpected errors as conflicts. `PreservationWinningDuringDeletionIsRetainedAsAConflict` and `DeletionWinningDuringPreservationReturnsAConflictWithoutRecreatingTheDraft` exercise both commit orders for batch/standard/variation/promotion, for an only draft and a child draft. `UploadRacingDraftDeletionCannotRecreateTheDraftOrLeaveItsBlob` checks upload compensation. |
| Confirm draft deletion and retain canceled/rejected edits | `DraftDeletionRequiresConfirmationAndNavigatesAfterSuccess`, `RejectedDraftDeletionRetainsEditorInputsAndUnlocksTheForm`, and `PreservedVersionsDoNotOfferDraftDeletion` cover component behavior. `CookCanDiscardDraftsWithoutLosingPreservedHistory` verifies Server/desktop and WebAssembly/mobile cancellation, dirty-editor deletion without a second guard, version-details deletion, empty first-draft recipe removal, and DELETE antiforgery rejection. |
| Changes to recipe-wide metrics | `NotebookPersistenceTests.CriteriaChangesLeaveNewScoresBlankAndDeleteRemovedScores` verifies missing historical scores and removal of scores/notes while general observations survive. |
| Pinned recipes inside recipes | `NotebookPersistenceTests.LinkedRecipesKeepSavedContentWhenSourceIsCorrected`; `NotebookComponentsTests.RecipeSheetRendersPinnedSubrecipeAndPreservesTextWithoutInterpretingMarkup`. |
| Compatible-unit costs and incomplete estimates | `NotebookRulesTests.CompatiblePurchaseUnitsProduceProportionalCost`, `CostNeverGuessesAcrossMeasurementFamilies`, and `MissingPricesRemainUnknownAndNestedRecipesUseTheirSavedYield` assert exact monetary results and unknown costs. |
| Comparison and chronological tasting selection | `NotebookRulesTests.ComparisonDetectsIngredientChangesAndReorderedPreparationWithoutCountingPrices`, `LatestEvaluationIsExplicitAndSameDayTastingsChooseLastRecorded`, and the browser workflow verify changes, order, tie-breaking, and displayed results. |
| Concurrency and SQL constraints | `NotebookPersistenceTests.ConcurrentEditsAllowOnlyOneWriterAndRetainItsContent` races independent contexts; `DatabaseConstraintsRejectInvalidAmountsAndScores` deliberately bypasses service validation and verifies PostgreSQL constraint rejection and retained data. |
| Inputs cannot change during a save/reload | `NotebookFormSaveTests` holds writes and reloads open, asserts Fluent/native inputs are disabled, and checks re-enabling and retained values after success/rejection. The Chromium workflow holds a WebAssembly save request open and checks the actual native controls at both widths. |
| Unsaved editor protection across enhanced SSR navigation | `NotebookWorkflowTests.EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates` cancels version/sidebar links, reload, and Back/Forward, asserts retained inputs and document identity, confirms discarding, and verifies that saved edits can leave without a prompt under Server and WebAssembly. |
| B3: protect recipe creation and settings inputs | `NewRecipeRetainsRejectedInputAndClearsGuardBeforeSuccessfulNavigation` checks rejected input and successful creation navigation; `SettingsMetricEditsRemainGuardedAfterRejectionAndClearAfterSave` checks structural edits, conflicts, and saved criteria. The Server/WebAssembly browser regression checks links, reload, Back/Forward, rejected saves, successful saves, and confirmed discard on these screens. |
| Browser-local dates and retained date selections | The same Chromium regression fixes the browser clock near midnight UTC in Chicago, records a batch/tasting, and checks the persisted local date under both renderers. `NotebookFormSaveTests.JournalWaitsForBrowserDateAndPreservesSelectedDatesAfterSaving` verifies blank disabled dates until browser initialization and retained manual dates after a save/reload. |
| Local timestamp display with static SSR fallback | `NotebookFormSaveTests.HistoryTimestampHasAnExplicitUtcFallbackAndAnUnambiguousInstant` checks UTC fallback text and an offset-bearing `datetime`; the browser regression tests a known UTC boundary in a history timestamp after enhanced navigation. |
| Journal input protection and explicit score validation | The Server/WebAssembly browser regression checks invalid `11` is retained and unsaved tasting/batch notes survive canceled links, reload, Back/Forward, and saves to the other form. `JournalRetainsInvalidScoresAndDoesNotSaveThem` checks 0, 11, fractions, and text; `JournalSavesBlankAndBoundaryScoresWithoutChangingTheirMeaning` checks blank, 1, and 10. |
| Audited tasting corrections | `TastingCorrectionsPreserveHistoryAndOrderAndRemoveDeletedCriteriaFromAudit` verifies dates, notes, scores, prior snapshots, stable identity/order, repeated corrections, concurrency rejection, and criterion removal. `InvalidTastingCorrectionsLeaveOriginalAndAuditUntouched` checks invalid reasons/payloads/scores/dates, missing IDs, and another owner. The component test retains inputs after conflict; the browser saves and reloads a correction under both renderers. |
| I5: audited batch corrections with stable identity and chronology | `BatchCorrectionsRetainIdentityOrderTastingsAndSuccessiveHistory` checks successive corrections, prior values, stable numbers and tastings, the exact tasting-date boundary, and stale revisions. `InvalidBatchCorrectionsLeaveDateNotesTastingsAndHistoryUntouched` covers eight invalid/unauthorized cases. `BatchCorrectionRetainsConflictInputsAndDoesNotCreateAnotherBatch` checks exact request fields and no new batch; the browser regression recovers from a mistaken date while retaining an unfinished tasting, then reloads both records and their audit. |
| Preserve existing batch order during schema upgrade | `BatchPositionMigrationPreservesExistingChronology` seeds batches in the preceding schema in reverse insertion order, applies the generated migration, then corrects a date across the other batch's date and verifies stable order. Only its disposable database is migrated backward. |
| Correcting a preserved recipe without experiment guidance | `PreservedVariationCorrectionNeedsOnlyItsCorrectionReason` persists a yield correction on a previously changed variation; `PreservedVersionShowsItsCorrectionDiffWithoutExperimentWarning` checks only the correction diff appears. Existing draft-variation guidance tests remain. |
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

## Draft deletion — October 3, 2026

The editor and version details now offer **Delete draft** with an explicit
confirmation. Browser checks cover keeping unsaved edits after cancellation,
deleting a dirty variation without a second discard prompt, removing an empty
first draft and its recipe, deleting from details, keeping the original preserved
version, and rejecting a DELETE without an antiforgery token. The runs use
Server at 1280 × 900 and WebAssembly at 390 × 900. Both confirmation screenshots
were inspected; controls and text fit, with no horizontal overflow. Local evidence
is under `tests/IngaCookBook.PlaywrightTests/bin/Debug/net10.0/TestResults/draft-deletion-*/`.

PostgreSQL tests verify ownership, stale tabs, recipe/child retention, durable
photo cleanup, upload compensation, and rollback when saving the cleanup job
fails. Deterministic save interceptors commit a competing operation after the
other has read its data. Both write orders cover batches, standards, child
variations, and promotion, for an only draft and an existing child draft.

These race checks initially exposed FK exceptions before EF reached its recipe
revision check (2 of 8 cases in one order, 5 of 8 in the other). Writes now verify
a changed/missing revision after those transaction rollbacks and return a conflict;
unexpected integrity errors with an unchanged revision still propagate. Promotion
preserves its source in the same transaction as its independent copy. The full
PostgreSQL suite subsequently passed 63 cases. No schema change or database reset
was needed.

A repeated browser run caught a test timing issue: the outgoing editor and
incoming details page both contain a Delete draft button. The test now waits for
the destination heading before clicking it, rather than clicking during enhanced
navigation. The original desktop/mobile recipe workflow also passed after the
server changes.

Final project/group runs passed **534 tests**: **206 unit**, **260 component**,
**63 PostgreSQL integration**, **1 Aspire integration**, and **4 targeted Chromium
cases** (two deletion cases and two existing recipe-workflow cases), with **0
failed** and **0 skipped** in those final runs. The other browser groups were not
rerun for this feature. Earlier investigative failures are described above and
are not counted as additional tests.

Commands from the repository root (the full build precedes `--no-build` runs):

```powershell
dotnet format IngaCookBook.slnx --severity warn
dotnet build IngaCookBook.slnx
dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj --no-build
dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --no-build
dotnet test --project tests/IngaCookBook.IntegrationTests/IngaCookBook.IntegrationTests.csproj
dotnet test --project tests/IngaCookBook.AspireIntegrationTests/IngaCookBook.AspireIntegrationTests.csproj --no-build
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*CookCanRecordEvaluateCompareAndPrintAnExperiment'
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --filter-method '*CookCanDiscardDraftsWithoutLosingPreservedHistory'
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
pwsh ./scripts/Test-RazorCodeBehind.ps1
```

The full solution build passed with zero warnings/errors, and formatting
verification was clean. All **17 Razor policy checks** passed after
rerunning with access to the user NuGet configuration; the initial sandboxed
attempt could not read that file.

Documentation review updated the product guide, this validation record, test
scope descriptions, and the authorized pre-deployment data policy in AGENTS.md
and README.md. Build conventions remain accurate. Existing pending navigation
changes were retained. The test fixtures disposed their isolated resources.

## Account menu theme follow-up — October 3, 2026

The account settings `FluentNav` now uses the same surface and hover tokens as
the main navigation. Its omitted parameters previously selected Fluent's
`colorNeutralBackground4` defaults, producing the contrasting gray panel.

Chromium checks at 1440 and 390 pixels confirmed matching menu surfaces in
light/dark/system modes, matching desktop hover colors, visible keyboard focus,
active Email/Password links after navigation, retained appearance after reload,
and no horizontal overflow on the phone layout. Screenshots were inspected and
are local under ignored `TestResults/menu-theme/`. The mobile drawer opened and
closed. A headless-browser WebAuthn `NotSupportedError` was observed during the
password-login session; this run does not establish passkey support.

The subsequent navigation-state check confirmed that the Recipes background in
the earlier previews was a hover effect, not incorrect route selection. The
pinned Fluent styles transition that background over 100 ms; `active` and
`aria-current="page"` stayed on the account link while Recipes was hovered.
Moving the pointer away and waiting for the background to equal the navigation
surface restored the neutral state in both themes. Opening the library selected
Recipes; returning to Profile or Email selected the account link.

The signed-in link now reads **My account**, including for existing users. The
existing `NavMenuTests.AuthenticationStateSelectsGuestLinksOrAccountAndLogoutAsync`
assertion was updated. At 390 pixels, the label occupied one text line in a
40-pixel-high drawer item, with no horizontal overflow. Fresh neutral previews
and state observations are ignored under `TestResults/navigation-state/`.
The navigation follow-up recorded no browser page errors and repeated the build
and unit/component commands below successfully with the same test counts.

The solution build passed with zero warnings/errors. Unit tests passed **206**
and component tests passed **256**, with **0 failed** and **0 skipped** in each.
These are repeated existing tests, not additions to the unique counts below.
Commands from the repository root:

```powershell
dotnet format IngaCookBook.slnx --severity warn
dotnet build IngaCookBook.slnx
dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj --no-build
dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --no-build
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
```

## Brand/layout follow-up — October 3, 2026

The follow-up to `e8d1b31` fixes document scrolling, Fluent dropdown/native style
collisions, header and control alignment, logo-based appearance, and routine
antiforgery/blob-provisioning diagnostics. Manual evidence and the remaining
Aspire watcher limitation are in [workflow QA](workflow-qa.md#brand-and-layout-investigation--october-3-2026).

The subsequent [ContainerExec investigation](workflow-qa.md#controlled-verification-of-the-upstream-fix)
reproduced the 13.6.0 defect and verified Microsoft's 13.6.1 staging fix in isolated
AppHosts, including a late command after the five-minute watch restart. The
application remains on stable 13.6.0; those diagnostic runs do not add to the
automated application-test counts below.

The final project/group runs again passed **506 unique tests**, **0 failed**, and
**0 skipped**: unit 206, component 256, PostgreSQL 35, Aspire 1, Chromium 8.
The two `CookCanRecordEvaluateCompareAndPrintAnExperiment` cases now additionally
assert actual wheel scrolling through a long metric form, field/action bounds,
header height, Fluent dropdown border/padding and selection, equal action
heights, and authenticated antiforgery cache headers. The guard cases still cover
both Server and WebAssembly. Browser groups ran separately to bound resource use.

Commands for this follow-up (from the repository root):

```powershell
dotnet format IngaCookBook.slnx --severity warn
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
dotnet build IngaCookBook.slnx
dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj
dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj
dotnet test --project tests/IngaCookBook.IntegrationTests/IngaCookBook.IntegrationTests.csproj --no-build
dotnet test --project tests/IngaCookBook.AspireIntegrationTests/IngaCookBook.AspireIntegrationTests.csproj --no-build
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --filter-method '*CookCanRecordEvaluateCompareAndPrintAnExperiment'
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates'
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*FluentNavigationPreservesDocumentAndCounterWorks'
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*ArtifactFailuresPreserveOriginalExceptionAndAttemptBothCaptures'
```

The workflow pair passed once, then passed again after explicit row-count waits
were added to make repeated Add/Remove actions wait for rendering. This is a
repeat of two cases, not two additional tests. No test failures were observed in
this follow-up. Documentation review updated setup/style guidance, the feature
theme description, scrolling-test guidance, and the existing QA/validation logs.
AGENTS.md and build rules remained accurate.
The final full solution build passed with zero warnings/errors and formatting
verification was clean. The post-build manual upload/log check found no application
warnings/errors; the separately documented Aspire watch failure still occurs.

## Earlier validation commands

The final October 3 runs after both QA follow-ups passed **506 tests** in total, with **0 failed** and
**0 skipped**, across separate project runs and four browser groups:

| Suite | Passed |
| --- | ---: |
| Unit | 206 |
| Component | 256 |
| PostgreSQL integration | 35 |
| Aspire integration | 1 |
| Chromium browser | 8 |

The full solution build completed with zero warnings and zero errors. The
formatter's verification pass was clean. The browser workflow verifies compact
six-place entry, that `100.1256 g` survives a save and appears unchanged in
comparison, and that comparison tables fit the viewport without hiding columns
behind scrolling.

```powershell
dotnet format IngaCookBook.slnx --severity warn --no-restore
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes --no-restore
dotnet build IngaCookBook.slnx
dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj --no-build
dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --no-build
dotnet test --project tests/IngaCookBook.IntegrationTests/IngaCookBook.IntegrationTests.csproj --no-build
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-restore --filter-method '*EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates'
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*CookCanRecordEvaluateCompareAndPrintAnExperiment'
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*FluentNavigationPreservesDocumentAndCounterWorks'
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*ArtifactFailuresPreserveOriginalExceptionAndAttemptBothCaptures'
dotnet test --project tests/IngaCookBook.AspireIntegrationTests/IngaCookBook.AspireIntegrationTests.csproj --no-build
```

Each browser group executed two cases. The first expanded guard run had two
test failures because enhanced navigation changed the URL before replacing the
page content. Waiting for destination UI fixed the test race; its final run
passed both renderers. A sandboxed command could not read the user NuGet config;
rerunning with the existing tool permissions succeeded.

Before this second follow-up, an earlier concurrent full-solution run reported
481 passed, 7 failed, and 0 skipped, with PostgreSQL readiness and Aspire startup
failures before the affected browser scenarios. The final separated runs above
passed all suites without changing their parallelism settings. They are an
aggregate result, not a clean concurrent full-solution run.

`--no-build` is used only after a successful matching build. Final infrastructure
logs are under ignored `TestResults/round2-fixes/*.log`; the earlier six-finding
logs remain under `TestResults/findings-fixes/final-*.log`. Browser screenshots
and traces remain under ignored test-output directories. Test fixtures disposed
their resources; the migration-generation Aspire run was stopped normally.

Documentation review updated the product guide, exploratory findings follow-up,
validation evidence, and test-layer descriptions. Existing setup, agent, and
build guidance remains accurate and unchanged. Deployment, real Azure
credentials, production email, backup operations, and load testing are outside
this local implementation verification.
