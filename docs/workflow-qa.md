# Exploratory kitchen workflow QA

## Section-link follow-up

- **Add photos** used `#photos`, which resolved against the app's root base URL.
  For signed-in users the root redirected to the library. The link now includes
  the current version route and reaches its uploader.
- **Experiment plan** had the same problem. Its URL now retains the editor path
  and query string, so moving to the plan preserves unsaved inputs and idea context.
- **Skip to content** now focuses the current main region without leaving the
  page, including after interactive routing; SSR retains a page-qualified fallback.
- **Choose a cover or edit captions** now opens the settings photo section. That
  destination also exists before the first upload, with an explanatory empty state.

Browser regression evidence and the checked destination links are recorded in
[validation.md](validation.md#section-link-validation).

## UI/UX implementation follow-up — October 4, 2026

The approved design is implemented with Fluent UI and the existing static SSR /
InteractiveAuto boundaries. The original review below remains the baseline.

- UX1: the editor measures the save bar, reserves document scroll clearance,
  and brings focused fields above it, including when actions wrap.
- UX2: an empty recipe name gets an in-place, announced error and field focus.
  Server validation remains intact.
- UX3: library totals no longer precede the work; card counts use singular and
  plural labels.
- UX4: print media removes the heading focus outline; screen focus stays visible.
- Ingredients lead the editor. The experiment coach and optional metadata are
  secondary, with multi-change guidance expanded when needed.
- Batch recording, tasting, and recorded history have separate views. Numbered
  score choices support notes, clearing, numeric entry, and unassessed values.
  Switching tasks retains unfinished forms.
- Save receipts show the recorded batch/tasting. A next idea starts an editable
  variation without changing the original tasting or silently saving the question.
- The library resumes drafts and filters work by status. History and comparison
  emphasize changes and assessed qualities. Kitchen view offers local checkmarks
  without changing the saved formulation.
- Photos show upload progress. Settings provide cover selection and caption
  editing beside previews. A nullable cover field was generated through the
  Aspire EF migration resource; development data was not reset.
- Coral actions, green standards/completion states, ivory or dark green surfaces,
  serif headings, and larger controls carry the visual direction across the app.
- The final narrow-screen pass found Fluent's internal 160 px dropdown minimum
  overflowing the editor's quantity/unit columns at 320 px. The editor uses the
  supported control-style parameter to remove that minimum, and shared fields
  constrain their grid tracks. A browser regression checks the document width.
- Kitchen checkboxes retain square 44 px targets, readable labels, and a visible
  strike-through when completed; checkmarks remain local to the open page.

Validation is recorded in [validation.md](validation.md). Local evidence is under
ignored `TestResults/uiux-implementation/`. Improvements in human task speed and
ease of use still need observation with the cook; tests and screenshots cannot
establish those outcomes.

### Code-review follow-up

- Kitchen view now includes saved recipe-level notes with their original line
  breaks. Empty notes do not create an empty section. This restores instructions
  that were visible on the regular recipe sheet but absent from the cooking view.
- The browser deletion workflow now waits for the details-only `.version-actions`
  area and scopes both deletion clicks to it. The shared recipe-name heading can
  no longer let the test interact with an outgoing editor during navigation.

## Comprehensive UI/UX review — October 3, 2026

**Reviewed application:** `21f20bb` on `main`. This original design review predates
the implementation follow-up above. Earlier resolved
findings later in this document retain their original context.

### Assessment and direction

The notebook already models the difficult parts of recipe development well:
versions, repeat batches, separate tastings, standards, and recorded corrections.
The main usability opportunity is deciding what deserves attention at each
moment. Introductions, totals, administration, optional notes, and primary work
currently occupy similarly prominent panels. The result feels more like filling
out forms than developing a recipe.

Recommended direction: **a warm, editorial recipe notebook with a quiet experiment
coach**. Use Inga's coral, leaf green, and ivory more deliberately; give the recipe
name, ingredients, and next useful action priority. The most valuable improvement
is less effort between an intention and a recorded result, supported by more
distinctive typography and composition.

Keep the existing rendering boundaries, Fluent UI, explicit saves, precise units,
recipe-wide criteria, optional scores/notes, and user-selected standards. Do not
add inventory, nutrition, sharing, scaling, or mandatory AI features to achieve
this direction. Do not infer causation or choose a winner from tasting scores.

### Browser scope and observations

Manual exploration used the Playwright Chromium library against the actual Aspire
application, PostgreSQL, and Azurite. Viewports: desktop 1440 × 900, phone
390 × 844, tablet 768 × 1024, and a 320-pixel comparison reflow check. Light and
dark appearances, keyboard navigation, accessibility-tree inspection, and print
media were included. System appearance was visible as an option; system-preference
switching was not repeated in this pass.

| Journey | Observed result and design implication |
| --- | --- |
| Home, registration, sign-in, workspace, account | Reviewed the screens and signed into the existing synthetic QA owner. The signed-in Home still presents the promotional introduction and another action to open the notebook. Account navigation is now concise. Registration and workspace creation were not resubmitted. |
| Find and resume a recipe | Library search, recipe cards, and standards are understandable. Totals occupy valuable space before recipes; the first phone card started about 454 px down. A recent draft or unfinished tasting has no direct resume action on its card. |
| Timeline and branching | Viewed the three-version Horchata history and branch view. Parentage is available. Repeated ingredient names and blank-score chips make individual experiments harder to scan. |
| Version details | Ingredients and preparation are readable. The make/taste action appears below costs, photos, and batch history, while trying a variation is prominent near the top. |
| Edit a variation | Examined four ingredients, three steps, and the existing experiment question. At 390 px wide, the first ingredient began around 1,440 px down; the page was about 4,164 px tall. The experiment panel precedes the ingredients on narrow screens. |
| Create and record a fresh recipe | Created `UI review · Cinnamon cream`, used the five ice-cream starter criteria, entered milk and a preparation step, saved, recorded a batch, and saved five scores plus a next idea. All persisted after reload. Empty-name validation was also exercised. |
| Taste a batch | With five criteria, the phone Save evaluation action began around 2,409 px down. With ten criteria it began around 3,564 px down. Each criterion permanently displays an optional note area, and the batch form precedes the tasting form on phones. |
| Finish a tasting | The saved entry contained the entered scores and next idea. The page returned to empty entry forms with a generic success notice; the meaningful result remained below them. |
| Compare versions and results | Reviewed V1/V2 ingredients, selected-tasting context, notes, score changes, and photos. No document-width overflow at 320 or 390 px. The full phone comparison still occupied roughly 3,800 px. The ingredient table already shows differences only; the proposed improvement is a concise overview, less repetitive text, and collapsing empty context/unassessed criteria. |
| Settings, photos, and print | Inspected ten-criterion settings and existing version photos; the library image decoded successfully. Printed recipe content and shell removal were correct, but heading focus decoration remained in print media. Uploads and destructive metric edits were not repeated. |

Measurements are CSS-pixel positions from the top of the document for these
fixtures, not universal page lengths or measured human task-completion times.
The existing photo fixtures include the business logo and a screenshot; they do
not establish the visual quality of a real food-photo library.

### Confirmed issues to resolve first

| ID / priority | Evidence and impact | Location and proposed remedy |
| --- | --- | --- |
| UX1 / high | On the 390 × 844 editor, keyboard Tab moved to an ingredient-name input at y=790.5–820.5 while the sticky save bar occupied y=778–844. The entire focused input was hidden. | `src/IngaCookBook.UI/Features/Notebook/Pages/VersionEditor.razor.css:5`. Reserve clearance for the actual toolbar height, including wrapping and safe areas; use document scroll padding/appropriate scroll margins and verify focus visibility in both directions. Keep document scrolling. Evidence: `17-focused-input-obscured.png`. |
| UX2 / high | Submitting a blank recipe name showed one combined name/description error above the long form. Focus ended on the document body, without identifying/focusing the recipe-name field. Inputs were retained and the alert was announced, but recovery required locating the problem manually. | `src/IngaCookBook.UI/Features/Notebook/Pages/NewRecipe.razor:12` and `NewRecipe.razor.cs:31`. Add field-specific errors associated with their inputs, focus the first invalid field on submission, and retain a concise linked summary for multiple errors. Evidence: `22-new-recipe-error.png`. |
| UX3 / low | Library totals use 32 px text with an inherited 20 px line height. Numerals crowd their labels in both themes. Cards also show singular counts as “1 versions” and “1 batches.” | `src/IngaCookBook.UI/Features/Notebook/Pages/Library.razor.css:13` and `Library.razor:53`. Give display numerals an explicit suitable line height and pluralize counts. If totals are moved to a secondary location, retain this correction there. Evidence: `03-library-desktop.png`, `27-library-phone-dark-closed.png`. |
| UX4 / low | Navigating directly to the print page focuses its heading. Under print media the heading still has the global coral 2 px outline, so printing immediately through the browser can decorate the recipe with a focus box. | `src/IngaCookBook/wwwroot/app.css:82` and `app.css:133`. Remove interactive focus decoration in print media only; retain visible keyboard focus on screen. Evidence: `29-print-focused-heading.png` and `print-focus-check.pdf`. No physical printer was tested. |

UX1 is the kind of author-created sticky-content obstruction addressed by
[WCAG 2.4.11, Focus Not Obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html).
This observation is not a complete accessibility-conformance assessment.

### Workflow improvements, in recommended order

1. **Make the editor start with the recipe.** Put the actual recipe name and
   version label above ingredients. On phones, reduce the coach to a short
   “Focus: Scoopability · 1 change” disclosure and put it after, or alongside,
   the active work. Expand relevant guidance when multiple changes need an
   explanation. Keep a fuller side panel on desktops. Use compact ingredient
   rows, with purchase costs and linked recipes available on demand. Baseline
   entry does not need the same prominence of experiment guidance as a variation.
   Locations: `VersionEditor.razor:24`, `VersionEditor.razor:64`.
2. **Give tasting its own focused view.** Separate recording a batch from scoring
   one. Show the selected version, batch, and date once, then provide discrete
   1–10 choices and expandable notes. On phones use two rows of five sufficiently
   large choices rather than ten tiny controls. Support keyboard operation,
   clearing a score, and genuinely unassessed values. A compact overview should
   remain the default; a one-criterion-at-a-time mode could be optional, not ten
   mandatory screens. Location: `BatchJournal.razor:14` and `:48`.
3. **Let successful saves feel complete.** Show the saved tasting summary and
   next idea immediately, with “Done” and “Try this as a variation.” Carry the
   idea into a new draft as an editable question; preserve the original tasting.
   Batch recording should offer “Taste now” or “Later.” Keep explicit saving and
   clear unsaved/saving/saved states. Location: `BatchJournal.razor:10`,
   `NotebookPage.cs:40`. This does not require confetti or decorative animation.
4. **Make the next useful action obvious.** A draft needs “Continue editing”; a
   standard needs “Make a batch”; a batch needs “Add a tasting.” Keep trying
   alternatives available without giving it priority during ordinary cooking.
   Put deletion, historical correction, and promotion in a clearly labeled
   secondary action area, retaining their safeguards. Locations:
   `VersionDetails.razor:18`, `:59`, `Components/VersionActions.razor`.
5. **Turn the library into a working notebook.** Land signed-in users here, with
   a recent-work section, direct resume actions, search, and modest status
   filters. Move the large totals below the work or remove them. Use an intentional
   compact fallback when there is no photo; let a real recipe photo be the cover
   when available. Keep card links and secondary actions semantically separate.
   Locations: `Features/Home/Pages/Home.razor:10`, `Library.razor:18`, `:39`.
6. **Make comparison answer the question quickly.** Lead with the changed
   ingredient/step, then the relevant recorded score changes: “Cream 240 → 260 g;
   scoopability 6 → 8.” Retain both selected tasting identities/dates. Hide
   unassessed criteria and empty context behind an explicit disclosure. Keep all
   precision, notes, changes, and photos available. Use text and simple aligned
   comparisons rather than a decorative radar chart or invented overall score.
   Location: `CompareVersions.razor:24`, `:34`, `:51`.
7. **Make history read like experiments.** Give each entry a short question,
   parent version, date, compact change summary, and the few relevant scored
   outcomes. Replace repeated full ingredient names with the changed values.
   Label untested drafts clearly and give the current standard a distinctive
   green marker. Keep the branch view as an optional explanation of lineage.
   Location: `Recipe.razor:42–69`.
8. **Reduce first-use paperwork.** Start with a name and a suggested, editable
   evaluation preset; let the cook get to ingredients immediately. Explain that
   criteria can be adjusted later. Do not quietly discard custom criteria when
   changing a preset. Workspace setup should use plain “Your kitchen” language
   while preserving the tenant model. Location: `NewRecipe.razor:15`.
9. **Improve kitchen ergonomics.** Aim for 16 px body/entry text and 44–48 px
   common action targets, with comfortable spacing and visible focus. Shorten
   repeated introductions rather than shrinking everything. Those target sizes
   are a comfort recommendation, not a claim that WCAG AA requires 44 px;
   [WCAG's minimum criterion](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html)
   uses 24 CSS px with exceptions. Locations: `app.css:50–58`, shared Fluent
   sizing, and `Layout/MainLayout.razor.css`.
10. **Make photos purposeful.** Place a restrained “Add photos” action near
    the recipe, with clear upload progress and successful/failed-file feedback.
    Favor a chosen cover and texture/detail captions over a permanent large
    empty photo panel. Preserve version ownership of images. Locations:
    `VersionDetails.razor:34`, `Components/PhotoUpload.razor`.

### Visual design and distinctive ideas

- **Typography with a clear hierarchy:** carry the warm serif character of the
  landing page into recipe names and section headings. Pair it with readable
  Fluent body text and tabular quantities. Make actual recipe context more
  prominent than generic headings such as “Shape your next discovery.”
- **Color with a purpose:** coral for the primary action and experimentation;
  leaf green for standards and recorded positive outcomes; ivory for quiet
  working space. Use a deeper coral for small text on light backgrounds and a
  lighter coral on dark surfaces. Never rely on color alone for status or deltas.
  Keep system/light/dark behavior. Avoid large decorative color blocks that push
  useful information away.
- **Fewer competing boxes:** one recipe sheet with grouped content, restrained
  dividers, and a smaller contextual coach. Costs, audits, and optional context
  should remain easy to find without becoming equal-sized primary panels.
- **An experiment receipt:** after a save, a compact factual record of the
  question, exact changes, selected batch/tasting, and the cook's observation.
  This could become the readable unit of history. Group related edits in the
  presentation without hiding the actual changes or silently bypassing the
  explanation required for multiple changes.
- **Turn “next idea” into a useful starting point:** a single action carries that
  text and the selected focus into a variation. The app supplies continuity;
  the cook supplies the judgment. No AI recipe generation is necessary.
- **A later kitchen reading mode:** large ingredients, tappable preparation
  checkmarks, and a clear current step on a tablet. Keep the original ordered
  instructions and quantities unchanged. Validate this with the cook before
  adding timers or other scope. It is less urgent than fixing editor and tasting
  friction.

### Visual concept and implementation sequence

An isolated local prototype is at
`TestResults/uiux-review/inga-notebook-concept.html`. It demonstrates Recipe,
Taste, and Compare views, coral/green light and dark treatments, discrete score
selection, optional notes, and a saved-result preview. Its sample content is
illustrative; it does not call the application or save data. Application
implementation would use Fluent components and the existing SSR/InteractiveAuto
boundaries, not copy a second UI framework into the product.

1. **Correct usability defects:** UX1–UX4, then readable type and action sizing.
   Acceptance: no focused field hidden by a toolbar at the supported sizes;
   invalid submission identifies the exact field; print has no focus decoration.
2. **Redesign the daily loop:** editor, make/taste separation, and completion
   summaries. Acceptance: an ordinary mobile draft reaches ingredients in the
   first useful screen without scrolling past the full coach; five scores can
   be entered without opening a software keyboard or five unused note areas;
   saved results appear immediately. Test ten-criterion and long-label cases too.
3. **Apply the visual direction across the product:** library, version details,
   history, comparisons, onboarding, account/settings, empty and error states.
   Keep browser Back/Forward, enhanced navigation, static account POSTs,
   concurrency handling, and unsaved-input protection intact.
4. **Observe the actual cook:** ask her to resume a draft, change one ingredient,
   record a tasting, and find the better-scoring attempt without coaching.
   Compare time, mis-taps, uncertainty, and recovery against the existing app.
   Treat faster task completion as something to measure, not a promised outcome
   of this visual prototype.

### Evidence, limits, and review outcome

Local screenshots and the print PDF are under ignored `TestResults/uiux-review/`.
Useful files include `07-editor-desktop.png`, `14-editor-phone.png`,
`15-journal-phone.png`, `17-focused-input-obscured.png`,
`20-history-desktop-dark.png`, `22-new-recipe-error.png`,
`23-tasting-saved-desktop.png`, `24-tasting-five-metrics-phone.png`,
`25-version-tablet.png`, `27-library-phone-dark-closed.png`,
`28-compare-320.png`, and `29-print-focused-heading.png`.
Concept screenshots are `concept-desktop.png`, `concept-desktop-dark.png`,
`concept-phone-tasting.png`, and `concept-phone-compare.png`.

There were **zero uncaught page exceptions observed**. A Playwright role lookup
failed for Fluent buttons after WebAssembly activation, but Chromium's actual
accessibility tree retained their button roles; this was not recorded as an app
accessibility defect. Some initial screenshots caught image loading or an open
mobile drawer; settled captures were used when evaluating those surfaces. The
normal on-screen focus outline around a navigated heading is not itself a bug.

This pass was not a screen-reader audit, physical-device or virtual-keyboard
test, Safari/Firefox pass, production Azure test, performance benchmark, or full
workflow regression run. Destructive operations, concurrent writes, passkeys,
and photo upload failure paths were not re-exercised. No automated test-suite
pass count is claimed. The added recipe/batch/tasting remains in the synthetic
QA workspace. No application source or existing recipe was changed. The temporary
browser session and the Aspire instance started for this review were stopped
normally, preserving the development data.

Review guidance included the
[Web Interface Guidelines](https://github.com/vercel-labs/web-interface-guidelines/blob/main/command.md)
and the W3C criteria linked above. AGENTS.md, README.md, build/README.md,
tests/README.md, and the existing product/validation docs were checked. Product
behavior and setup guidance remain accurate; only this review log and its
validation cross-reference need updating for this task.

## Brand and layout investigation — October 3, 2026

Baseline: `e8d1b31`, plus this working change set. Tested through Playwright's
Chromium library against the real Aspire app, PostgreSQL and Azurite. A separate
synthetic `Inga’s brand QA kitchen` workspace contains these experiments; existing
recipes were not edited. Desktop was 1440 × 900, phone viewport 390 × 844,
timezone America/Chicago. Browser actions used normal entry, selection and
navigation; wheel scrolling was checked explicitly.

| Finding | Diagnosis and outcome |
| --- | --- |
| B4: wheel scrolling does nothing | Reproduced `body { height: 100dvh; overflow: hidden }` from Fluent's baseline. The document was taller than the viewport but wheel input left `scrollY` at zero. Application CSS now restores document scrolling. Long metric settings scrolled 657 px on desktop and 741 px on the phone viewport. |
| B5: inset line in the header | The outer layout item had 8 px padding around a nested header with a bottom border. The header now occupies its full 64 px row with a single border at the outside edge. |
| B6: dropdown inner box and low text | A global native `button` rule added borders/padding to Fluent's light-DOM combobox button. Native styles are now scoped; the inner dropdown control has zero border/padding. Pointer and keyboard selection both worked. |
| B7: misaligned action/input rows | Native action links were taller than Fluent buttons; field margins and an empty message-row gap displaced metric controls. Shared action sizing and scoped field-row alignment correct these. |
| I6: logo theme and phone readability | Coral/ivory/green tokens replace the brown palette in light/dark/system modes. Standards use green, the focus panel fits its content, the static navigation shares the surface palette, and score tables give metric names more room on phones. |
| B8: antiforgery cache warning | The API filter set only `no-store`, conflicting with framework token generation. Responses now use the framework-compatible `no-cache, no-store` plus `Pragma: no-cache`. |
| I7: routine Azure container conflict warning | Every upload attempted `CreateIfNotExists`, producing a handled Azure 409 for the existing container. Uploads now check existence first; the race-safe create remains for initial provisioning. Storage errors are still reported. |
| Open in pinned 13.6.0: Aspire ContainerExec watcher timeout | Confirmed upstream regression. A controlled comparison reproduced missed command state in 13.6.0 and verified the official 13.6.1 staging fix across the five-minute watch restart. See investigation below. |

### Realistic workflow checks

| Scenario | Observed result |
| --- | --- |
| Registration, confirmation, sign-in, workspace | A fresh owner created a private USD workspace through the existing Identity flow. |
| Horchata baseline | Saved 600 g milk, 240 g cream, 120 g sugar and 40 g rice, three preparation steps and a 1000 g yield. Package quantities/prices produced the expected $3.60 total. |
| Photos | A text file was rejected with feedback. The supplied 1280 × 1280 business-reference JPEG uploaded and decoded through authenticated blob delivery. It was used only as QA photo content. |
| Batches and repeated tastings | Two unchanged baseline batches retained three distinct tastings. Score `11` was rejected unchanged. Blank criteria remained unscored. |
| Audited corrections | Corrected creaminess 7 → 8 with a reason and added actual aging duration to batch notes. Audit summaries retained the correction reasons and prior records. |
| Controlled variation | Changed only cream 240 → 260 g. Comparison showed that ingredient change, creaminess 8 → 9 and scoopability 6 → 8. |
| Related changes and branches | A sibling from V1 changed rice quantity and infusion time. Saving required an explanation; both V2 and V3 appeared under V1. Promoting V3 created an independent recipe with provenance. |
| Pinned nested recipe and print | Affogato included 100 g of preserved V2 and espresso. Print expanded the nested recipe. Correcting V2's yield from 1000 to 1020 g left the affogato's saved 1000 g snapshot intact. A4 PDF inspected. |
| Long criteria settings | Added six criteria to the five starter metrics, saved, and reached lower controls with the mouse wheel on desktop and phone. |
| Historical metric deletion | Removing the scored Flavor metric required acknowledgement; saved evaluations then omitted it. |
| Concurrent tabs | First settings save won. Stale second save showed a conflict and retained its entered description. |
| Phone input protection | Canceling navigation from a dirty preserved editor retained phone-entered notes. Saving a reasoned correction then succeeded. |
| Navigation and appearance | Mobile drawer closed after enhanced navigation; library and comparison had no document overflow. Light/dark persisted through navigation, and system mode followed emulated OS changes. No JavaScript console errors/warnings were observed during the workflow session. |

### Aspire log investigation and remaining limitation

The attached stack trace is one failed local orchestration watch, not a series of
recipe persistence failures. The same `Polly.Timeout.TimeoutRejectedException`
and `ContainerExec` critical message appeared in repeated clean CLI starts with
Aspire 13.6.0/KubernetesClient 19.0.2. Web/database/blob resources remained healthy
and the tested application operations succeeded. Container-command monitoring
is still affected; this is not classified as a clean AppHost log run.

The October 3 follow-up confirmed the cause against checksum-verified source
for installed `Aspire.Hosting` 13.6.0 (commit `56f3e9c0d216c0c7069dabb49dd0464e4827744f`):

1. `WatchAsync` awaits the initial Kubernetes watch response inside its bounded
   initialization pipeline. KubernetesClient 19.0.2 waits for the first content
   line before returning that response. A healthy but empty event stream can
   therefore exceed the 60-second initialization budget.
2. The timeout surrounds the retry strategy, and the watcher's outer retry does
   not handle `TimeoutRejectedException`. The watch terminates instead of
   reconnecting. Increasing an application/database timeout does not fix this.
3. There is also a cancellation gap when the five-minute periodic restart occurs
   while the watch factory is still waiting for its first event.

Microsoft's [fix #20466](https://github.com/microsoft/aspire/pull/20466) changes
watch connection timeouts to apply per attempt, retries those timeouts, and
handles periodic cancellation during factory creation while preserving unrelated
cancellation. Its [13.6 backport #20664](https://github.com/microsoft/aspire/pull/20664)
was merged at `07f7de73da886785e4d0b8f22ac35928d56d80ee`. This matches
[the reported 13.6.0 regression #20609](https://github.com/microsoft/aspire/issues/20609).

#### Controlled verification of the upstream fix

Two disposable .NET 10 AppHosts were run through the Aspire CLI with random
ports, session containers, and no mounted development data. Neither contained
IngaCookBook, EF migrations, Azurite, registration, or recipe code. A plain
PostgreSQL AppHost first reproduced the critical after one minute. The controlled
pair then included a second, explicit-start PostgreSQL container and an
associated `/bin/echo` ContainerExec. The parent was started later so the watch
initially had no command events. Creating that diagnostic resource required
reflection over Aspire's internal model **only in the ignored reproduction**;
no reflection workaround was added to application code.

| Build | Idle-watch result | Late command result |
| --- | --- | --- |
| Stable 13.6.0, KubernetesClient 19.0.2 | Critical at 18:14:57 UTC, about 60 seconds after host startup. | Parent became healthy, but the command state remained absent in Aspire at the final 480-second observation. |
| Official staged 13.6.1, KubernetesClient 19.0.2 | No warning/error/critical entries during the 474-second observation. | Parent started at 18:20:03 UTC, six minutes after host startup and beyond the five-minute watch restart. Aspire observed `Finished` and captured `CONTAINER_EXEC_WATCH_OK`. |

The Windows x64 staging CLI archive was SHA-512 checked against Microsoft's
published checksum. Both its version identity and the restored Hosting package's
repository metadata identify the merged backport commit above. The immutable
package feed was `https://pkgs.dev.azure.com/dnceng/public/_packaging/darc-pub-microsoft-aspire-07f7de73/nuget/v3/index.json`.
The staging archive's build location was `13.6.1-preview.1.26502.4`; its CLI and
package version are `13.6.1`. This is a **staging validation**, not a claim that
13.6.1 is released on NuGet.org. The stable feed still ended at 13.6.0 when checked.

The two final controlled observations matched their expected outcomes: one
reproduced defect and one verified fix. These were diagnostic CLI runs, not a new
automated application-test pass count. An earlier probe incorrectly relied on
`WithExplicitStart` on the internal command itself; it executed during startup
and was excluded from the idle-watch comparison. The corrected probe delays the
parent container. The existing application suites were not rerun for this
investigation, and the complete application has not yet been validated on staging.

**Recommendation and decision:** keep IngaCookBook on stable 13.6.0 for now;
upgrade to stable 13.6.1 (or a later stable release containing #20664) when it is
published. The tested recipe workflows and database/blob services remain usable,
but a fresh AppHost restart alone is not a lasting fix for an idle command watch.
No log suppression, timeout inflation, internal patch, or downgrade was added.
No background upgrade or monitoring task was scheduled.

When upgrading, update the documented Aspire stack coherently, build the
solution, run unit/component and affected infrastructure/browser suites, and
repeat the empty-watch/late-command check beyond five minutes. Verify startup,
migration completion, `/health`, database/blob access, and relevant AppHost logs.
The isolated result does not substitute for that complete application validation.
Both disposable AppHosts were stopped normally. The unrelated AppHost running on
this machine was left untouched.

Local, ignored evidence is in `TestResults/containerexec/`: `repro/` and `fixed/`
contain the minimal projects, `validation-summary.json` contains sanitized final
states/diagnostics, and `fixed-command-output.txt` contains the observed output.
Files marked `private` and raw CLI logs may contain dashboard credentials and
must not be shared. Project and global CLI dependencies were not changed.

Sources: [Aspire watch implementation](https://github.com/microsoft/aspire/blob/56f3e9c0d216c0c7069dabb49dd0464e4827744f/src/Aspire.Hosting/Dcp/KubernetesService.cs),
[Kubernetes client watch response](https://github.com/kubernetes-client/csharp/blob/v19.0.2/src/KubernetesClient/Kubernetes.cs#L53-L87).

Evidence is local and ignored under `TestResults/brand-qa/`: baseline, settings,
comparison, branches, mobile light/dark library, nested print screenshot and PDF.
The original screenshots and before-editor capture establish the layout defects.
Automated commands/results are recorded in [validation](validation.md).

Final build recheck: two more files uploaded together into the existing container;
all three photos decoded. Application telemetry returned no warnings/errors,
the browser recorded no JavaScript errors/warnings or HTTP 5xx responses, and the
Aspire ContainerExec timeout remained the only critical AppHost finding. Metric
inputs and adjacent buttons had a 0 px bottom-edge difference. At 390 px, the
document was 390 px wide and all three comparison tables fit their 316 px
containers. Primary-action foreground/background contrast was 5.30:1 in light
mode and 5.72:1 in dark mode. The final solution build had zero warnings/errors;
format verification passed. QA Aspire and browser processes were stopped normally;
the synthetic workspace remains available for reproducing these scenarios.

Limits: Chromium with phone-sized viewports, not physical touch hardware,
Safari/Firefox, screen-reader testing, production Azure, or paper-printer output.
Very rapid scripted clicks initially inspected UI before asynchronous rendering
completed; subsequent steps waited for rendered controls and used keyboard entry.
Those automation races were not reported as data-loss defects.

## Original exploration

Run: October 2, 2026 (America/Chicago), commit `ff81f23`.
Status: completed. Browser exploration through the Codex in-app browser and
standalone Playwright Chromium 153.0.8010.12, using the local Aspire application,
PostgreSQL, and Azurite. Desktop: 1280 pixels; phone viewport: 390 × 844. All entered data is
synthetic, under the separate `QA kitchen — realistic workflows` workspace.
Application source was unchanged during that exploratory run. The original
observations below are retained as reproduction evidence; the follow-up fixes
are tracked separately here and in [automated validation](validation.md).

## Findings follow-up — October 3, 2026

All six findings are resolved and verified with regression checks:

| Finding | Change | Regression evidence |
| --- | --- | --- |
| B1 | History, standard selections, and correction timestamps localize in the browser, with an explicit UTC fallback in static HTML. Batch/tasting date-only values remain unchanged. | `HistoryTimestampHasAnExplicitUtcFallbackAndAnUnambiguousInstant`; `EditorProtectsEnhancedNavigationAndJournalUsesBrowserDates`. |
| B2 | The journal reuses the editor's protection for links, Back/Forward, programmatic navigation, and reloads. Saving one form preserves unsaved input and its warning in the other. | The Server/WebAssembly browser regression cancels each kind of navigation, checks retained notes, permits navigation after saving, and protects unfinished preparation notes after a tasting save. |
| I1 | Scores retain invalid input and show a whole-number 1–10 error instead of clamping. Invalid entries cannot save. | `JournalRetainsInvalidScoresAndDoesNotSaveThem` covers 0, 11, fractional, and nonnumeric values; `JournalSavesBlankAndBoundaryScoresWithoutChangingTheirMeaning` covers blank, 1, and 10. The browser checks 11 stays visible. |
| I2 | **Correct tasting** requires a reason and retains previous values in visible history. Identity and recording order remain stable, stale saves are rejected, and deleting a criterion removes its scores from audit snapshots too. | `TastingCorrectionsPreserveHistoryAndOrderAndRemoveDeletedCriteriaFromAudit`, `InvalidTastingCorrectionsLeaveOriginalAndAuditUntouched`, `JournalCorrectionUsesOriginalIdAndRetainsEditsAfterConflict`, and both browser renderers. |
| I3 | Amount, purchased amount, and yield retain six-place precision without trailing zero padding. | The desktop/mobile browser workflow checks `100.123456`, `100.1256`, and whole purchased amount `1000`, including save and comparison. |
| I4 | Preserved recipe corrections show only changes to that saved version and use the correction reason without an extra experiment explanation. Draft variations retain their existing guidance. | `PreservedVariationCorrectionNeedsOnlyItsCorrectionReason` and `PreservedVersionShowsItsCorrectionDiffWithoutExperimentWarning`; existing multiple-change variation regression retained. |

The tasting audit table is generated by the configured Aspire EF resource in
`AddEvaluationCorrections`. Test resources use disposable databases and storage;
the original exploratory workspace remains available. See the validation log for
final run counts and commands.

Documentation review updated this follow-up, the product guide, validation evidence,
and affected test-layer descriptions. Existing setup, build, and agent guidance
continues to apply.

## Second exploratory pass — October 3, 2026

Status: completed against `ff81f23` plus the uncommitted six-finding fixes described
above. This pass used Playwright's installed Chromium 153.0.8010.12 through the
actual Aspire app, PostgreSQL, and Azurite. It exercised the UI with ordinary
keyboard entry, clicks, file selection, navigation, and reloads; no API seeding or
direct database changes were used. Desktop viewport: 1280 × 900; phone viewport:
390 × 844. Primary timezone: America/Chicago; a separate authenticated context
used Pacific/Honolulu to cross the calendar-day boundary with real timestamps.

All records are synthetic in `QA Round 2 — kitchen experiments`, owned by a new
QA account. The original QA workspace and existing user data were not edited.
The new workspace is retained to reproduce the observations, including the
deliberately incorrect future-dated batch. No application code was changed.

### Scenario results

| Scenario | Observed result |
| --- | --- |
| Register, confirm, sign in, and create a workspace | Passed with the development confirmation flow and USD currency. |
| Enter a frozen-yogurt baseline with costs and ordered steps | Saved and reopened 600 g yogurt, 250 g puree, 150 g sugar, two steps, and 1000 g yield. Whole amounts remained unpadded. Prices of $6, $8, and $2 per 1000 g produced the expected $5.90 total. |
| Accidental score `11` | Rejected with a visible whole-number 1–10 error. Input remained `11`; no perfect-score evaluation was created. |
| Leave an unfinished tasting | The version link prompted; canceling retained the score and notes in the journal. |
| Correct a saved tasting | Corrected Scoopability 4 → 5 with a reason. Current score, original score, notes, and reason appeared in the expanded audit. |
| Record another batch while a tasting is unfinished | Batch 2 saved separately. The unfinished tasting kept its text and association with Batch 1; saving then produced Batch 1's second evaluation. |
| One-variable process experiment | V2 changed only aging from 4 to 12 hours; it saved without a related-changes explanation. |
| Select a standard, then correct its yield | V2 became the standard. A 1000 → 990 g correction saved using only its correction reason; it did not require another experiment explanation. |
| Related two-row substitution from the original baseline | V3 replaced 30 g sugar with dextrose. Saving without an explanation was rejected; explaining the single substitution allowed saving. V2 and V3 both appeared as children of V1. |
| Choose an earlier tasting in comparison | Default showed the later partial tasting. Selecting the first tasting showed the corrected Scoopability score 5, rather than its old score 4. |
| Rename, add, and remove criteria | Flavor → Strawberry flavor kept its scores; new Fruit freshness was blank in earlier tastings. Removing Scoopability required acknowledgement and removed it from current and prior correction snapshots, while general observations remained. |
| Promote a variation | `QA Softer strawberry` opened as a separate recipe with one version, no batches, and provenance back to its source. |
| Use a recipe inside another recipe | A sundae containing 200 g of V2's 990 g yield cost $1.19 ($5.90 × 200/990). After correcting the source yield to 900 g, the sundae retained its saved 990 g formulation and $1.19 estimate. |
| Upload and reject photos | A synthetic PNG uploaded and appeared. Text content named `.png` was rejected, and the existing photo remained. |
| Print the nested recipe | Print media expanded the saved ingredients and steps, including the nested yield, and hid application navigation. Saved an A4 PDF and inspected the print screenshot. |
| Phone layouts, themes, navigation, search, and keyboard actions | Editor, nested recipe, and comparison were usable at 390 px. Comparison document width was 390/390 px; each table was 316/316 px. Dark mode survived reload, system mode followed emulated light/dark, and enhanced navigation closed the drawer. Search returned the sundae. Keyboard Tab reached Save draft. |
| Two tabs correct the same tasting | First save persisted. The stale second save showed a conflict and retained its typed correction; reloading the first tab showed the winner without the stale text. The journal reported WebAssembly rendering. |
| Actual timestamp across a timezone boundary | All three version timestamps stored on October 3 UTC displayed October 2 in Pacific/Honolulu, matching browser-local formatting. |
| Leave unfinished recipe creation/settings | **Failed: B3 below.** Both forms lost input without a prompt. |
| Recover from a mistaken batch date | Date validation correctly rejected an earlier tasting, but there was no way to correct the saved batch date. **Improvement I5 below.** |

### B3 [P2] — Recipe creation and settings silently lose unfinished input

- **Impact:** a cook can lose a typed description or newly configured criteria
  when briefly checking another recipe, despite the editor and journal having
  unsaved-change protection.
- **Reproduce in settings:** open **Recipe & evaluation settings**, change the
  description, add `Fruit freshness`, then click **← Recipe history** without
  saving. Return to settings.
- **Actual:** no confirmation dialog appeared; the original description and five
  saved metrics returned. The new metric and typed description were gone.
- **Also reproduced on a phone:** type a name and description on **New recipe**,
  follow **← Recipe library**, and reopen **New recipe**. The name was blank and
  no dialog had appeared.
- **Expected/remedy:** apply the existing dirty-form protection to both screens,
  or retain recoverable drafts; clear protection only after a successful save.
- **Locations:** [RecipeSettings.razor](../src/IngaCookBook.UI/Features/Notebook/Pages/RecipeSettings.razor)
  at line 7 and [NewRecipe.razor](../src/IngaCookBook.UI/Features/Notebook/Pages/NewRecipe.razor)
  at line 7. Their code-behind stores unsaved values only in component fields;
  neither page wired the editor/journal navigation guard during this pass.
- **Evidence:** `settings-unsaved-before.png`, `settings-unsaved-after.png`,
  `new-recipe-unsaved-before.png`, `new-recipe-unsaved-after.png`.
- **Status:** fixed in the second findings follow-up below.

### I5 — Add an audited correction for batch dates and preparation notes

- **Scenario:** accidentally record a batch as October 4 when it was made on
  October 3. Attempt to save an October 3 tasting for that batch.
- **Observed:** the tasting correctly rejects with “A tasting cannot be earlier
  than the batch was made.” The batch remains dated October 4. The journal offers
  **Record batch** and **Correct tasting**, but no way to correct the saved batch
  date or preparation notes. Recording another batch would leave the erroneous
  batch in history.
- **Suggested improvement:** provide **Correct batch** with a reason and prior
  values, consistent with recipe/tasting corrections. Validate the corrected date
  against existing tastings. This is a recovery feature request, not a failure
  of the existing tasting-date validation.
- **Location:** [BatchJournal.razor](../src/IngaCookBook.UI/Features/Notebook/Pages/BatchJournal.razor),
  recorded-batches section beginning at line 62.
- **Evidence:** `batch-date-recovery.png`.
- **Status:** implemented in the second findings follow-up below.

### Evidence and limits for the second pass

Artifacts are local and ignored under `TestResults/exploration-20261003/`.
In addition to the finding screenshots, the folder contains `baseline-editor.png`,
`invalid-score.png`, `tasting-correction.png`, `single-change.png`,
`yield-correction.png`, `comparison-corrected-score.png`, `metric-removal-audit.png`,
`branches.png`, `nested-print.png`, `sundae.pdf`, `mobile-nested.png`,
`mobile-editor.png`, `mobile-comparison.png`, `mobile-dark-library.png`,
`stale-tasting-correction.png`, and `timezone-boundary.png`. `session-summary.json`
records the synthetic recipe URLs and browser version without passwords or cookies.

No page JavaScript errors were observed on the main page. Locator timeouts while
discovering labels, waiting for the wrong destination, or clicking before
interactivity were corrected and are not counted as application defects. Some
Fluent controls appear as text in Playwright's ARIA snapshot; a Chromium
accessibility-tree check confirmed Save draft's button role and keyboard focus.
The open modal drawer intercepts the header menu button; Escape closed it normally.

This was exploratory browser QA, not a new automated suite run. The earlier
493-test result in [validation](validation.md) was not rerun or increased here.
There was no physical touch device, screen-reader session, Safari/Firefox,
production Azure, real camera-image test, native paper printing, or load test.
The WebAssembly journal was directly verified; this pass does not independently
claim full Server/WebAssembly transition coverage.

Browser contexts and the temporary browser process were closed. Aspire was stopped
normally, preserving development data. Documentation review checked AGENTS.md,
README.md, build/README.md, tests/README.md, and the existing product/validation
docs. Only this log and its validation cross-reference needed updates; setup,
feature behavior, and agent/test conventions remain unchanged.

The required `dotnet format IngaCookBook.slnx --severity warn --no-restore` and
matching `--verify-no-changes` pass both exited successfully. Hash comparison
confirmed the formatter did not change the existing non-documentation work;
`git diff --check` was clean. No new build or automated test run was needed for
these documentation-only changes.

## Second findings follow-up — October 3, 2026

B3 now uses the shared unsaved-input guard on **New recipe** and **Recipe &
evaluation settings**. It covers text and criteria edits, enhanced links,
Back/Forward, reloads, and programmatic navigation. Rejected saves keep inputs and
protection; successful creation clears the guard before opening the first version.

I5 adds **Correct batch** with a required reason, previous date/notes, and visible
audit history in the journal and version details. It preserves the original batch,
its tastings, and unfinished tasting input. Corrections reject stale revisions,
foreign-workspace access, and dates later than a saved tasting. Stable persisted
positions prevent a date correction from renumbering batches; `AddBatchCorrections`,
generated through the Aspire EF resource, backfills the previous batch order.

Regression coverage includes `NewRecipeRetainsRejectedInputAndClearsGuardBeforeSuccessfulNavigation`,
`SettingsMetricEditsRemainGuardedAfterRejectionAndClearAfterSave`,
`BatchCorrectionRetainsConflictInputsAndDoesNotCreateAnotherBatch`,
`BatchCorrectionsRetainIdentityOrderTastingsAndSuccessiveHistory`,
`InvalidBatchCorrectionsLeaveDateNotesTastingsAndHistoryUntouched`, and
`BatchPositionMigrationPreservesExistingChronology`. The existing Server/WebAssembly
browser regression also covers both guarded forms and recovery from an incorrect
batch date through saving and reloading its tasting and correction audit.

See [validation](validation.md) for the completed run counts and commands. This
follow-up uses disposable test resources; the original QA workspaces remain intact.
Documentation review updated the product guide, this log, validation evidence,
and test-layer descriptions. Setup, build rules, and agent guidance remain accurate.

## Scenarios

| Scenario | Result / evidence |
| --- | --- |
| Register, confirm account, sign in, create USD workspace | Passed using a new QA account. |
| Baseline vanilla recipe: three ingredients, two steps, five metrics, yield and purchase costs | Saved and reopened correctly. 550 g milk at $2.40/kg + 300 g cream at $8/kg + 150 g vanilla sugar at $3/500 g = $4.62. |
| Repeat tastings and repeat batches | Two batches and three evaluations remained separate. Partial scoring stayed blank for unassessed metrics. Local default date was October 2. |
| Single-variable variation | V2 inherited V1; cream 300 → 350 g showed one change and cost $5.02. Focus metric and question persisted. |
| Select specific tastings for comparison | Default chose the most recent tasting. Selecting baseline batch 1/tasting 1 showed creaminess 6 → 8 (+2), scoopability 4 → 7 (+3), overall 6 → 8 (+2), including notes. |
| Independent sibling experiment with multiple changes | V3 branched from V1, changing sugar 150 → 130 g and aging 12 → 18 hours. Two changes appeared; saving without an explanation was rejected and retained the inputs. Providing an explanation allowed saving. Branch view showed both V2 and V3 under V1. |
| Choose a winner, then correct its historical record | V2 replaced V1 as standard. Correcting yield to 1050 g retained the prior 1000 g formulation and correction reason in the visible audit. See I4 for the extra explanation required. |
| Promote a variation into an independent recipe | `QA Premium vanilla` started with one version and no batches. Its origin link remained available. Later metric changes to the original did not change its five original criteria. |
| Add, rename, and remove evaluation criteria | Flavor → Vanilla flavor retained score 8. New Melting behavior showed Not scored in the old evaluation. Removing Creaminess required acknowledgement; it disappeared from the old journal while other scores/observations remained. |
| Multiple photos and invalid file | Two synthetic PNG files uploaded and both decoded in the browser. A text file named `.png` was rejected; the two existing photos remained. Real cloud Azure was not used. |
| Generic food recipe and a pinned recipe ingredient | `QA Affogato` used its own Coffee and cream balance metric, 100 g of V2 ice cream, and 30 mL espresso. Cost was $0.78: $5.02 × 100/1050 + $10 × 30/1000. A deliberate mass/volume purchase mismatch produced an incomplete estimate. Compatible mL units restored the complete estimate. |
| Correct source after saving a linked recipe | Corrected V2 cream purchase price from $8/kg to $10/kg. The affogato retained its saved formulation and $0.78 cost. |
| Print a nested recipe | Print media expanded the included recipe, showed its original quantities/yield and steps, and hid the application shell. An A4 PDF was generated. No physical printer was tested. |
| Phone layout and themes | History, comparison, and editor document widths were 390/390 pixels; comparison containers were 316/316. Screenshots were inspected. Explicit dark/light and emulated system preference changes updated the background correctly. Drawer closed after enhanced navigation settled. |
| Unsaved recipe protection | In Playwright, canceling the leave prompt retained both the editor URL and the typed note. Saving allowed navigation. The embedded-browser dialog issue was tooling-specific. |
| Stale concurrent tab | First tab saved a note. Second tab's stale save showed a conflict message and retained its input. Reloading the first tab showed the first writer's saved note. |
| Unsaved tasting navigation | Failed: entered score and metric notes disappeared without a prompt after leaving and returning. See B2. |

## Confirmed bugs

### B1 [P2] — History and standard dates disagree with the local journal date

- **Impact:** misleading chronology for an evening kitchen session in a timezone west of UTC.
- **Reproduce:** in America/Chicago after 19:00 on October 2, create a version,
  record a batch, and select the version as standard.
- **Actual:** journal defaults and recorded batch/tasting dates are October 2;
  version timeline and “Selected as standard” display October 3.
- **Expected:** display timestamp-based history in a consistent local timezone,
  or explicitly label UTC. Keep the user's date-only batch/tasting values intact.
- **Observed:** V1 and V2 timeline, V1 standard selection.
- **Locations:** `src/IngaCookBook.UI/Features/Notebook/Pages/Recipe.razor:51`
  and `VersionDetails.razor:70` format the stored timestamp directly.
- **Evidence:** `history-dates.jpg`; correction timestamps also showed October 3.

### B2 [P2] — Leaving a tasting silently loses unsaved scores and notes

- **Impact:** a cook can lose observations while briefly checking the formulation.
- **Reproduce:** open a preserved version's Batches & tastings page, enter
  Scoopability `9` and notes, then click **← Version details** before saving.
  Return through **Make a batch or add a tasting**.
- **Actual:** navigation proceeds with no confirmation. Both fields are empty on
  return; no evaluation was saved. Playwright observed zero dialog events.
- **Expected:** warn before leaving a dirty journal, or retain its unfinished
  input. The recipe editor's equivalent warning already works.
- **Location:** `src/IngaCookBook.UI/Features/Notebook/Pages/BatchJournal.razor:7`
  (back link), with the unsaved evaluation fields and save action in the same page.
- **Evidence:** `unsaved-tasting-before.png` and `unsaved-tasting-after.png`.

## Usability improvements

### I1 — Explain invalid scores instead of silently changing them

Entering `11` for Scoopability and leaving the field clamps it to `10`; saving
records a perfect score without a validation message. Prefer a visible 1–10
validation message, especially for accidental extra digits on a phone.

### I2 — Allow an audited correction to a tasting

After the above accidental score was saved, the journal only offered another
tasting. There was no visible edit/correct action on the recorded evaluation.
Provide a correction flow with a reason, analogous to recipe corrections, so an
input mistake does not remain part of future comparisons.

### I3 — Avoid padded decimal quantities during ordinary entry

In the initial editor flow, typing `550` changed the value to `550.000000` on blur.
Display six-place precision when present without padding every whole-number
ingredient quantity; the saved detail view already renders `550 g` clearly.

### I4 — Keep historical corrections distinct from new experiment guidance

V2 already differed from V1 by its cream quantity. Correcting only V2's copied
yield, with a correction reason, was rejected until another explanation was
entered into “How do these changes belong together?” The guide counts the full
V1 → V2 difference, so it treats this one-field correction as a two-change
experiment. Consider using the correction reason alone here, or make the second
explanation's purpose clear. Evidence: `correction-extra-explanation.png`.

## Evidence

Local screenshots live under ignored `TestResults/exploration-ff81f23/`:

- `compare-tastings.jpg`: selected tasting scores, notes, and ingredient diff.
- `history-dates.jpg`: local batch/tasting dates alongside next-day version dates.
- `branches.png`, `multiple-changes.png`: sibling history and focused-experiment guidance.
- `correction-extra-explanation.png`: correction blocked by experiment guidance.
- `photos.png`: two retained uploads and invalid-image feedback.
- `nested-print.png`, `affogato.pdf`: nested formulation in print media.
- `mobile-history.png`, `mobile-comparison.png`, `mobile-editor.png`,
  `mobile-dark.png`, `library-light.png`: responsive and theme evidence.
- `stale-tab-conflict.png`: rejected stale save with retained input.
- `unsaved-tasting-before.png`, `unsaved-tasting-after.png`: lost unsaved evaluation.
- `theme-light-recheck.png`, `theme-light-reloaded.png`: settled navigation colors
  after switching dark → light and reloading.

These are local, ignored artifacts, not committed fixtures. The synthetic QA
workspace is retained for reproducing findings; no existing user's recipes were
edited. The temporary standalone browser and the Aspire run were stopped.

## Limits and documentation review

Direct automation fills did not reliably update Fluent-bound values. Subsequent
entry used normal keyboard events and blur; this tooling limitation is not logged
as an application defect. The embedded browser then stalled on a JavaScript
confirmation dialog; standalone Playwright completed the later checks, including
successful dismissal of the same recipe-navigation prompt. No automated pass count
is claimed for this exploration, and the previously reported 475 tests were not
rerun for this documentation-only change.

The first light-theme library screenshot caught a CSS transition with dark link
backgrounds and light-theme text. A fresh reproduction checked the settled state:
navigation text was `rgb(100, 93, 84)` on `rgb(240, 240, 240)`. Reload also rendered
normally. This was not logged as a persistent theme bug. Two initial probe failures
used Fluent's transient `body[data-theme]` attribute; the corrected probe used the
app's `html[data-appearance]` marker and waited for the rendered layout.

This was Chromium desktop and phone-sized viewport testing, not physical touch
devices, Safari/Firefox, a screen reader, production Azure, offline operation,
load testing, or an explicit Server/WebAssembly transition test. Tiny synthetic
PNG uploads verify transfer/decoding, not realistic camera-image appearance or
large-file performance. Native paper printing and broad security testing remain
outside this pass.

Reviewed AGENTS.md, README.md, build/README.md, tests/README.md, the product guide,
and the existing validation document. Existing setup and feature guidance remains
accurate; this exploratory log is linked from the validation document. No product
behavior or installed skills were changed.
