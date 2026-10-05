# IngaCookBook

.NET 10 Blazor application with a hosted WebAssembly client, shared UI and kernel,
Aspire orchestration, PostgreSQL-backed ASP.NET Core Identity, and a recipe
development notebook. Record ingredients and preparation, make batches, evaluate
results, and compare experiments. See the [product guide](docs/recipe-notebook.md).

## Local prerequisites

- .NET SDK **10.0.401** (selected by `global.json`).
- Aspire CLI **13.6.0** on `PATH`, matching the AppHost SDK and stable integrations.
  Follow the [Aspire installation guide](https://aspire.dev/get-started/install/).
- Docker Desktop running Linux containers, or another Aspire-supported container runtime.
- A trusted .NET development HTTPS certificate: `dotnet dev-certs https --trust`.
- Optional VS Code with this repository's recommended C# and Aspire extensions.
  The editor opens `IngaCookBook.slnx` by default.

Run `dotnet --version`, `aspire --version`, and `aspire doctor` from the repository
root to verify the environment. The AppHost is explicitly located by the root
`aspire.config.json`.

## First run and application identity

Use PowerShell 7 for the scripts in this repository. From the solution root,
run `dotnet build IngaCookBook.slnx`, then `aspire run`. The first build/start may
restore NuGet packages, download Aspire/EF tooling, and pull container images.
Migrations create the Identity and recipe notebook schemas; no accounts, recipes,
or local credentials are included. Register an account to sign in immediately,
then create a workspace at `/workspace`. The default `Email:Provider=None` requires
no email confirmation. For optional local email testing, see [account email](#account-email).
Git initialization is optional and separate.

If this solution was generated, `.template-provenance.json` records its template
version and source commit. It is an independent snapshot: template updates do not
update this application. Review SDK/package/skill updates in this repository.

Use a unique application name for projects you run side by side. Different ports
do not isolate browser cookies on the same hostname. A generated secrets ID is
unique even when the application name is reused, but the development hostname is
name-based. Aspire's default data-volume name depends on the AppHost path; moving
or renaming a checkout can select a different volume. Keep the original secrets
and volume together if preserving development data.

## Run through Aspire

Aspire is the default entry point for running and debugging the application. It
starts PostgreSQL and Azurite, applies migrations, supplies configuration, and
starts the web project. The local Mailpit inbox starts only when you explicitly
select `Email:Provider=Mailpit`. The hosted WebAssembly client runs through the web
project.

For interactive development, run from the repository root:

```powershell
aspire run
```

The CLI builds the AppHost and its projects before starting them. For a background
run (including agent validation), use:

```powershell
aspire start --launch-profile https --non-interactive
aspire wait ingacookbook --timeout 120 --non-interactive
aspire describe --non-interactive
```

`aspire start` prints the dashboard login URL. The dashboard and `aspire describe`
show the current application endpoints. Open the HTTPS endpoint for
`ingacookbook.dev.localhost`; all ports are allocated dynamically and can change on
restart. Chromium browsers resolve `.localhost` names locally. Command-line
clients that do not resolve subdomains can use the internal `https://localhost`
endpoint shown by Aspire. Do not copy ports or dashboard login tokens into source.

The background command explicitly selects `https`; it is also the AppHost's first
and default launch profile. `AddProject` selects the
matching web profile and derives its endpoints from `applicationUrl`; there is no
AppHost override for the web profile or its ports. The web profiles retain
`ingacookbook.dev.localhost:0`: the hostname is our local convention, and port `0` asks
Aspire to allocate an available port instead of using the template's fixed ports.
The other web launch settings preserve the template's environment and Blazor debugging.

The AppHost's `https` profile omits dashboard, OTLP, and resource-service addresses:
Aspire 13.6.0 supplies secure, dynamically allocated local endpoints. The optional
AppHost `http` profile sets `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, which opts into
HTTP endpoints with allocated ports. This opt-in applies only when that profile
is selected; the default `https` profile keeps Aspire's secure defaults.

Use the web resource's **Rebuild** command to apply compiled application changes.
For AppHost changes or a full solution build, stop first to release Windows file locks:

```powershell
aspire stop --non-interactive
dotnet build IngaCookBook.slnx
aspire start --launch-profile https --non-interactive
```

Stop the application with `aspire stop --non-interactive` (or Ctrl+C for a foreground
run). PostgreSQL and any started pgAdmin container stop with Aspire. The database
volume survives. Aspire retains its generated local PostgreSQL password in AppHost
user secrets; preserve those secrets along with the volume when reusing local data.
No database password or connection string needs to be committed.

## Debug through Aspire

In VS Code, install the recommended Aspire and C# extensions and open the repository
root. Select **Aspire: IngaCookBook** in Run and Debug, then press **F5**. The checked-in
`.vscode/launch.json` uses the Aspire debugger and discovers the AppHost through
`aspire.config.json`; no fixed ports or connection strings are needed. The extension
starts the resource graph and attaches the .NET debugger to the web project.
Use **Ctrl+F5** with the same configuration to run without debugging.

Stop any CLI-started instance with `aspire stop --non-interactive` before starting
an IDE debug session. Use the IDE's **Stop Debugging** action to end its session.
Open the dashboard through **Aspire: Open Dashboard** or the Aspire view; editor
settings control whether it opens automatically. This workspace sets
`aspire.dashboardBrowser` to `openExternalBrowser`, so the dashboard opens in the
configured external browser for both F5 and Ctrl+F5. This overrides a user-level
`debugEdge`/`debugChrome` preference; C# application debugging remains enabled with F5.

If VS Code reports **Unable to launch browser: "Unable to attach to browser"**,
check **Output → Aspire Extension**. An entry such as **Failed to start debug
browser (pwa-msedge), falling back to default browser** identifies the dashboard's
browser-debugger session. That session is separate from the AppHost and web C#
debuggers and is not needed to use the dashboard. Keep the workspace's
`openExternalBrowser` setting and remove any `dashboardBrowser: "debugEdge"` or
`"debugChrome"` override in the selected `launch.json` configuration, which takes
precedence over workspace settings. Then stop and restart the Aspire session.

Set server breakpoints in the web project's code-behind, such as the registration
handler in `src/IngaCookBook/Features/Account/Pages/Register.razor.cs`, then exercise the
page through the HTTPS endpoint shown by Aspire. Inspect variables and the call
stack in the debugger, and use Aspire's logs and traces for the corresponding
HTTP and database activity. A breakpoint can temporarily interrupt health checks;
resume execution before diagnosing the paused resource as unhealthy.

In Visual Studio, set **IngaCookBook.AppHost** as the startup project, select its
**https** profile, and use F5 (debug) or Ctrl+F5 (run). Launching `IngaCookBook` or
`IngaCookBook.Client` alone does not start the database/migration dependency graph.

The existing unit and component tests remain headless and can run/debug through
Test Explorer without Aspire.

See the [Aspire VS Code extension guide](https://aspire.dev/get-started/aspire-vscode-extension/).

## Fluent UI Blazor and styling

`Microsoft.FluentUI.AspNetCore.Components` **5.0.0** is centrally pinned in
`Directory.Packages.props` and referenced by the server, client, and shared UI.
Both host `Program.cs` files call `AddFluentUIComponents()` so prerendering,
Interactive Server, and Interactive WebAssembly resolve the same services.
The shared kernel has no UI dependency. Fluent UI Blazor v5+ is the application's
layout, styling, and component framework. Use its components and design tokens
for new UI; do not introduce a parallel CSS framework or default to custom Grid/Flexbox.

`Microsoft.FluentUI.AspNetCore.Components.Icons` **5.0.0** is referenced by the
shared UI and flows to both hosts. Navigation uses `IconRest`/`IconActive` with
regular/filled system icons; the counter uses `IconStart`. Use strongly typed
instances such as `new Icons.Regular.Size20.Home()` (the Razor imports define the
`Icons` alias) so publishing can retain referenced icons. Keep meaningful visible
labels; redundant icons are decorative and not extra tab stops. No icon font,
CDN, script, service registration, or separate emoji package is needed.

`App.razor` loads the Fluent baseline stylesheet, `IngaCookBook.styles.css`, then
`app.css`. The generated solution stylesheet already imports the Fluent component
CSS bundle; do not add a second link to that bundle. `no-fuib-style` on the body
prevents Fluent's initializer from adopting another copy of the baseline after
the application overrides. The package's Blazor JavaScript initializer loads the
web components automatically, including static layout hamburger behavior. Do not
add a v4 web-components script or `FluentDesignTheme`. Appearance defaults to the
system preference and offers explicit light and dark modes. A small head script
selects the initial mode before painting; the shared initializer applies the
pinned v5 brand palette and updates pickers after enhanced navigation. Application
styles use v5 CSS design tokens. The logo supplies coral pink (`#E4786D`) and
leaf-green accents, with ivory neutrals in light mode. Mode-specific token
overrides keep text and primary actions legible; green also identifies a standard.

The shell uses `FluentLayout`, `FluentNav`, and `FluentNavItem`; the home, counter,
and authenticated pages use Fluent cards and buttons with native content links.
Native anchors preserve accessible link names and navigation before JavaScript
initializes. Account management
has Fluent navigation and a card around its content. `FluentGrid`/`FluentGridItem`
provide responsive page columns and `FluentStack` provides content spacing, with
a mobile drawer below the Fluent layout's 768px breakpoint. Keep grid adaptive
rendering off for static SSR; responsive CSS does not need a .NET event handler.

Render boundaries are deliberate:

- `Routes`, `MainLayout`, Home, the authenticated summary, and Identity pages
  use static SSR. `Counter` opts into `InteractiveAuto` because its button needs
  .NET event handling. Counter actions are disabled during prerendering until
  their renderer becomes interactive. Static pages don't start a .NET interactive
  runtime simply to display navigation or cards.
- `Counter` hosts `FluentProviders` inside its interactive boundary. Providers
  inherit its renderer and scoped services for dialogs, toasts, tooltips, and key
  handling. Add providers inside other interactive pages/subtrees when needed,
  once per active renderer/service scope. The layout's `Body` never crosses an
  interactive boundary.
- Static navigation has ordinary `Href` links with `tabindex="0"`, since Fluent's
  interactive roving-tabindex setup does not run in SSR. Shell links use Blazor's
  enhanced navigation: the server renders the destination and Blazor updates the
  existing document without restarting the page or reloading the runtime.
  `Components/App.razor.js` indexes history before calling `Blazor.start()`;
  `blazor.web.js` therefore has `autostart="false"`. Preserve this ordering:
  enhanced navigation is available before Blazor's JS initializers finish, so
  indexing only in an initializer can miss a quick first click and bypass the
  unsaved-changes prompt on Back.
  `IngaCookBook.UI.lib.module.js` registers the collocated `MainLayout.razor.js` handler
  through `afterWebStarted`. It closes the Fluent mobile drawer on
  `enhancednavigationstart`, before the DOM update, including back/forward
  navigation. Stable layout/hamburger IDs preserve their JS wiring across updates.
  The same initializer handles **Skip to content** by focusing the current main
  region without navigation, including after interactive routing. Its SSR URL
  remains a working fallback. Section links must include their page path (and
  any current query string); fragment-only URLs resolve against `<base href="/">`.
  The shell grows with its content and uses document scrolling, allowing Blazor
  to reset scroll position on navigation and restore it through browser history.
  `app.css` explicitly resets the Fluent baseline's body height/overflow. Check
  actual wheel scrolling on a long form; automation can focus or scroll an input
  into view even when ordinary scrolling is blocked.
  Both navigation menus explicitly use `colorNeutralBackground2` for their surface
  and `colorBrandBackground2` for hover, so account settings follow the same
  light/dark/system palette as the main navigation instead of Fluent's defaults.
  The signed-in link is labeled **My account** so long email addresses do not
  wrap in the sidebar. Fluent's vertical bar and `aria-current="page"` identify
  the active link; the temporary hover background does not indicate selection.
  The account settings menu does not use interactive categories or event callbacks.
- Identity forms retain native inputs, submit buttons, form names, antiforgery,
  and passkey hooks. They receive Fluent token styling while preserving static
  POST mapping, autofill, and cookie redirects. These POSTs keep normal navigation
  so cookie changes refresh authentication state. Shared notices retain their
  `notice`/`data-kind` contract. The reconnect/error UI retains native controls
  because it must function while the Blazor circuit is unavailable.

Shared styles live in `src/IngaCookBook/wwwroot/app.css`: sizing, typography, forms
(`account-form`, `form-field`, `checkbox-field`), action groups (`actions`),
notices (`notice` with a semantic `data-kind`), and table overflow
(`table-container`). Native button styling is scoped to Identity/native forms
and `native-button`; do not apply borders or padding to every `button`, because
Fluent dropdowns have light-DOM control buttons. `control-row` aligns labeled
Fluent fields with adjacent actions, accounting for field margins/message spacing.
Component-specific styles belong in adjacent `.razor.css`
files. Use narrowly scoped `::deep` selectors for child component markup.

Custom CSS is limited to application sizing, accessibility, and static form
compatibility, using Fluent tokens. Prefer Fluent layout/spacing parameters;
the library's generated styles are expected. Use normal flow for prose and
semantic tables. Do not recreate Bootstrap utilities or add `!important` or
ornamental animations. Use icons to clarify navigation/actions. Keep form
labels before their controls and preserve Blazor/Identity behavior hooks when
editing markup. Check narrow screens, keyboard focus, and text wrapping when
changing layouts. See `AGENTS.md` for the authoring conventions.

Setup references: [installation](https://www.fluentui-blazor.net/installation),
[layout](https://www.fluentui-blazor.net/layout), and the
[released package source](https://github.com/microsoft/fluentui-blazor/tree/358e449b08711f3343ce6c2ffbe6c9f77045bde5).
Navigation references: [enhanced navigation](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0#enhanced-navigation-and-form-handling)
and [JavaScript with static SSR](https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/static-server-rendering?view=aspnetcore-10.0).

## Installation and app updates

The online-only PWA uses `wwwroot/manifest.webmanifest` with a stable `/` identity,
`/recipes` launch URL, and standalone display. Sign-in redirects still use the
existing static SSR Identity flow. A stable HTTPS origin reachable from the
device is required for normal installation; a phone cannot use the development
machine's localhost address. Installation does not package the server or database.

The navigation's **Install app** disclosure offers a browser install button when
`beforeinstallprompt` is available, with browser-menu guidance otherwise. On iPhone
and iPad, use Safari's Share → Add to Home Screen. Installed standalone windows hide
the install controls. Browser chrome follows the selected light/dark appearance
where supported. The Kitchen Notebook icon source is
`assets/branding/kitchen-notebook.png`; on Windows, run
`pwsh ./scripts/Export-AppIcons.ps1` to reproduce the checked-in favicon, Apple
touch icon, and 192/512 px regular/maskable PNGs. Preserve the source's generous
padding so the notebook fits circular masks.

No application service worker or offline cache is registered. Existing .NET
framework resource caching remains framework-owned; recipe data, account pages,
and photos continue to require the server. Do not add the standalone Blazor PWA
template's `index.html` navigation fallback to this SSR application.

`ApplicationRelease` computes a startup fingerprint from server/UI/shared assembly
identities and the SDK's `IngaCookBook.staticwebassets.endpoints.json` content. The
SDK manifest must accompany the deployed application so asset-only changes are
included; assembly identities provide a fallback when the manifest is absent.
The anonymous, no-store `/app-version` endpoint exposes only this opaque identity.
Browser checks are bounded and throttled to five minutes, while visible, including
after enhanced navigation or returning to the app. An update notice offers
**Refresh app** or **Later**. It never forces a reload; explicit refresh keeps the
notebook's normal unsaved-input confirmation. Later dismisses that release for
the current document session, and a subsequent release can show a new notice.
This improves client refresh behavior, but does not provide server deployment
draining or preserve an Interactive Server circuit across a server restart.
If reconnection/resumption is rejected, the connection dialog also requires an
explicit refresh and offers **Review my inputs** so unsaved notes can be copied.
A persistent **Refresh app** notice remains available while reviewing inputs,
including after dismissing the dialog with Escape and when the release is unchanged.
This keeps the visible page intact; it cannot recover lost server-side state.

## Resource graph and database

```text
postgres (PostgreSQL 18.3, managed data volume)
  ├─ ingacookbookdb (physical database: ingacookbook)
  │    └─ ingacookbook-migrations (EF database update)
  │         └─ ingacookbook (Blazor server + hosted WebAssembly)
  └─ pgadmin (explicit start)
photostorage (Azurite in local run mode, managed data volume)
  └─ recipephotos (private blobs consumed by ingacookbook)
mailpit (optional Email:Provider=Mailpit, disposable SMTP capture + inbox UI)
  └─ ingacookbook (waits for inbox readiness)
```

After a normal startup, these dashboard states are expected:

| Resource | Expected state | Meaning |
| --- | --- | --- |
| `postgres`, `ingacookbookdb` | Running / Healthy | PostgreSQL and the application database are ready. |
| `photostorage`, `recipephotos` | Running / Healthy | Azure Blob-compatible photo storage is available locally. |
| `mailpit` | Absent by default | With `Email:Provider=Mailpit`, Running / Healthy means the optional local email inbox is ready. |
| `ingacookbook-migrations` | Finished | The one-shot migration command completed successfully. It is not a long-running service. |
| `ingacookbook` | Running / Healthy | The web application is ready and its database readiness check passes. |
| `pgadmin` | Not started | Optional database UI; start it when needed. |

With hidden resources displayed, `ingacookbook-rebuilder` can also be **Not started**
until the web project's Rebuild command is used, and the EF tool resource can be
**Finished**. These helper states do not indicate an application startup failure.
An **Exited**, **Failed**, or **Unhealthy** application/database resource, or web
startup blocked by failed migrations, does require investigation through its logs.

The PostgreSQL image version is the built-in default of
`Aspire.Hosting.PostgreSQL` 13.6.0. Container lifetime remains Aspire's default
session lifetime. Data uses an Aspire-managed named volume rather than a repository
file. This is a fresh PostgreSQL schema; the original SQLite scaffold is removed.

Aspire supplies `ConnectionStrings:ingacookbookdb` to both the application and migration
tool. The application uses Npgsql EF Core 10.0.3, EF Core 10.0.12, and Aspire's
Npgsql EF integration 13.6.0. Its non-pooled `IDbContextFactory<ApplicationDbContext>`
supports a context per Blazor operation; Identity can still resolve the scoped
context. Dispose factory-created contexts with `await using`.

## Recipe photos and Azure Storage

The AppHost models `photostorage` with `Aspire.Hosting.Azure.Storage` 13.6.0 and
`recipephotos` as its blob service. Run mode uses Azurite with a managed data
volume and session lifetime. Aspire supplies `ConnectionStrings:recipephotos`;
the application registers `BlobServiceClient` with the matching Aspire client
integration. Photos persist across ordinary local restarts. Tests remove volume
mounts from all resources, including the storage emulator, before starting.

The app creates the private `recipe-photos` container on its first upload. Keys
contain workspace, recipe, version, and photo GUIDs; filenames are display
captions only. Authenticated minimal API routes stream the bytes after checking
ownership. No public container access, browser credentials, or permanent SAS
links are used. JPEG/PNG/WebP signatures and the 10 MiB per-file limit are checked
server-side. A rejected metadata save compensates by deleting only that upload.
Expected Azure failures return a recoverable save error. If Azure's upload result
is uncertain or immediate compensation fails, that unique blob key is queued for
cleanup without affecting earlier successful uploads.

Account deletion commits the Identity/database cascade and a `PhotoCleanupJobs`
entry in one EF transaction, using the configured execution strategy. The job
survives deletion of its workspace. The hosted cleanup worker processes up to 20
due jobs at startup and once per minute, deleting blobs and snapshots only under
each queued prefix. Storage failures defer the job for another minute; transient
database failures leave jobs queued. Successful jobs are removed. Inspect worker
warning events 2100/2101 for deferred cleanup. Account deletion does not wait for
Azure; photos become inaccessible through the app immediately and bytes are
removed when storage is available. `DurablePhotoCleanup` adds the queue table
through the existing migration startup dependency.

The production AppHost grants the application identity blob data access, including
listing/deleting blobs and creating the container. Shared-key authentication and
anonymous blob access are disabled. The emulator remains local only.
Database and blob backups must be retained together. Azure retention/versioning
and backup policies govern retained copies separately from application deletion.
This first release has no individual photo-deletion UI or general orphan scan;
unexpected failures that prevent recording a cleanup job can still require
operational cleanup.

Identity schema version 3 is retained, including the `AspNetUserPasskeys` table.
Registration signs users in immediately by default, without sending email. Email
addresses remain unverified login identifiers. External-login providers remain
optional and need their own credentials. The production setup below uses the
existing password/passkey/owner-recovery workflow.

## Azure deployment

The production configuration is implemented in `IngaCookBook.AppHost/Deployment`.
It has been validated locally; Azure provisioning and GitHub bootstrap still require
the first live run. `aspire deploy` owns infrastructure and application deployment;
there is no `azure.yaml`, separately maintained Bicep, or deployed AppHost process.
The selected target is the default Pay-As-You-Go subscription, **Central US**, and
the dedicated **`rg-ingacookbook-prod`** resource group. Published artifacts are
previews, not inputs to `aspire deploy`.

| Resource | Initial configuration |
| --- | --- |
| Container Apps | Standard managed environment with Consumption workload profile; `ingacookbook` has one 0.5-vCPU / 1-GiB replica, single revision mode and sticky sessions. |
| PostgreSQL Flexible Server | PostgreSQL 16 (the pinned Azure integration default), Standard_B1ms, 32 GiB, seven-day backups, no HA/geo-redundancy. Local development stays on PostgreSQL 18.3. |
| Photos | Standard LRS blob storage, managed-identity access, no anonymous/shared-key access, seven-day blob/container soft deletion. |
| Registry | Basic ACR, identity-based image pulls. |
| Secrets | Key Vault holds database connection strings; a stable database password comes from the GitHub production environment. Database administrator name is `cookbookadmin`. |
| Telemetry | Managed Aspire dashboard; Log Analytics retains logs for 30 days, with a 0.1-GB/day ingestion cap. The cap can interrupt logging and is not an exact billing ceiling. |
| Budget | Resource-group budget of 75 in the subscription billing currency (the selected subscription uses USD), with actual-spend notifications at 50%, 80%, and 100%. This is not a spending cap. |

The generated HTTPS address is `ingacookbook.<environment-domain>.azurecontainerapps.io`.
The dashboard requires Azure authentication; it is not an anonymous admin page.
Use its URL from the Aspire deployment summary or the Container Apps environment
in the portal. Its live telemetry is not a durable tracing archive; Log Analytics
provides retained application/console logs. No email service, sender domain, Mailpit,
pgAdmin, or Azurite is deployed. Azure budget email is independent of application email.

PostgreSQL initially uses password authentication with Azure's `AllowAllAzureIps`
firewall rule and certificate/hostname-verified TLS (`SSL Mode=VerifyFull`). Unused
Kerberos negotiation is disabled in both connection secrets. This allows network access from other Azure
tenants too; authentication is still required. This is not a private-endpoint/VNet
deployment. Storage also has a public service endpoint with authenticated private
blobs. These choices avoid adding networking infrastructure for the initial small
deployment. Capacity and subscription quota are checked by the first deployment;
the earlier Central US SKU discovery did not reserve capacity.

### First-time setup

Prerequisites: .NET from `global.json`, Aspire CLI 13.6.0, Docker with Linux containers,
PowerShell 7, Azure CLI, and GitHub CLI. Sign in with `az login` and `gh auth login`.
The bootstrap operator needs subscription permission to register providers and
create the RG, manage role assignments/budgets, and repository admin access.
No Microsoft 365 subscription or organizational mailbox is required.

Review the non-mutating preview first:

```powershell
./scripts/Initialize-Deployment.ps1 -SubscriptionId <subscription-guid> -AlertEmail <owned-alert-inbox> -WhatIf
```

Then run the same command without `-WhatIf` after approving the target. The script:

1. Registers the required Azure resource providers and creates the selected RG.
2. Creates `ingacookbook-github`, a user-assigned identity with a federated credential
   for `repo:eruvalca/IngaCookBook:environment:production`. Its Contributor and Role
   Based Access Control Administrator roles are confined to this RG. The latter
   lets Aspire grant its application identities storage/Key Vault/registry access.
3. Creates the GitHub `production` environment restricted to the `main` branch.
   An existing environment with different branch rules is rejected for review;
   existing reviewer settings are not overwritten.
4. Creates/updates the monthly budget using the supplied private alert inbox.
5. Sets the production environment variables `AZURE_SUBSCRIPTION_ID`,
   `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_LOCATION`, and `AZURE_RESOURCE_GROUP`.
   Generates `POSTGRES_PASSWORD` once and sends it to GitHub via stdin; it is never
   printed. Reruns retain it. If the database exists but the secret is missing,
   restore the current credential instead of generating a replacement.
6. Enables the repository variable `DEPLOYMENT_ENABLED=true` only after setup
   succeeds. The script itself does not deploy application infrastructure.

Keep the selected inbox and Azure identifiers in deployment configuration, not
source. This script changes Azure/GitHub state; `-WhatIf` performs no CLI calls.
Do not rerun it merely to diagnose a deployment. Disable automatic deployment with
the repository variable `DEPLOYMENT_ENABLED=false` when needed.

### Automatic releases and migration safety

`.github/workflows/deploy.yml` validates pull requests and `main`: full solution
build, formatter verification, all five test projects, Bicep compilation/model
checks, and the real Linux migration bundle against disposable PostgreSQL 16.
The latter applies the bundle twice and checks Identity's passkey table.
Action dependencies are pinned to commit SHAs. Only TRX reports are uploaded;
deployment state, console dumps, secrets and generated infrastructure are not.

After successful validation, enabled `main` pushes (or manual workflow dispatch on
`main`) log into Azure through OIDC and run:

```powershell
aspire deploy --environment Production --non-interactive
```

The deploy job supplies `Azure__SubscriptionId`, `Azure__Location`,
`Azure__ResourceGroup`, `Azure__CredentialSource=AzureCli`, and the secret
`Parameters__postgres_password`. Keep the password unchanged between releases.
No Azure login client secret or deployment state cache is needed. Production
deployments are serialized; a newer push does not cancel an in-flight migration.
GitHub concurrency can replace an older pending run with a newer one.
After acquiring the deployment lock, the job reads the current `main` SHA from
GitHub and proceeds only if it matches the validated run's SHA. An older commit
that finishes validation later is skipped before Azure login, deployment, and
smoke checks. Failure to verify the current SHA stops the job. A new push during
an active deployment waits for that deployment to finish; rerunning an old
workflow cannot intentionally roll back production. Revert on `main` instead.

The native Aspire pipeline builds/pushes both images, provisions the manual
`ingacookbook-migrations` job, starts it once, and polls that exact execution.
The web application's **provisioning step** depends on migration success. A failed,
stopped, unknown, or timed-out job blocks the new web deployment. The job has one
replica, no retries, a ten-minute execution limit, and a twelve-minute overall
deployment wait. Polling tolerates transient read failures; an uncertain start is
not automatically repeated. Inspect the execution before retrying a failed release.

The gate is not a database rollback: earlier schema or infrastructure writes may
have succeeded. After real users begin, use migrations compatible with the running
version, preserve migration history, and back up before destructive schema changes.
Revert faulty application changes on `main` to release known-good code through the
same workflow; do not assume that activating old code reverses the database. Restore
PostgreSQL to a new server for a point-in-time recovery, and validate matching photo
data before switching connections. Soft deletion is recovery assistance, not a full
coordinated database/blob backup strategy.

### Production behavior and first live verification

The AppHost explicitly enables status-only `/health` and `/alive` endpoints.
Readiness checks PostgreSQL; liveness does not. HTTP probes carry the trusted
forwarded HTTPS scheme, and the app accepts forwarded headers behind ACA ingress.
Do not reuse this proxy setting for a container directly exposed to untrusted traffic.

The generated Container App sets `runtime.dotnet.autoConfigureDataProtection=true`.
Azure supplies shared Data Protection storage across revisions/replicas. The app's
discriminator remains `IngaCookBook`. The owner recovery command must run **inside
the running app's container** so it uses the same environment and key ring:

```powershell
az containerapp exec --resource-group rg-ingacookbook-prod --name ingacookbook --command "dotnet IngaCookBook.dll account-recovery --user-id <account-reference> --base-url https://<app-host>/"
```

Use a private operator terminal, never CI or captured dashboard commands. Verify
the person's identity as described below. The first live validation must establish
that the actual Azure key-ring integration also works in this operator process.

The workflow runs `scripts/Test-Production.ps1` for public HTTP checks. Before
inviting the first user, also register a disposable account, create a workspace,
recipe/version/batch/tasting, upload and retrieve a photo, and check dashboard logs.
Then release a harmless change and verify sign-in survives it, a private owner
recovery link works, and the old password is rejected. Remove only that disposable
account. Confirm budget recipients and a usable restore path. These Azure checks
cannot be established by local tests or a Bicep preview.

A deployment/restart can disconnect Interactive Server circuits. One warm replica
and sticky sessions support the initial usage but do not preserve unsaved server
state across releases. Prefer deploying when the notebook is idle. The existing
refresh/reconnect UI preserves visible inputs where possible and never silently
promises that an interrupted write was rolled back.

References: [Aspire deployment](https://aspire.dev/deployment/azure/aca-deployment-aspire-cli/),
[Azure .NET hosting and Data Protection](https://learn.microsoft.com/azure/container-apps/dotnet-overview),
[Azure budgets](https://learn.microsoft.com/azure/cost-management-billing/costs/tutorial-acm-create-budgets).

## Account email

### No email required (default)

Both the web application and AppHost default to `Email:Provider=None`. Anyone can
register with an email-shaped login name and password and sign in immediately.
Registration does not generate a confirmation token, send email, or mark the email
as verified. Each account creates its own private workspace; registering does not
grant access to another user's recipes. Existing unconfirmed test accounts can also
sign in. Failed password attempts count toward Identity's default lockout policy
(five failed attempts, five-minute lockout).

**My account → Login details** shows the login name and stable account reference.
Email changes and verification actions are unavailable in this mode. **Forgot your
password?** explains owner-assisted recovery instead of offering an email form.
Old confirmation routes do not change or verify an email address when email is
disabled. Password changes, passkeys, authenticator setup and existing two-factor
recovery codes remain available. Two-factor recovery codes replace the authenticator
step; they are not password-reset tokens.

If an earlier local setup explicitly selected Mailpit or Azure, set the AppHost
user secret `Email:Provider` to `None` and restart Aspire. Leaving an Azure connection
string configured does not enable sending. A disabled transport throws if an
unexpected code path attempts email, rather than reporting successful delivery.

### Owner-assisted password recovery

1. Verify the person independently before recovering an account. A claimed email
   address or account reference is not proof of ownership. Keep your wife's account
   reference from **Login details** in a safe place. If needed, locate the record by
   inspecting `AspNetUsers` through a trusted database session and verify it before
   proceeding; do not reset an arbitrary account solely because someone names its email.
2. Open a private operator terminal in the deployed application's environment,
   working directory, and OS identity. The process needs the same database/storage
   configuration and **Data Protection key ring** as the running web app. Run the
   published server executable in its administrative mode:

   ```text
   dotnet IngaCookBook.dll account-recovery --user-id <account-reference> --base-url https://your-app-host/
   ```

   This mode exits without starting an HTTP listener or background workers. It is
   not a replacement for Aspire when running the application. For local validation,
   use the compiled server DLL with the Aspire-provided resource configuration and
   the same OS user's key ring; a fresh key ring on another machine cannot generate
   a usable link. The configured application discriminator is `IngaCookBook`.
3. The command prints the selected account reference and a reset link to that
   terminal only. Send the link privately after verification. Do not run it in a
   logged CI job or an Aspire resource command whose output is retained in the
   dashboard. The base address must be an HTTPS origin without a path, query,
   fragment, or embedded credentials. Exit codes are 0 (link issued), 1 (account
   not found), or 2 (invalid arguments); unexpected errors propagate as failures.
4. The user opens the link, enters their existing login email and a new password.
   Links expire after one hour and cannot be reused after a successful reset.
   Issuing a link does not change the password or account; a successful reset
   invalidates earlier reset tokens through Identity's security stamp. Never share
   or store a temporary plaintext password.

Recovery preserves the account ID, workspace, recipes, email verification state,
and two-factor settings. It does not bypass an authenticator or clear an active
lockout. Someone who also loses their authenticator needs their saved two-factor
recovery codes; this command handles password recovery only. Browser sessions are
invalidated according to Identity's normal security-stamp validation intervals,
not synchronously in every open browser tab.

No public admin-reset endpoint is exposed. Production deployment must make the
operator terminal and persisted key ring available before rollout. Test an actual
operator-generated link after deployment. Optional email confirmation/change links
use the same one-hour Identity data-protection token lifespan.

### Deferred provider alternatives (researched October 4, 2026)

Microsoft's [ACS retirement guide](https://learn.microsoft.com/en-us/azure/communication-services/acs-retirement-and-breaking-changes-guide)
lists Email for retirement on **September 30, 2028**, with new-customer signup for
retiring services ending **October 23, 2026**. Do not provision ACS for this rollout.
Existing local capture and the optional ACS adapter remain
implemented for explicit opt-in. The initial deployment does not need a replacement;
the alternatives below are retained as research if automatic email is wanted later.

| Candidate | Cost and deployment implications |
| --- | --- |
| Exchange Online Plan 1 + Microsoft Graph | Microsoft lists USD 4/user/month, paid annually. A licensed business mailbox and Exchange permissions are required; a personal Outlook.com account is not this mailbox. Scope application send permission to the dedicated mailbox and use managed identity. Microsoft 365 licensing is separate from the Azure resource-group budget. |
| Azure Logic Apps Consumption + Outlook.com | Very low usage-based Azure cost and no business mailbox license, using a personal Outlook.com connection authorized interactively by its owner. Connection revocation can require reauthorization; this adds a workflow and an OAuth connection to operate. Use a dedicated sender account, protect the HTTP trigger, hide reset/confirmation content from run history, and retain failure/timeout handling. |
| SendGrid through Azure Marketplace | The public Marketplace purchase page currently starts at USD 19.95/month. Older references to Azure Free 100 plans do not establish availability for a new subscription. This is a third-party provider purchased through Azure. |

The earlier provider assessment prioritized free or inexpensive third-party options
without a custom domain or a Microsoft 365 organization. Azure hosting itself does
not require Microsoft 365; the Exchange alternative above requires a licensed
organizational mailbox. No replacement provider has been selected or implemented;
email is disabled for the initial rollout instead.

For the expected single-user volume, the leading domain-free option is a dedicated
Gmail mailbox sending through authenticated SMTP. This uses an actual `gmail.com`
sender, with [two-step verification and an app password where supported](https://support.google.com/mail/answer/185833).
Google recommends OAuth when available; changing the account password revokes app
passwords, and [consumer sending/abuse limits](https://support.google.com/mail/answer/22839)
still apply. Keep any app password in server-side secret configuration.

[Purelymail](https://purelymail.com/pricing) is a paid alternative at USD 10/year on
its simple plan, subject to resource-use limits. Its [shared-domain mailboxes](https://purelymail.com/docs/users)
avoid buying a domain; the provider documents a small username charge depending on
length. [SMTP and app passwords](https://purelymail.com/docs/setup/technical) are
supported. Its service permits transactional mail but is not for marketing or
mailing lists. This option has not been tested against a live account.

Brevo's free plan includes 300 sends/day, but its [replacement sender for free
addresses](https://help.brevo.com/hc/en-us/articles/14925263522578-Comply-with-Gmail-Yahoo-and-Microsoft-s-requirements-for-email-senders)
is explicitly a temporary measure, not a permanent domain-free contract. SMTP2GO's
[signup requirements](https://www.smtp2go.com/blog/smtp2go-questions-answered/) exclude
free-provider addresses. These restrictions matter more here than their free quotas.

If automatic email is introduced later, preserve the shared Identity message
composer and local Mailpit workflow: add the selected provider behind
`IAccountEmailTransport`, validate configuration and
cancellation, test failures, and verify live confirmation/password-reset delivery
to an owned inbox, including SMTP connectivity from the deployed application.

An Exchange mailbox can initially use the tenant's `onmicrosoft.com` address without
buying a custom domain. Microsoft limits that domain to 100 external recipients per
organization per rolling 24 hours, which fits this application's expected usage.
See [initial email addresses](https://learn.microsoft.com/en-us/microsoft-365/admin/email/change-email-address)
and [Exchange sending limits](https://learn.microsoft.com/en-us/office365/servicedescriptions/exchange-online-service-description/exchange-online-limits).

Outside Azure, [Resend's free plan](https://resend.com/pricing) is a low-volume
alternative, but [sending to other recipients requires a verified owned domain](https://resend.com/docs/knowledge-base/403-error-resend-dev-domain).
It is an option if purchasing/configuring a domain is preferable to the Microsoft
mailbox or connector setup; it is not part of the selected deployment yet.

Sources: [Exchange pricing](https://www.microsoft.com/en-us/microsoft-365/exchange/exchange-online-business-plans-and-pricing),
[Graph sendMail](https://learn.microsoft.com/en-us/graph/api/user-sendmail),
[Exchange application RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac),
[Outlook.com connector](https://learn.microsoft.com/en-us/connectors/outlook/),
[Logic Apps pricing](https://azure.microsoft.com/en-us/pricing/details/logic-apps/),
and [SendGrid Marketplace plans](https://marketplace.microsoft.com/en-us/product/sendgrid.tsg-saas-offer?tab=PlansAndPrice).

### Optional local inbox

Set `Email:Provider=Mailpit` in AppHost user secrets and start through Aspire:

```powershell
dotnet user-secrets set 'Email:Provider' 'Mailpit' --project src/IngaCookBook.AppHost
```

The run-mode AppHost then adds **mailpit**
using `CommunityToolkit.Aspire.Hosting.MailPit` and waits for it before starting the
web application. Open its **http** endpoint in the Aspire dashboard; ports are
allocated dynamically. Register with any test address, open the captured confirmation
email, and follow its link before signing in. Password resets, email changes, and
resend-confirmation requests use the same inbox. Messages have HTML and plain-text
parts and the sender is `notebook@example.test`.

No Azure subscription or email credentials are needed for this mode. Mailpit captures
SMTP locally and has no forwarding/relay configured. It is a development inbox, not an
ACS emulator: it verifies our email content and account flows, not Azure delivery.
The inbox has no persistent volume and is discarded with its container. It contains
account recovery links; keep it private on the development machine. Links point to
the local app origin and only work where that origin is reachable and trusted.

The server accepts `Email:Provider=Mailpit` only in Development. Unknown providers,
invalid sender addresses, missing endpoints/credentials, or invalid timeouts fail
startup. Explicit Mailpit or Azure selection also enables required account
confirmation and the email recovery/change screens. The web application and AppHost
otherwise default to `None`; the publishing graph never adds a local inbox.
Switching email on later requires a confirmation plan for existing unverified users.
EF design-time model creation
does not start the host or send email.

### Optional real Azure delivery during development

These instructions describe the existing ACS adapter, not the recommended new
deployment path. Review the retirement dates and provider decision above before
creating resources.

1. Create an **Email Communication Services** resource and provision an Azure-managed
   domain for an initial test, or verify a custom domain (including SPF and DKIM).
2. Create an **Azure Communication Services** resource and **connect the email domain**
   to it. These are two distinct resources. Copy the connected domain's exact MailFrom
   sender address and the Communication Services resource's connection string.
3. Store configuration in **AppHost user secrets**, outside the repository:

   ```powershell
   dotnet user-secrets set 'ConnectionStrings:communicationemail' '<ACS connection string>' --project src/IngaCookBook.AppHost
   dotnet user-secrets set 'Parameters:email-sender' '<verified MailFrom address>' --project src/IngaCookBook.AppHost
   dotnet user-secrets set 'Email:Provider' 'Azure' --project src/IngaCookBook.AppHost
   ```

   Prefer your IDE's user-secret editor for the real connection string so it does not
   enter shell history. User secrets are local development storage, not an encrypted
   production secret store. Never commit credentials or export dashboard secrets.
4. Restart Aspire. Azure mode omits Mailpit and sends through
   `Azure.Communication.Email` **1.1.0**. Register one disposable account with an inbox
   you control, confirm it, then test Forgot password and email change. Check the
   inbox/junk folder as well as Azure delivery status. Real sends use Azure resources
   and are billed; they are deliberately excluded from automated tests.
5. To return to no-email registration, set `Email:Provider` to `None` in AppHost user
   secrets and restart (or select `Mailpit` for local capture). Leaving Azure
   credentials configured does not enable Azure sending.

Both providers share the same Identity message composer. Delivery observes the SSR
request cancellation token, host shutdown, and a 30-second bound (`Email:TimeoutSeconds`
on the **web** app; allowed range 1–120). SDK/SMTP failures propagate rather than
displaying a false success. An interrupted send may already have been accepted;
retrying can produce another message. If registration created the account but
delivery failed, use **Resend email confirmation** instead of registering again.
There is no background delivery queue or application-level automatic resend.

The ACS adapter waits for the send operation to succeed. This means Azure accepted
the message for delivery, not that it arrived in an inbox. This implementation uses
a connection string for the local Azure option. A future production email mode
would need a selected provider and its authentication/configuration. Deployment
automation and delivery-event monitoring remain separate work.

References: [Aspire Mailpit integration](https://aspire.dev/integrations/devtools/mailpit/mailpit-host/),
[Azure resource prerequisites](https://learn.microsoft.com/azure/communication-services/concepts/email/prepare-email-communication-resource),
[connect an email domain](https://learn.microsoft.com/azure/communication-services/quickstarts/email/connect-email-communication-resource),
and [Azure Email .NET SDK](https://learn.microsoft.com/dotnet/api/overview/azure/communication.email-readme).

## EF migrations

During the current pre-deployment stage, all data is disposable test data and
earlier development schemas need not remain compatible. Schema changes may
replace the migration history with a new initial migration and reset the
IngaCookBook development database. Verify the application/database target first,
generate through the migration resource below, and verify startup and migration
tests against the fresh baseline. Do not reset other projects or reset data for
changes that need no schema update. Revisit this policy before real users or deployment.

`Aspire.Hosting.EntityFrameworkCore` **13.6.0-preview.1.26479.8** manages
`ingacookbook-migrations` and its **dotnet-ef 10.0.12** tool. It does not alter the machine's
global EF tool. On every start, migrations wait for PostgreSQL and the database;
the web application starts only after migration success. Failure blocks web startup.
Migrations use the actual web startup registration, keeping Identity's design-time
and runtime models aligned.

For a schema change:

1. Stop Aspire, change the model, and build the solution.
2. Start Aspire. If EF detects pending model changes, migration startup can fail and
   block the web resource; the migration authoring commands remain available.
3. On `ingacookbook-migrations` in the dashboard, run **Add Migration...** and supply a
   descriptive name. Review the generated files in `src/IngaCookBook/Data/Migrations`
   with namespace `IngaCookBook.Migrations`.
4. Apply the repository's conventions to the handwritten migration class (file-scoped
   namespace and `internal sealed partial class`); leave generated designer code alone.
   Keep the model snapshot in the same migrations directory. EF can choose a directory
   from the legacy namespace when regenerating a missing snapshot, so check its location.
5. Stop Aspire, rebuild, and start again. The built-in migration resource applies the
   newly compiled migration before starting the web application.
6. Run **Get Database Status** on `ingacookbook-migrations` to verify applied migrations and
   pending model changes.

CLI equivalents for inspecting and updating the current compiled model:

```powershell
aspire resource ingacookbook-migrations ef-database-status --non-interactive
aspire resource ingacookbook-migrations ef-database-update --non-interactive
aspire logs ingacookbook-migrations --non-interactive
```

The built-in **Remove Migration**, **Drop Database**, and **Reset Database** commands
are also available. In this pinned integration, **Remove Migration** invokes EF
with `--force` and can revert an applied migration and delete its data. Treat all
three as destructive operations requiring explicit intent. There is no
application-startup migration routine or custom worker.
See [Aspire's EF migration integration](https://aspire.dev/integrations/databases/efcore/migrations/).

## Health, telemetry, and pgAdmin

The application references `IngaCookBook.ServiceDefaults`. In Development, and in
deployments explicitly setting `HealthChecks:Enabled=true`, `/health`
checks readiness including PostgreSQL connectivity; `/alive` checks process liveness
independently of PostgreSQL. Aspire monitors `/health`. The database readiness check
has a five-second timeout so EF's transient retries do not hold an unhealthy response
open for minutes. Cancellation is cooperative: an in-flight Npgsql connection attempt
can delay the response (about 15 seconds in local outage validation). Normal
application operations retain Aspire's retry behavior.

The dashboard exposes server logs, request traces, Npgsql database spans, and metrics.
Useful CLI commands include `aspire logs ingacookbook`, `aspire otel traces`, and
`aspire otel logs`. The Aspire MCP server provides the same resource and telemetry
visibility to agents. Database query telemetry can contain application information;
keep exports and runtime logs out of Git.

Aspire 13.6.0 has a confirmed idle-watch regression: after about one minute,
`Watch task over Kubernetes ContainerExec resources terminated unexpectedly`
can appear even while the application and containers are healthy. The terminated
watch can miss later container-command state/log updates. A fixed **13.6.1 staging
build** passed an isolated comparison, including the five-minute watch restart;
it was not yet on NuGet.org's stable feed when verified. See the
[diagnosis and validation](docs/workflow-qa.md#aspire-log-investigation-and-remaining-limitation)
before changing dependencies. Do not treat this as a recipe/database timeout or
hide it by disabling orchestration logging.

Aspire 13.6 retains dashboard run history automatically in **Run** persistence
mode. Use the header's run selector to compare the live run with completed runs;
historical runs are read-only. Up to ten unpinned runs are retained per application,
and pinning keeps a useful run. Console logs are only persisted after their stream
is viewed or exported, so capture needed console output before stopping.

Keep the default dashboard data directory, `<ASPIRE_HOME>/dashboard` (normally
`~/.aspire/dashboard`), outside the checkout. Persisted resource snapshots can
contain unredacted credentials even when the dashboard masks them. On Windows,
the directory inherits filesystem permissions; keep it private and do not share
its databases or backups. The existing Git exclusions cover `.aspire/`, `*.db`,
`*.db-wal`, and `*.db-shm`. No application telemetry changes are needed for run
history. See [dashboard persistence](https://aspire.dev/dashboard/data-persistence/).

For quick SQL inspection, select **REPL** on the running `postgres` resource. The
dashboard opens the container's bundled `psql` with its existing credentials, so
no local client or pgAdmin startup is required. Run `\connect ingacookbook` to switch
from the initial `postgres` database to the application database. Exit with `\q`
before closing the tab; closing the viewer alone can leave the client running.
This shell has normal write permissions, not read-only access. Database reset or
drop still requires explicit intent to delete local data. `WithRepl()` uses our
existing PostgreSQL package without an experimental-warning suppression, while
the dashboard terminal infrastructure remains preview. See
[PostgreSQL REPLs](https://aspire.dev/integrations/databases/postgres/postgres-host/#open-an-interactive-repl).

pgAdmin is available only in run mode. Start `pgadmin` from its dashboard action or:

```powershell
aspire resource pgadmin start --non-interactive
aspire wait pgadmin --timeout 120 --non-interactive
```

Open its generated HTTP endpoint, expand **Servers → postgres → Databases → ingacookbook**.
Aspire configures the connection and credentials. Stop it from the dashboard when done.

## Agent tools and skills

Shared repository skills live only in `.agents/skills`, supported by Codex and
Copilot CLI. Copilot desktop inherits repository/CLI skills and MCP configuration.
The Aspire skills use the first-party **aspire-skills v0.0.3** bundle refreshed
with CLI 13.6.0. Local corrections document 13.6's Project v2 diagnostic changes
and the distinction between persistent-resource cleanup and volume deletion.
Reconcile those corrections when refreshing the skills; use installed package/API
evidence and current documentation when upstream guidance differs. Project v2
migration remains deferred; the AppHost continues to use `AddProject`.

The existing user-level Codex and Copilot MCP entries run **`aspire agent mcp`**.
Keep a single entry per agent. No repository MCP duplicate or VS Code agent
configuration is required. On a new machine, install Aspire on `PATH`, then:

- Codex: `codex mcp add aspire -- aspire agent mcp` (unless already configured).
- Copilot CLI: use `/mcp add` to add a local/stdio server named `aspire`, command
  `aspire`, arguments `agent mcp`, at user scope. Its configuration is stored in
  `~/.copilot/mcp-config.json` and is inherited by Copilot desktop.
- Restart/reload the agent after configuration. Start the application from this
  repository and use the Aspire MCP resource-list tool to verify the connection.
  If several AppHosts are running, select this repository's AppHost explicitly.

The checked-in skills require no additional installation on clone. To refresh the
Aspire skills, use the matching CLI's `aspire agent init` with the **standard** skill
location (`.agents/skills`), then review the diff. Avoid creating alternate copies under
`.github`, `.codex`, or VS Code agent configuration. Keep user secrets, dashboard
tokens, runtime `.aspire` state, telemetry exports, and temporary browser output out
of source control.

The [Fluent UI Blazor v5 usage skill](.agents/skills/fluentui-blazor-usage/SKILL.md)
includes setup, data-grid, and theming references. The library is configured as
described above; installing the skill alone does not configure an MCP server.
When using its examples, follow
`AGENTS.md` for code-behind, CSS, static account rendering, and central package
versions. Verify version-sensitive APIs against the Fluent UI Blazor MCP server's
documentation and the released package source. The v5.0.0 package's assembly file
version is `5.0.0.26268`, also reported by the installed MCP server. Its version
checker compares this assembly version against the NuGet version and reports a
false mismatch; `5.0.0.26268` is not the NuGet package version. Some skill examples
still use prerelease APIs, so verify component names and parameters against the
installed package instead of copying them verbatim.

References: [Aspire MCP](https://aspire.dev/reference/cli/commands/aspire-agent-mcp/),
[Codex skills](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills),
[Copilot CLI skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills),
[Copilot desktop](https://docs.github.com/en/copilot/how-tos/github-copilot-app/customize-github-copilot-app).

## Validation

For every change set, run formatting from the repository root, review the fixes,
and require a clean verification pass. Stop Aspire first on Windows. After code
changes, also build and run the unit and bUnit projects. All five test projects use
native Microsoft.Testing.Platform and Shouldly. The complete suite additionally
requires Docker, Aspire tooling, development HTTPS, and Playwright Chromium:

```powershell
dotnet format IngaCookBook.slnx --severity warn
dotnet format IngaCookBook.slnx --severity warn --verify-no-changes
dotnet build IngaCookBook.slnx
pwsh ./tests/IngaCookBook.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test --solution IngaCookBook.slnx
```

The unit and component projects can still run individually without external
processes. Testcontainers covers focused database integration, Aspire integration
covers the real resource graph and HTTP behavior, and Playwright covers browser
interaction against its own isolated Aspire application. Test resources are
disposable and never use the development database volumes. See
[tests/README.md](tests/README.md) for commands, prerequisites, layer selection, and
[build/README.md](build/README.md) for formatting scope, analyzer rules, and Razor
code-behind validation. Formatting does not replace checks for other file types.
