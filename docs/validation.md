# Recipe notebook verification

The approved [UI/UX implementation](workflow-qa.md#uiux-implementation-follow-up--october-4-2026)
addresses the four usability findings and applies the notebook design to editing,
tasting, history, comparison, photos, and the library. The original review of
`21f20bb` remains in the workflow log as the baseline.

## Local account email — October 4, 2026

The pre-change unit/component baseline passed **262 unit** and **304 component**
tests, with zero failures or skips. The email integration adds 27 unit cases and a
real browser confirmation/recovery/change-email workflow. Two component cases for
the removed development confirmation bypass were retired; the remaining privacy
test verifies that the confirmation page never generates or exposes a token.

Final email validation includes **289 unit**, **302 component**, **76 PostgreSQL**,
**16 Chromium**, and **1 Aspire startup** cases passing: **684 total**, zero
failures/skips, across separate project runs. Browser tests now
obtain their registration confirmation links from a recipient-filtered Mailpit
inbox and verify HTML/plain-text links. The new account workflow proves that login
is blocked before confirmation, recovery changes the password and rejects the old
one, and a newly confirmed email becomes the sign-in address. The AppHost test
also checks Mailpit readiness. Fixtures disposed their resources; `aspire ps`
reported no remaining run.

```powershell
dotnet build IngaCookBook.slnx
dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj --no-build
dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --no-build
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build
dotnet test --project tests/IngaCookBook.AspireIntegrationTests/IngaCookBook.AspireIntegrationTests.csproj --no-build
dotnet test --project tests/IngaCookBook.IntegrationTests/IngaCookBook.IntegrationTests.csproj
dotnet format IngaCookBook.slnx --severity warn
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
```

Provider/configuration tests cover Development-only Mailpit, explicit Azure
selection, invalid options without secret disclosure, safe link/code encoding,
request/shutdown/timeout cancellation, and Azure SDK payload/completion/error
handling. A malformed Azure connection string initially exposed an uncaught SDK
validation exception; validation now returns the intended safe options error.
The new browser scenario initially found both shell navigation copies during
startup; waiting for the destination heading fixed its synchronization.

The local inbox uses real SMTP. Azure SDK calls are substituted in unit tests:
Azure resource creation, credentials, sending limits, DNS authentication and
actual external inbox delivery were **not** verified or invoked. The optional
live check and configuration are documented in [account email setup](../README.md#account-email).

## Suite reliability and coverage — October 4, 2026

The unchanged full suite reproduced **10 browser startup failures**: **582 of
592 tests passed, 10 failed, 0 skipped**. The browser cases were starting separate
AppHosts concurrently, alongside the PostgreSQL and Aspire suites. Resource logs
showed unhealthy container runtimes, migration failures and AppHost startup
timeouts before the affected workflows reached a browser. This demonstrates
startup contention in this test arrangement, not a diagnosed Docker engine defect.

Browser tests now share one assembly-owned disposable AppHost. Tests still run
concurrently with independent browser contexts, accounts and workspaces. The
`all` / `conservative` / `1x` settings remain unchanged; development volumes are
still removed and resource teardown remains owned by the fixtures. Startup
failure diagnostics capture bounded resource state/logs before disposal. Two
full-suite runs then passed **592/592**, including one coverage baseline run;
neither needed the earlier method-at-a-time workaround.

Coverage expansion exposed and fixed three application defects:

- Preparation step reorder controls were enabled before the unsaved-input guard
  finished initializing. They now use the same readiness lock as the editor's
  other editing controls. The pending-initialization component regression failed
  before the fix and passed afterward.
- Anonymous private-photo API requests redirected to login, producing login HTML
  when redirects were followed. The notebook API group now disables cookie
  redirects, returning `401`; ordinary account pages still redirect. A real
  uploaded-photo HTTP regression failed before the fix and passed afterward.
- A full collected run caught a fast SSR link click before history indexing
  initialized. Back then reached an unindexed entry without prompting and could
  lose unsaved input. Holding the actual UI initializer request reproduced this
  in both Auto renderers (**2 failed**). The application boot module now indexes
  history before `Blazor.start()`; both delayed-initialization cases passed
  afterward (**2 passed, 0 failed, 0 skipped**), including Back/Forward, reload
  dismissal, retained inputs and unchanged document time origin.

The startup ordering was checked against the pinned
[ASP.NET Core boot implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Components/Web.JS/src/Boot.Web.ts):
enhanced navigation is attached before asynchronous initializers finish. API
cookie behavior follows the documented
[`DisableCookieRedirect` endpoint convention](https://learn.microsoft.com/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0).

The new tests assert observable behavior, not only execution or non-null results:

| Requirement | Named evidence |
| --- | --- |
| Preserve HTTP outcomes, request payloads and uploaded bytes | `HttpNotebookResponseTests`, including `MissingConfirmationDoesNotReportASuccessfulSave` and `UploadSendsOriginalPhotoBytesAndMetadataToTheSelectedVersion`. |
| Reject malformed recipes/tastings without changing records or consuming revisions | `NotebookValidationTests`, `RejectedFormulationAndPromotionInputsCannotMutatePersistedContent`, `InvalidTastingsDoNotConsumeTheRevisionOrAppendAnEvaluation`, `IncompleteDraftCannotBecomeABatchOrStandard`. |
| Keep linked formulations authoritative and stable after source edits | `LinkedIngredientsRejectUnpreservedSourcesAndKeepServerSnapshotsAcrossEdits`. |
| Select the right library cards, covers, continuation and historical comparisons | `NotebookPageTests` checks exact result sets, targets, parent versions and scores, including blank versus zero. |
| Respect discard choices, preserve rejected inputs and save actual step order | `NotebookFormSaveTests.Variations.cs`, including `CancelingATastingCorrectionHonorsTheDiscardChoice` and `PreparationReorderingWaitsUntilUnsavedChangesProtectionIsReady`. |
| Keep cleanup alive on transient failures while surfacing unexpected failures | `PhotoCleanupWorkerTests`, using the real worker and processor with controlled dependency failures. |
| Resolve created API resources and enforce account boundaries | `ApiCreatedLocationsResolveAndOtherAccountsCannotReadTheNotebook`: recipe/version/batch/evaluation/photo owner reads, other-account `404`, anonymous `401`, and account-page `302`. |
| Protect input even when the first SSR click races initialization | `EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates` under Server and WebAssembly with a deliberately delayed initializer request. |

Final full-suite validation passed **658 tests, 0 failed, 0 skipped**, including
**66 new cases**. This is one normal all-projects run, with coverage enabled:

| Suite | Before | Final passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Unit | 225 | 262 | 0 | 0 |
| Component | 282 | 304 | 0 | 0 |
| PostgreSQL integration | 70 | 76 | 0 | 0 |
| Aspire integration | 1 | 1 | 0 | 0 |
| Playwright | 14 | 15 | 0 | 0 |

Coverage uses the existing Microsoft collector and merges the five top-level
Cobertura reports with `dotnet-coverage` 18.11.2. No collector exclusions or
analyzer settings were weakened. Application totals exclude only the
`IngaCookBook.Testing` support library. The handwritten view additionally excludes
generated `obj` sources and EF migrations, de-duplicates source filename/line
pairs, and **includes startup code**. Raw branch totals are the collector's
reported instrumented conditions, including generated code.

| Coverage scope | Before | After |
| --- | ---: | ---: |
| Handwritten .NET source lines, including mapped Razor and startup | 4,091 / 4,333 (**94.41%**) | 4,225 / 4,333 (**97.51%**) |
| All instrumented application lines, including generated code and migrations | 11,036 / 11,871 (**92.97%**) | 11,195 / 11,871 (**94.31%**) |
| All instrumented application branches | 2,453 / 2,828 (**86.74%**) | 2,600 / 2,884 (**90.15%**) |

The collector follows the test-owned server/AppHost processes, including SSR and
interactive Server execution. It does **not** measure JavaScript or .NET executing
inside browser WebAssembly. Browser behavior in both renderers is checked by
Playwright; the eight WebAssembly startup lines remain uncovered in the .NET
report. These percentages are not whole-product coverage or a mutation score.

The **108** remaining handwritten lines include production-only middleware,
some API alternatives normally reached through direct Server services, optional
correction/photo/nested-recipe display paths, circuit/stream/compensation failure
handling, and defensive Identity guards. Meaningful future additions include
direct API workspace/promotion and oversized-upload cases, and browser editing of
linked ingredients and historical correction displays. Real provider callbacks,
device passkeys, deployment/restart behavior, and Azure outages remain environment
validation opportunities. No private-state manipulation or deliberately invalid
framework state was added just to execute defensive lines.

Commands run from the repository root included:

```powershell
# Reproduce the unchanged full-suite failures.
dotnet test --solution IngaCookBook.slnx --report-trx --results-directory TestResults/suite-reliability-before
# After fixing shared infrastructure, establish the collected baseline.
dotnet test --solution IngaCookBook.slnx --no-build --coverage --coverage-output-format cobertura --report-trx --results-directory TestResults/coverage-baseline-full
# Run the deterministic navigation regression before and after its fix.
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --filter-method '*EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates' --report-trx --results-directory TestResults/navigation-startup-after
# Final validation, after building the exact test/application sources.
dotnet format IngaCookBook.slnx --severity warn
dotnet build IngaCookBook.slnx
dotnet test --solution IngaCookBook.slnx --no-build --coverage --coverage-output-format cobertura --report-trx --results-directory TestResults/coverage-verified
dnx dotnet-coverage -y -- merge 'TestResults/coverage-verified/*.cobertura.xml' -o TestResults/coverage-verified-merged.cobertura.xml -f cobertura
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
```

The build completed with **0 warnings and 0 errors**. Formatting fixes were
reviewed and final verification was clean. Ignored `TestResults` contains the
before/after TRX and Cobertura reports, merged reports, `coverage-comparison.json`
with the per-file uncovered line inventory, and command logs. Browser screenshots
and traces remain under the test project's output `TestResults` directory. The
assertion review checked exact outcomes, saved payloads, denied writes, retained
inputs, selection/navigation and failure handling; three regressions were also
observed failing against the unfixed application and passing with their fixes.

## Cancellation validation — October 4, 2026

Before implementation, `dotnet test --solution IngaCookBook.slnx --report-trx
--results-directory TestResults/cancellation-before` executed **554** tests:
**544 passed, 10 failed, 0 skipped**. Unit (**206**), component (**268**),
PostgreSQL integration (**65**) and Aspire startup (**1**) all passed. Browser
coverage passed **4 of 14**; the other ten timed out in Aspire startup before
reaching their workflows. This was recorded before the cancellation changes.
Subsequent browser verification runs each existing method's cases together,
one method at a time, without changing the checked-in parallelism settings.

The regression coverage includes:

| Requirement | Named test evidence |
| --- | --- |
| Required token contracts without a custom analyzer | `NotebookContractRequiresAnExplicitFinalCancellationToken`; the solution build enforces CA2016, CA1068, MA0040, MA0032, MA0079 and MA0080 at their documented scopes. |
| Disposal cancels work and preserves cleanup | `DisposingEditorCancelsSaveAndCleansGuardWithoutReloadingLateSuccess`, `DisposalIsIdempotentAndCancelsBeforeCleanup`. |
| Reused pages do not publish stale results, errors or busy state | `ReusedEditorCancelsOldLoadAndIgnoresItsLateResultOrFailure`, `OldCompletionCannotUnlockOrPublishOverAStillPendingLoad`, `FailedEditorDependencyLoadDoesNotExposeAnotherRecipesEditableContent`. |
| SSR request lifetime is separate from interactive lifetime | `RequestAbortCancelsOnlyStaticRendering` (Static, Server and WebAssembly branch cases). |
| Deadlines preserve inputs and describe uncertain writes accurately | `OperationDeadlineCancelsWorkAndReportsTheAppropriateRecovery`, `TimedOutSaveRetainsEditedInputsAndExplainsUncertainOutcome`, `JournalRetainsInputsWhenConfirmedSaveCannotRefresh` (batch and tasting). |
| HTTP forwarding, antiforgery and stream cancellation | `EveryNotebookOperationAbortsItsHttpRequest` (15 operations), `CancelingAntiforgeryLookupPreventsTheWrite`, `CancellationReachesStreamingResponseAndUploadBodies`. |
| A real request abort reaches the backend | `AbortingRealHttpRequestCancelsNotebookEndpointAndEfQuery`, using loopback Kestrel, the production endpoints/service, and an EF command interceptor. |
| Interrupted writes/uploads preserve data and compensate safely | `AlreadyCanceledOperationsDoNotWriteRecipesOrStartUploads`, `CanceledUploadQueuesOnlyUnconfirmedBlobUsingAnIndependentToken`, `CancellationAfterBlobSuccessFinishesMetadataOrCompensation` (success and conflict). |

The HTTP-to-EF test observes the cancellation token at EF's command boundary;
it does not measure PostgreSQL's wire-level cancel latency. Blob interruption
tests use the existing controlled photo store with real PostgreSQL metadata;
the Aspire/browser suites exercise actual Azurite success and cleanup paths.
These checks do not claim that cancellation rolls back a completed write,
immediately disposes a disconnected Server circuit, or guarantees cleanup
during a simultaneous process/database outage.

After implementation, **592 tests passed, 0 failed, 0 skipped**, including
**38 new regression cases**:

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Unit | 225 | 0 | 0 |
| Component | 282 | 0 | 0 |
| PostgreSQL integration | 70 | 0 | 0 |
| Aspire integration | 1 | 0 | 0 |
| Browser (seven method groups, two cases each) | 14 | 0 | 0 |

The final journal adjustment also passed the complete unit/component suites and
the affected workflow, draft-deletion and navigation/journal browser groups again.
The passing browser total comes from grouped runs, not a successful rerun of the
initial all-projects-at-once command. No parallel settings or startup timeouts were
relaxed. TRX reports are under ignored `TestResults/cancellation-after` and
`TestResults/cancellation-browser`; command logs are `TestResults/cancellation-*.log`.

Commands run from the repository root included:

```powershell
dotnet build IngaCookBook.slnx
dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj --no-build --report-trx --results-directory TestResults/cancellation-after
dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --no-build --report-trx --results-directory TestResults/cancellation-after
dotnet test --project tests/IngaCookBook.IntegrationTests/IngaCookBook.IntegrationTests.csproj --no-build --report-trx --results-directory TestResults/cancellation-after
dotnet test --project tests/IngaCookBook.AspireIntegrationTests/IngaCookBook.AspireIntegrationTests.csproj --no-build --report-trx --results-directory TestResults/cancellation-after
# Repeated for each of the seven existing browser test methods.
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --no-build --filter-method '*CookCanRecordEvaluateCompareAndPrintAnExperiment' --report-trx --results-directory TestResults/cancellation-browser/CookCanRecordEvaluateCompareAndPrintAnExperiment
pwsh ./scripts/Test-RazorCodeBehind.ps1
dotnet format IngaCookBook.slnx --severity warn
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
```

The solution build passed with **0 warnings and 0 errors**. All **17** Razor policy
checks passed. Formatting changes were reviewed, and final verification was clean.
The test-quality review checked cancellation, stale completion, input retention,
request boundaries and photo compensation against their observable assertions;
it is not a line-coverage percentage or an empirical mutation score.

## Basic PWA validation — October 4, 2026

The online-only installation and update work uses the selected Kitchen Notebook
artwork. The focused browser command documented in
[tests/README.md](../tests/README.md#installation-and-update-checks) passed **2**
cases (desktop/Server and mobile-width/WebAssembly), with **0 failed, 0 skipped**.
The scenarios cover served manifest/icon dimensions, anonymous no-store version
checks, theme metadata, installation prompt dismissal and keyboard acceptance,
enhanced navigation, safe refresh cancellation, Later dismissal, saved-state
refresh, and the rejected-session review/copy flow. No browser page errors were
reported. Screenshots and traces are in ignored `installation-*` test-output
directories; the successful runner summary is `TestResults/pwa-browser.log`.

`dotnet build IngaCookBook.slnx` passed with **0 warnings, 0 errors**. The complete
unit and component commands (`dotnet test --project` for their respective
projects, with `--no-build` after the final solution build) passed **206** and
**268** tests respectively, both with **0 failed, 0 skipped**.

`dotnet publish src/IngaCookBook/IngaCookBook.csproj -c Release -o TestResults/pwa-publish`
succeeded. Artifact inspection confirmed the manifest, favicon, Apple/regular/
maskable icons, installation/update modules, and the SDK static-asset manifest
used for release identification are present. The publish log is
`TestResults/pwa-publish.log`. The local publish was not deployed or run.

Installation permissions and newer release responses were simulated; native OS
installation, real iOS/Android devices, multiple installed windows, and an actual
deployment/restart remain rollout checks. The browser tests exercise the real
rejected-session UI event without terminating the backend. No service worker,
offline recipe storage, or background synchronization was added.

The recovery review follow-up also passed both browser cases (**2 passed,
0 failed, 0 skipped**). With no new release available, **Review my inputs** and
Escape leave a persistent refresh notice. Canceling its keyboard-triggered
refresh retains the notes and document; confirming refresh loads saved data
and clears the notice. Desktop/mobile `review-inputs-with-refresh.png` screenshots
were inspected in the new `installation-*` output directories. The solution
build again reported **0 warnings, 0 errors**; the unit and component runs again
passed **206** and **268** tests, each with **0 failed, 0 skipped**. This follow-up
uses the simulated rejected-session event, not an actual backend restart.

## Section-link validation

`NotebookWorkflowTests.NotebookLinksReachTheirSectionsAndKeepUnsavedInputs` passed
both cases: desktop (1280 px, Server) and mobile (390 px, WebAssembly).

| Requirement | Browser evidence in each case |
| --- | --- |
| Add photos reaches the current version's uploader. | Clicked the link, checked its URL fragment and visible section, retained document identity, and uploaded a PNG. |
| Experiment-plan navigation retains unfinished work. | Clicked from an editor with an idea query parameter; checked the query, visible target, unchanged notes/quantity, and document identity. |
| Keyboard skip stays on the current page. | Activated with Enter in the editor, version, photo settings, and static account pages; checked main-region focus, unchanged URL, and document identity. |
| Cover/caption navigation has a useful destination. | Followed the link before and after upload, checking the open section, empty-state explanation, and visible caption input. |
| Other primary destinations resolve. | Followed kitchen, print, comparison, history/branch, tasting, workspace/library, and account/profile/email/password links. No page errors or HTTP 5xx responses were observed during these interactions. |

```powershell
dotnet build IngaCookBook.slnx
dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --filter-method '*NotebookLinksReachTheirSectionsAndKeepUnsavedInputs'
dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj --no-build
dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --no-build
dotnet format IngaCookBook.slnx --severity warn
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
```

Results: browser **Passed: 2, Failed: 0, Skipped: 0**; unit **Passed: 206,
Failed: 0, Skipped: 0**; component **Passed: 268, Failed: 0, Skipped: 0**.
The solution build passed with zero warnings/errors. Logs are under ignored
`TestResults/link-fixes-*.log`; formatting was reviewed and verification was clean.
Browser screenshots/traces use the test output's
`notebook-links-*` directories. The first browser run reached the final account
page but failed both cases because the test expected “Current password” instead
of “Old password”; correcting the locator produced the clean run above.
The first sandboxed build could not read the user NuGet configuration; the
authorized build with normal cache/configuration access succeeded.

This is focused Chromium coverage, not an exhaustive crawl of all account states,
external URLs, or other browser engines. Isolated fixtures own and dispose their
Aspire resources and test data. Server cases intentionally hold and then abort
WebAssembly downloads to select the renderer, so teardown traces may show aborted
asset downloads; these are not link failures.

## UI/UX implementation validation — October 4, 2026

### Code-review follow-up

The two review findings are addressed:

| Requirement | Evidence |
| --- | --- |
| Kitchen view retains saved notes, line breaks, and literal text without displaying an empty section. | `NotebookComponentsTests.KitchenSheetRendersRecipeNotesAsPlainTextWithLineBreaks` and `KitchenSheetOmitsEmptyRecipeNotes` (empty and whitespace cases). The focused `dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --filter-method '*KitchenSheet*'` run passed all three cases, zero failed/skipped. |
| Deletion from version details cannot target the outgoing editor. | `NotebookWorkflowTests.CookCanDiscardDraftsWithoutLosingPreservedHistory` now waits for `.version-actions` and scopes both deletion controls to it, retaining the remaining-history assertions. |

`dotnet build IngaCookBook.slnx` passed with zero warnings/errors. The full unit
suite passed 206 tests and the component suite passed 268, both zero failed/skipped,
using their documented `dotnet test --project ... --no-build` commands. Current
follow-up logs are in ignored `TestResults/uiux-review-fixes/`.
`dotnet format IngaCookBook.slnx --severity warn` completed with its fixes reviewed;
the final `dotnet format IngaCookBook.slnx --severity warn --verify-no-changes`
passed without changes. No validation AppHost remained running after the tests.

`dotnet test --project tests/IngaCookBook.PlaywrightTests/IngaCookBook.PlaywrightTests.csproj --filter-method '*CookCanDiscardDraftsWithoutLosingPreservedHistory*'`
passed both Server/desktop and WebAssembly/phone scenarios: two passed, zero
failed/skipped. An initial attempt failed both cases because the generic reveal
helper's relative locator could not open the newly scoped action menu; the test
now opens that menu explicitly before using its scoped delete controls. The
failed log is retained alongside the successful rerun. The new component-test
setup was also corrected to supply renderer information and explicit string
comparison before its passing runs.

Documentation review confirmed that setup, build rules, and agent conventions
remain accurate. The existing product guide, workflow log, test scope, and this
validation record were updated for the two fixes.

### Original implementation checks

- `dotnet build IngaCookBook.slnx --no-restore`: passed, zero warnings/errors.
- `dotnet format IngaCookBook.slnx --severity warn` completed; its fixes were
  reviewed. The final `dotnet format IngaCookBook.slnx --severity warn --verify-no-changes`
  passed without changes.
- `dotnet test --project tests/IngaCookBook.UnitTests/IngaCookBook.UnitTests.csproj --no-build`:
  206 passed, zero failed/skipped.
- `dotnet test --project tests/IngaCookBook.ComponentTests/IngaCookBook.ComponentTests.csproj --no-build`:
  265 passed, zero failed/skipped. New cases cover required-name rejection, additive
  presets, retained forms when switching journal tasks, tasting receipts, and
  editable next-idea drafts including saving a revised question.
- `dotnet test --solution IngaCookBook.slnx --no-build`: 543 passed, four failed,
  zero skipped out of 547. Unit, component, PostgreSQL integration, and Aspire
  integration projects passed. Three browser cases timed out during Aspire
  startup; one browser assertion still expected history to be expanded after a
  reload. The assertion now opens the recorded-tastings view before checking it.
- Repeating the complete Playwright project: nine passed, one failed during
  Aspire startup, zero skipped. The focused
  `--filter-method '*EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates*'`
  rerun then passed both Server and WebAssembly cases, zero failed/skipped.
  All 547 distinct cases therefore passed across these runs; this does **not**
  describe a single clean full-solution run.
- Cover/caption integration tests verify persistence, stale-write rejection,
  workspace ownership, foreign photo rejection, and fallback after deleting the
  draft that supplied the cover. The `RecipeCoverPhoto` migration was generated
  through Aspire's migration resource and applied normally, retaining test data.
- A subsequent desktop/phone workflow rerun exposed an immediate assertion after
  kitchen Reset. The test now waits for cleared checkmarks before asserting;
  both cases passed on rerun. This timing correction does not relax the expected
  result. The 320 px manual pass also found a dropdown minimum-width overflow,
  now covered by the editor's responsive browser check.
- After the responsive fix, the focused
  `--filter-method '*CookCanRecordEvaluateCompareAndPrintAnExperiment*'` run passed
  both desktop and phone cases (two passed, zero failed/skipped). The 206 unit
  and 265 component cases also passed again with zero failures/skips.
- The final kitchen-only CSS adjustment was rebuilt and checked manually:
  checkboxes measure 44 × 44 px, clicking their labels marks completion, completed
  labels have a strike-through, and Reset clears the checkmarks. No document
  overflow was observed at 320 px.

Real-browser checks include required-field focus, Tab/Shift+Tab clearance above
the sticky save bar, discrete score selection and arrow keys, retained precision,
kitchen checkmarks/reset, completion summaries, print focus styling, themes,
draft deletion, and correction/navigation guards under both Auto renderers.
Earlier runs exposed selector assumptions and a manual-preview/test-host lifecycle
collision; they were not counted as passes. Infrastructure failures remain visible
in the ignored logs under `TestResults/uiux-implementation/`.

Manual exploration additionally verified cover selection/caption edits and the
saved-tasting → new variation → revised question → save → reload workflow. It
observed no uncaught browser exceptions or application warning/error entries in
that run. Final viewport checks found no document-width overflow at 320 px in
the editor, library, history, comparison, and tasting views; comparison was also
checked at 768 px. The 390 px scoring controls measured approximately 54 × 44 px,
with five choices per row. Chromium's accessibility tree exposed named radio
groups and numbered radio options. System-theme changes were exercised in both
directions. These checks do not substitute for assistive-technology testing.

On the same four-ingredient review fixture at 390 px, the first ingredient now
starts about 543 px down, versus about 1,440 px in the original review. This is a
layout measurement, not a measured improvement in task completion time. Final
screenshots are under `TestResults/uiux-implementation/`: `final-library-desktop.png`,
`final-library-phone-dark.png`, `final-editor-desktop.png`, `final-editor-320.png`,
`final-tasting-desktop.png`, `final-tasting-phone-dark.png`,
`final-comparison-desktop.png`, `final-history-dark.png`, and
`final-kitchen-desktop.png`.

Screenshot fixtures include reference artwork rather than production
food photography. This is not a screen-reader audit, physical-device/virtual-
keyboard test, or proof of faster human task completion.

The final manual pass recorded no uncaught browser exceptions or application
warning/error log entries. The owned Aspire AppHost and browser were stopped.
Documentation review covered root instructions, setup/build/test guidance, and
the existing feature/QA documents. Setup and durable agent rules remain accurate;
feature behavior, browser-test scope, and evidence were updated in their existing
locations.

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
