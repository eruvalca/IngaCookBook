# Recipe notebook

IngaCookBook is a private recipe development notebook for any food. It helps a
cook remember exactly what went into an attempt, how it was made, what the
results were, and what to try next. Ice cream has an optional starter set of
evaluation criteria; no workflow depends on ice cream-specific calculations.

## Making an experiment

1. Register, sign in, and name your workspace. Choose a currency for optional costs.
2. Create a recipe with a name and an optional description. Evaluation criteria
   start with overall satisfaction and can be expanded or edited before creating
   it or in recipe settings later. The ice cream preset adds missing criteria
   without replacing custom ones. An empty name is identified and focused in place.
   Scores use 1–10, with 10
   representing the best result. Every score has an optional notes field.
   Recipe creation and settings warn before leaving unsaved names, descriptions,
   or criteria. Canceling navigation retains the form; rejected saves retain the
   warning, and a successful save clears it.
3. Write the first version's ingredients, amounts, units, and ordered preparation
   steps. Save explicitly. The editor shows unsaved changes and warns before leaving,
   including version/sidebar links and browser Back/Forward. Canceling keeps the
   current editor and its inputs; confirming discards the unsaved changes.
   Inputs lock while a save and its reload are in progress, then unlock so later
   edits cannot be silently replaced by that reload. Rejected saves retain inputs.
4. Record a batch when you make it, including what actually happened during
   preparation. This preserves the recipe version. Batch and tasting dates default
   to the browser's local calendar date in both Server and WebAssembly rendering.
   Date-dependent forms stay disabled until that default is ready; manually chosen
   dates survive saves and reloads of the journal's data.
5. Switch to **Add a tasting** for that batch: tap a 1–10 choice, optionally expand
   notes, and record overall observations and one idea
   to try next. Evaluate the same batch later or make another unchanged batch.
   Invalid scores stay visible with a 1–10 whole-number validation message; they
   are never silently clamped. The journal warns before leaving unsaved batch or
   tasting inputs, including links, Back/Forward, and document reloads. Saving one
   form does not discard or clear protection for unfinished input in the other.
   Switching journal tasks retains both forms. New journal visits prefer the latest
   batch without a tasting, falling back to the latest batch. The batch selector
   always allows another choice. A saved batch offers **Taste now** or **Later**;
   if another tasting is unfinished, it offers to resume that tasting instead.
   A saved tasting shows the batch, date, scores, notes, and next idea immediately.
   **Try this as a variation** creates an inherited draft with that idea as an
   editable, unsaved question. It still requires an explicit save.
6. Select a winner as the current standard. It can still be the starting point
   for variations.
7. Try a variation from any version. Pick an optional metric to improve and write
   the question you are testing. The editor shows differences as you work.

The app guides rather than imposes a one-change limit. When more than one recipe
row changes, it warns and requires an explanation of how the changes belong to
one experiment. A sugar replacement may legitimately change two ingredients.
Separate variations can test separate ideas. Changes are counted by ingredient
row, step content/order, and yield; this is a practical prompt, not a scientific
claim of causation. Purchase prices do not count as experiment variables.
Corrections to preserved versions compare against that version's saved content
and require only their correction reason, not another experiment explanation.

The editor puts ingredients and preparation first, with optional costs, linked
recipes, step notes, and version notes in disclosures. Its experiment coach sits
alongside the recipe on large screens and below it on phones, expanding when
several changes need an explanation. A compact link jumps to the plan on the
current page, keeping unfinished inputs and any tasting-idea context. The save
bar reserves space for keyboard focus even when its actions wrap.

**Kitchen view** presents the exact saved recipe, including recipe-level notes,
with large ingredient and step checkmarks. Checkmarks belong only to the current
open page and can be reset; they never change the recipe or record a batch
automatically. **Record this batch** opens the regular batch form.

## Understanding history

Use **Delete draft** under **Draft options** in the editor or **More recipe actions**
in version details to abandon an
unused draft. **Keep draft** cancels; **Delete permanently** confirms removal,
including pending editor changes and attached photos. Deleting the only version
also removes the recipe from the library. Otherwise, the remaining history stays
intact and the app returns to it. Preserved versions and versions used as an
origin cannot be deleted. Deleting a variation does not unlock its preserved
starting version. Stale tabs must reload before deleting.

Each recipe has a timeline, a branching view, and comparison between any two
versions. Compare ingredients, preparation, experiment notes, photos, and scores
with their notes and deltas. Choose specific tastings on either side; the latest
dated tasting is the default, with recording time breaking ties. Blank means
unassessed. The cook chooses the standard; scores do not automatically pick it.
History summarizes changes and scored results, with a green current-standard
section. Comparison leads with a concise overview, retains exact changes and
selected tasting identities, and groups unassessed criteria and optional context
in disclosures. Score differences do not imply that a particular edit caused them.

Signed-in Home opens the library. Search and filters find drafts, standards, and
recipes awaiting a tasting. The newest created draft has a resume action; recipe
cards offer the relevant next action. Recipes without photos use a compact text
cover rather than an empty image area.

Versions become preserved when used for a batch, selected as a standard, or used
as a variation's starting point. Corrections require a reason and retain the
previous content in a visible correction history. Making a separate recipe also
preserves its origin atomically with creating the copy. A variation can become a
separate recipe with its own future history and a link to its origin. Its prior
batches and photos stay with the original recipe.

Use **Correct tasting** on a recorded evaluation to fix a mistaken date, score,
or observation. A reason is required. The original tasting identity and recording
order stay intact; each correction retains the prior date, scores, metric notes,
observations, and next idea. Both the journal and version details show this audit.
New tasting sessions should be recorded as separate evaluations.

Use **Correct batch** to fix its date or preparation notes, with a required reason.
The journal and version details retain the prior date and notes for every
correction. A corrected date cannot be later than an existing tasting; correct a
mistaken tasting date first if necessary. Batch identity, its tastings, and its
number stay unchanged. Batches are numbered in recording order; the migration
preserves the previous chronological order for existing batches. Correcting a
batch also preserves any unfinished tasting in the journal.

History, standard selections, and correction timestamps display in the browser's
local timezone. Static HTML supplies an explicitly labeled UTC fallback until
JavaScript localizes it; date-only batch and tasting entries are not shifted.

Criteria belong to the recipe. Adding one leaves earlier evaluations blank.
Removing one requires acknowledgement and deletes that criterion's historical
scores and notes across the recipe, including tasting correction snapshots.
General observations remain. Past standard
selections remain visible too.

## Ingredients, costs, photos, and printing

- Ingredient names are free text, including brands and preparation details.
  Units include mass, explicitly US customary volumes, metric volumes, and counts.
  Quantity inputs support six decimal places without padding whole numbers or
  adding trailing zeros; comparisons display saved precision.
- Another recipe's preserved version can be an ingredient. A saved snapshot
  prevents later corrections to the source from silently changing this recipe.
  Nested recipes are limited to eight levels.
- Purchase quantity, unit, and price are optional. Costs are proportional within
  compatible units; mass-to-volume conversion is never guessed. Unpriced
  ingredients make the estimate incomplete. Linked recipes need a compatible
  yield and complete cost information. Fractional cents remain precise through
  nested recipes; rounding happens only in the displayed estimate. Estimates
  exclude labor and overhead.
- JPEG, PNG, and WebP photos attach to a version, up to 10 MiB each. Multiple
  files upload individually; a partial failure reports how many were saved.
  Bytes live in private Azure Blob Storage. Authenticated routes check workspace
  access before serving them.
  Storage outages show an error while preserving earlier successful uploads.
  Unconfirmed uploads and failed compensating deletes are queued for cleanup.
  Upload progress identifies the current file and saved count. Recipe settings
  provide photo previews, editable captions, and a library cover choice from that
  recipe's photos. Automatic cover selection prefers a photo from the standard,
  then the first available photo. Deleting a draft that supplied the cover clears
  the choice. Photos retain their original version ownership. **Add photos** jumps
  to the current version's uploader. **Choose a cover or edit captions** opens the
  photo section in settings, with an explanation when no photos are available.
- A print view includes the recipe and expands nested formulations. Browser
  printing supports paper or PDF and hides navigation and editing controls.

## Implementation

Library, history, comparisons, printing, workspace setup, and the shell use
static SSR. Editors, the kitchen checklist, and action/upload areas opt into InteractiveAuto. A shared
`INotebookService` has a scoped server implementation and a WebAssembly HTTP
adapter. Identity remains static SSR. Fluent UI v5 supplies controls, layout,
icons, and theme tokens. Appearance follows the system or an explicit light/dark
preference saved in the browser. The visual theme takes its coral pink and
leaf green from Inga's Frozen Desserts logo, with ivory surfaces in light mode
and warm dark surfaces at night. Long forms scroll with the document.

Recipe creation, settings, the editor, and the journal reuse collocated JavaScript
to protect enhanced links and history traversal;
`NavigationLock` handles programmatic .NET navigation. The shared JS initializer
indexes history entries while preserving Blazor's state so a canceled Back/Forward
action can restore the original entry without adding another one. Form listeners
are removed when their DOM is replaced. Full document exits use the browser's native
unsaved-changes prompt.

ASP.NET Core Identity supplies authentication. Each owner initially has one
workspace. Recipe reads, writes, nested references, and photos check that owner
and workspace. Explicit workspace IDs provide the boundary for future members;
sharing, invitations, and role management are absent from this release.

Deleting a draft removes its version records and durably queues cleanup of that
version's photo prefix in the same database transaction. Photos become
inaccessible immediately; the existing background worker retries blob removal.
Deleting the account removes its recipe records and durably queues its photo
removal in the same database transaction. Photos become inaccessible through the
app immediately; a background worker retries Azure cleanup until it succeeds.
Other workspaces are unaffected. Azure-retained versions and backup copies follow
the storage operator's retention policy; see [storage setup](../README.md#recipe-photos-and-azure-storage).

EF Core uses relational entities, foreign keys, unique indexes, score/amount
constraints, application-assigned GUIDs, and optimistic concurrency on recipes.
Operations create and dispose contexts through `IDbContextFactory`. Read queries
use no tracking and collection graphs use split queries. JSONB is restricted to
saved nested formulations and correction snapshots. Stale writes return a conflict
without overwriting newer work; rejected saves retain the current editor inputs.
The library currently loads the owner's collection in one request; pagination
and summary projections are future scaling work.

Authenticated minimal APIs live under `/api/notebook`. They use transport records,
antiforgery validation on mutations, server-side business rules, and no-store
responses. Expected failures return `ChangeRejected` with an HTTP status;
successful writes return `ChangeSaved`. New metric identities are assigned by the
server. Reload after writes to get the canonical metric IDs and new revision.
Development OpenAPI is at `/openapi/v1.json`; see the [HTTP examples](../src/IngaCookBook/Notebook.http).

## Installing the notebook

Open **Install app** below Appearance in the navigation to add Inga’s to a home
screen or desktop. Supported browsers offer an install button; other browsers
show menu instructions. The Kitchen Notebook icon identifies the installed app.
It opens the recipe library, asking for sign-in when necessary. Installation
still requires an internet connection to use recipes and save changes.

When a new release is detected, a notice offers **Refresh app** or **Later**.
Finish and save an unfinished recipe or tasting before refreshing. The app never
refreshes automatically; its existing unsaved-input confirmation also applies to
this refresh action. Choosing Later leaves the current work in place.
If a server restart ends the current session, choose **Review my inputs** to copy
unfinished notes before refreshing. That session can no longer save changes.
The **Refresh app** notice stays at the top while you review your inputs. Once
you have copied what you need, use it to reconnect; canceling the unsaved-changes
confirmation keeps your inputs and the notice available.
See [installation setup](../README.md#installation-and-app-updates) for hosting,
browser support, icons, and update behavior.

## Release boundaries

No offline mode, sharing, imports, inventory, nutrition, quantity scaling, or
ice cream formulation calculator is included. Saving is explicit. No automatic
conclusion is made about which change caused an improvement. Account email
delivery and cloud infrastructure need configuration before public deployment;
the checked-in setup uses the existing development confirmation flow and Azurite.
No deployment or Azure subscription resources are created by these changes.
