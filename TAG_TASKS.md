## v0.7.3 — Demo UI polish

- Replaced the in-app login proof panel with a compact protected-session strip and logout action.
- Added friendly read-only demo views for every app button.
- Rendered members, front history, source systems, systems, privacy buckets, custom fields, import batches, source records, source ID mappings, import metadata, and demo session data as readable cards/rows.
- Kept raw JSON available behind row expanders for proof and verification.
- Updated the app page title to “Explore the PluralBridge read-only demo.”
- Preserved the served/root app split and the required apiBaseUrl difference.

# PluralBridge Tag Task History

This file records the major work represented by each repository tag. It is intended as a project-facing release/task history for the repository root.

Tags are listed in reverse chronological order so the latest project changes appear first.

---

## v0.7.2 — Conference-safe hosted demo database

### Major tasks completed

- Created and validated the conference-safe Azure SQL demo database `PluralBridgeDemoAnonXlat`.
- Rebuilt the anonymized demo database from an SSMS-generated script with remapped GUID literals instead of live GUID mutation.
- Replaced member display names with a 49-name synthetic list.
- Replaced member descriptions and the System description with same-character-count lorem ipsum text.
- Cleared `dbo.pb_source_records.RawJson` to `N'[ ]'` for all 945 source-record rows.
- Validated row counts, table and column shape, primary keys, foreign keys, constraints, foreign-key orphan checks, DBCC constraint validation, and uniqueidentifier profiling.
- Retained the conference demo anonymization and validation scripts under `database/demo/`.
- Updated the hosted API proof so `/api/me` reports `PluralBridgeDemoAnonXlat`.
- Updated the Azure App Service connection string so the protected browser demo reads from `PluralBridgeDemoAnonXlat`.
- Validated the live protected browser path: login, `me`, `members`, anonymized member names, and lorem ipsum descriptions.

### Notes

This release marks the protected Azure-hosted browser/API proof as conference-demo ready. The deployed demo reads from the anonymized `PluralBridgeDemoAnonXlat` database, while private/source proof databases remain outside public demo configuration.

Historical Azure proof and targeting artifacts remain project history. Runtime public surfaces for the app, API, website, and GitHub deployment path do not point at the private/source proof database.

---

## v0.7.1 — Protected browser button app proof

### Major tasks completed

- Added a protected browser button app proof under the ASP.NET Core API project.
- Copied the browser proof app into `api/PluralBridge.Api/PluralBridge.Api/wwwroot/app/` so the Azure API App Service can host `/app/` directly.
- Added cookie-auth login and logout endpoints for the protected proof surface.
- Protected `/app/`, copied app static files, and `/api/*` controller endpoints behind authentication.
- Added configuration support for demo credentials through local User Secrets and Azure App Service app settings.
- Guarded the temporary `/whoami` auth diagnostic endpoint behind `DEBUG_MODE` so it remains available for local diagnostics without appearing in normal release builds.
- Deployed the protected proof to the Azure App Service `pluralbridge-api-proof-001` using Kudu ZIP deployment.
- Turned SCM Basic Auth Publishing Credentials back off after deployment.
- Validated the Azure end-to-end flow: login, app load, protected `me` API call, and logout.

### Notes

This release proves that the Azure-hosted PluralBridge API can serve a protected browser app and protected REST surface from the same App Service. It keeps the demo credential values outside repository history by using User Secrets locally and Azure App Service app settings in the deployed proof.

The protected browser app remains a proof path for the Plural conference/demo objective. Later work can replace the fixed demo login with durable user/session/System mapping and decide how `https://thepluralbridge.org/app` routes to the application surface.


## v0.7.0 — Read-only C# API and database-backed browser proof

### Major tasks completed

- Added the first durable ASP.NET Core C# REST API under `api/`.
- Added Microsoft.Data.SqlClient database access against the validated Azure SQL proof database.
- Added read-only REST endpoints for `me`, source systems, import batches, systems, members, privacy buckets, custom fields, front history, source records, source ID mappings, and import metadata.
- Wired the browser app read-only buttons to real API calls instead of static mock contract payloads.
- Preserved the Phase 3A login/session surface as frozen placeholder behavior while completing Phase 2B.
- Confirmed all required Phase 2B browser/API counts from the validated 1-6 proof slice: 1 source system, 1 import batch, 1 system, 49 members, 2 privacy buckets, 7 custom fields, 886 front-history rows, 945 source records, and 945 source ID mappings.
- Kept all Phase 2B endpoints read-only with `canWrite` set to false.
- Excluded raw source JSON from the public source-record endpoint while preserving source inventory and hash metadata.
- Added Visual Studio and .NET ignore rules so local build output, user settings, and transient IDE files stay out of repository history.
- Added output-box CSS wrapping and height limits so large JSON payloads remain usable in the browser proof surface.

### Notes

This release marks the first PluralBridge browser proof backed by a durable C# REST API and real Azure SQL data. It corrects the Phase 2 read-only surface so the app now proves live database retrieval across the complete 1-6 proof slice instead of static browser-side contract JSON.

The API still uses a fixed Phase 2B proof context. Phase 3 remains responsible for replacing that fixed context with real login, session, and user-to-System mapping behavior.


---

## v0.6.0 — Azure SQL cloud-readiness proof

### Major tasks completed

- Created the Azure SQL cloud-readiness proof database for the validated PluralBridge 1-6 vertical slice.
- Proved the 1-6 schema in Azure SQL with the expected 9 proof tables.
- Validated expected core columns, primary keys, foreign keys, and unique constraints in Azure SQL.
- Confirmed the Azure proof database was writable and suitable for schema and data-load validation.
- Generated a private ignored data-load SQL artifact from the private JSON export.
- Loaded the validated 1-6 slice into Azure SQL using SSMS.
- Confirmed all expected Azure data counts passed: 1 source system, 1 import batch, 1 system, 49 members, 2 privacy buckets, 7 custom fields, 886 front-history rows, 945 source records, and 945 source ID mappings.
- Archived failed candidate paths instead of repairing failed generated SQL forward.
- Added compact non-private audit checkpoints for the Azure schema proof and data-load proof.

### Notes

This release marks the first completed Azure SQL database-backed proof for the minimal PluralBridge 1-6 vertical slice. Private JSON export files and generated private SQL load files remain ignored and are not part of the public repository history.

The next project phase can build on this proof toward the read-only REST/browser demo path for token-scoped member and front-history data.

---

## v0.5.2 — Website menu restructure

### Major tasks completed

- Restructured the top-level public website navigation into grouped menus.
- Added `Home` as a visible top-level navigation item.
- Kept `Start Here` and `Export Now` as direct top-level navigation items.
- Grouped documentation and user-flow pages under `Guides`.
- Grouped Discussions, Pluralpedia, Mastodon, and GitHub under `Community`.
- Converted `About` into a grouped menu with `About PluralBridge` and a disabled `Contact Us` coming-soon placeholder.
- Added active navigation states for top-level pages and guide submenu items.
- Added mobile styling so dropdown menu labels behave like centered navigation buttons.
- Added `website/site-menu.js` so opening one dropdown closes any other open dropdown.
- Previewed the grouped navigation locally in desktop browser and on Android before release.

### Notes

This release improves the public website navigation before the later Contact/Support page work. The Contact Us item is intentionally present as a disabled placeholder; the actual Contact/Support page is deferred to a separate feature branch.

---

## v0.5.1 — Social preview image update

### Major tasks completed

- Added `website/assets/images/pluralbridge-about-logo.png` as the current approved PluralBridge logo image for social previews.
- Updated Open Graph metadata in `website/index.html` so shared website links point to the current approved logo.
- Updated Twitter card metadata in `website/index.html` so shared website links point to the current approved logo.
- Left the old social-preview image file in place instead of deleting it.

### Notes

This is a small production patch release for the public website metadata. It gets the current approved PluralBridge logo into production for link previews on Mastodon and other social platforms. Some services may cache previews, so the corrected image may appear only after cache refresh or a new share.

---

## v0.5.0 — Mobile navigation and privacy reminder usability

### Major tasks completed

- Added the first contributor-ready starter Issues for developer-community outreach.
- Added and labeled starter Issues for synthetic fixtures, export validation, SQLite schema drafting, non-technical export guidance, and documentation/accessibility review.
- Added a developer landing Discussion pointing contributors to scoped starter Issues and privacy-safe contribution boundaries.
- Improved public website navigation for narrow screens and mobile devices.
- Changed mobile navigation into a two-column tappable grid.
- Preserved the existing desktop navigation and page structure while allowing better wrapping behavior.
- Added a mobile privacy reminder toggle so the privacy warning starts compact on phones and can be expanded when needed.
- Kept the full privacy warning visible on desktop.
- Added `website/privacy-banner.js` for the mobile privacy reminder toggle.
- Previewed website changes locally and on a real phone before release.

### Notes

This release is a visible website usability release. It improves the mobile path before adding the later Contact/Support page, so users can navigate the site more clearly on phones while still seeing the privacy reminder and having access to the full warning text.

The developer-outreach foundation now includes scoped starter Issues and a GitHub Discussion landing point, with the same safety boundary: contributors should use synthetic examples, redacted descriptions, and public-safe fixtures only.

---

## v0.4.2 — GitHub Discussions and public project links

### Major tasks completed

- Enabled and configured GitHub Discussions for the PluralBridge repository.
- Created starter Discussions categories for announcements, design proposals, feature ideas, help and questions, community and documentation, and polls.
- Added pinned starter discussions explaining how each major discussion area should be used.
- Added GitHub Discussions links to `README.md`.
- Added GitHub Discussions links to the public website navigation.
- Refreshed the `TAG_TASKS.md` follow-up queue for the current `v0.4.1` project state.
- Added developer-community publicizing follow-up items to the release task queue.
- Added official Simply Plural export guidance follow-up items to the release task queue.

### Notes

This release prepares PluralBridge for developer and community participation by making GitHub Discussions visible from the repository and website, while keeping privacy boundaries and public-project workflow expectations clear.

---

## v0.4.1 — Release-history bookkeeping

### Major tasks completed

- Added the missing `v0.4.0` entry to `TAG_TASKS.md` after the `v0.4.0` release tag had already been created.
- Preserved the existing `v0.4.0` tag instead of rewriting or moving release history.
- Recorded this follow-up as a small patch release so the repository history remains explicit.

### Notes

This release is a release-history correction. It documents the `v0.4.0` contributor-governance release in `TAG_TASKS.md` without changing the already-published `v0.4.0` tag.

---

## v0.4.0 — Contributor governance and developer workflow

### Major tasks completed

- Expanded contributor governance guidance in `CONTRIBUTING.md`.
- Documented the project discussion model for GitHub Discussions, Issues, Pull Requests, and community spaces.
- Added branch-role guidance for `master`, `dev`, feature branches, and production patch branches.
- Added maintainer authority and delegation guidance for protecting privacy posture, release stability, public trust, and community safety.
- Added contributor privacy rules covering real exports, tokens, avatars, notes, logs, member data, friend data, fronting history, privacy buckets, and database files.
- Added project independence boundaries for Simply Plural and Apparyllis interoperability work.
- Added plural-community conduct guidance for contributors.
- Added current developer-help areas, including official export normalization, SQLite support, validation reports, documentation, accessibility, SQL review, viewer prototypes, and import bridges.
- Expanded `website/developer-workflow.html` to mirror the repository-facing contributor workflow guidance.
- Normalized top-level website navigation across public pages.
- Normalized Docs and About hero presentation.
- Tightened navbar CSS for the current flat navigation.
- Previewed website changes locally and through Cloudflare before release.

### Pull requests and major commits included

- PR #25: Expand contributor governance and website consistency.
- PR #26: Release contributor governance guidance.

### Notes

This release prepared PluralBridge for careful developer and open-source contributor participation by documenting where discussions happen, how branches are handled, what privacy boundaries apply, and how contributors can help without handling private user data.

---

## v0.3.4 — Website logo and release-history access

### Major tasks completed

- Added the PluralBridge bridge image to the public website navigation.
- Moved the larger PluralBridge logo/banner to the About page, where project identity is explained.
- Updated homepage social preview image metadata to use the PluralBridge logo.
- Added homepage access to the release and tag task history.
- Added documentation-page access to the release and tag task history.
- Previewed the website changes locally before merging into `dev`.

### Notes

This release added the first public PluralBridge logo treatment to the website, improved project identity presentation, and made the repository tag/task history easier for visitors to find.

---

## v0.3.3 — Official export probes and privacy banner

### Major tasks completed

- Added inspection/probe tooling for official Simply Plural export files.
- Added normalization/probe support to help understand the shape of official export data.
- Added a site-wide privacy reminder banner warning that Simply Plural exports may contain sensitive account, token, note, message, friend, avatar, and System data.
- Reinforced that export files should be treated as private backups and should not be posted publicly or attached to support requests.

### Pull requests and major commits included

- PR #17: Add official export inspection and normalization probes.
- PR #18: Add site-wide export privacy reminder.
- PR #19: Release official export probes and privacy banner.

### Notes

This release started the investigation path for official Simply Plural export files while preserving the project safety boundary around private export data.

---

## v0.3.2 — Guided export launcher

### Major tasks completed

- Added a guided export launcher to make the export path easier for regular users.
- Continued reducing command-line friction for users who need to preserve data before the Simply Plural shutdown.
- Kept the export process aligned with the local-first safety model.

### Pull requests and major commits included

- PR #15: Add guided export launcher.
- PR #16: Release guided export launcher.

### Notes

This release moved PluralBridge toward a more approachable export workflow while still keeping the current implementation script-based and local-first.

---

## v0.3.1 — Root tag task history

### Major tasks completed

- Added root-level `TAG_TASKS.md` to record major work represented by repository tags.
- Established a project-facing release and task history outside generated website documentation.
- Created a place to summarize release work, repair checkpoints, and follow-up queues.

### Pull requests and major commits included

- PR #13: Add tag task history.
- PR #14: Release tag task history.

### Notes

This release added the repository-level task history that is now being linked from the public website.

---

## v0.3.0 — Public website, documentation rendering, and non-technical user path

### Major tasks completed

- Added generated public documentation pages under `website/docs`.
- Added local Prism-based syntax highlighting for documentation code blocks.
- Added public Start Here and Help Me Export paths for regular users.
- Improved README and `START_HERE.md` guidance for users arriving from GitHub.
- Added Simply Plural shutdown and preservation landing content.
- Reworked `install.html` from a requirements inventory into a practical setup sequence.
- Reworked `run.html` into a plain-language export procedure.
- Added clearer Simply Plural API token setup guidance using `SP_TOKEN`.
- Added JSON export guidance.
- Added avatar export guidance.
- Added output-checking guidance for:
  - `exports/json`
  - `exports/member_images`
  - additional export folders or manifest files as scripts are added or documented
- Added common warning signs for:
  - missing token
  - missing `exports/json`
  - missing `exports/json/members.json`
  - authentication or authorization errors
- Added OS-specific install guidance for:
  - Windows
  - macOS
  - Linux
- Moved Python download links into the Python installation step.
- Kept Git Bash, Terminal, and Git guidance in the command-line setup step.
- Added a `mailto:` contact path for other desktop or laptop setups:
  - `needsofthemany@thepluralbridge.org`
- Clarified that mobile-only export is not the current PluralBridge path.
- Added token walkthrough screenshots and public user/developer/project documentation pages.
- Added SEO metadata, sitemap, `robots.txt`, and Google Search Console verification support.
- Fixed documentation rendering, code highlighting, docs layout, and token guide screenshot issues.
- Previewed the website changes before release.
- Released through a `dev` to `master` pull request.
- Fast-forwarded `dev` back to the released `master` state.
- Created and pushed annotated tag `v0.3.0`.

### Pull requests and major commits included

- PR #4: Add nontechnical start here path.
- PR #5: Release earlier `dev` work into `master`.
- PR #6: Add plain-language export guide.
- PR #7: Clarify Git requirement for regular users.
- PR #8: Release `dev` into `master`.
- PR #9: Improve install and run user path.
- PR #10: Add OS-specific install download paths.
- PR #11: Separate Python and shell install guidance.
- PR #12: Release improved public guidance for non-technical users.

### Notes

This release materially changed the public shape of PluralBridge. It made the project more legible to regular users, especially users arriving from GitHub who may not know the difference between a source repository, a ZIP download, scripts, and a finished app installer.

The release keeps PluralBridge focused on preservation first: helping users understand how to export and protect their Simply Plural data while the original service remains available.

---

## docs-rendering-live — Verified documentation rendering workflow

### Major tasks completed

- Verified the public documentation rendering workflow.
- Confirmed generated documentation pages could be built and deployed through the website path.
- Improved generated documentation page behavior and layout.
- Helped establish the website documentation pipeline that later shipped as part of `v0.3.0`.

### Notes

This was a workflow verification tag rather than a versioned release. It marks a known-good point for documentation rendering.

---

## image-fix — Token guide screenshot repair point

### Major tasks completed

- Fixed the token guide Step 6 screenshot issue.
- Preserved the token walkthrough as a usable visual guide.
- Kept the correction isolated to the token-guide image problem.

### Notes

This was a focused repair tag rather than a versioned release. It marks a known-good point for the corrected token-guide screenshot.

---

## v0.2.0 — Public site and repository maturity baseline

### Major tasks completed

- Expanded the public-facing project website.
- Added or strengthened public project pages around:
  - project identity
  - Simply Plural shutdown urgency
  - export/preservation messaging
  - install/run/safety guidance
  - documentation navigation
- Improved public messaging for regular users arriving from GitHub.
- Added public metadata and crawler/indexing support.
- Added public website structure that could support later documentation rendering and user-path improvements.
- Improved GitHub-facing credibility through clearer project framing.
- Continued refining safety guidance and public-facing documentation boundaries.
- Preserved the project’s core message:
  - export first
  - keep user data private
  - do not treat a Simply Plural token as a future PluralBridge credential
  - build future tools around preserved data

### Notes

This tag represents a stronger public baseline before the larger `v0.3.0` website/documentation/user-path release.

---

## v0.1.0 — Initial public preservation baseline

### Major tasks completed

- Established PluralBridge as an independent preservation and continuity project.
- Published the initial public repository structure.
- Added the GNU GPL v3.0 project license.
- Added core project framing:
  - PluralBridge is independent.
  - PluralBridge is not affiliated with Simply Plural, Apparyllis, or the Simply Plural development team.
  - PluralBridge uses public Apparyllis REST API access with a user-created token.
  - PluralBridge is focused first on preservation of user-owned data.
- Added initial repository documentation and examples.
- Added early Python export tooling for Simply Plural data.
- Added early avatar export tooling.
- Added SQL Server migration/import script structure.
- Added redacted/safe example shapes.
- Added basic safety guidance for tokens, exports, private data, screenshots, reports, avatars, notes, and database files.
- Added repository layout for:
  - `docs/`
  - `examples/`
  - `reports/`
  - `scripts/`
  - `tests/`
  - `website/`

### Notes

This tag represents the initial public foundation: export-first preservation, local files, private user control, and a repo structure large enough to grow into documentation, database work, viewers, and future services.

---

## Current post-v0.5.2 follow-up queue

### Public site and user-path verification

- Smoke-test the production site after deployment.
- Verify the home page social-preview metadata points to `https://thepluralbridge.org/assets/images/pluralbridge-about-logo.png`.
- Check:
  - `https://thepluralbridge.org/start-here.html`
  - `https://thepluralbridge.org/help-me-export.html`
  - `https://thepluralbridge.org/export-now.html`
  - `https://thepluralbridge.org/install.html`
  - `https://thepluralbridge.org/run.html`
  - `https://thepluralbridge.org/safety.html`
  - `https://thepluralbridge.org/developer-workflow.html`
- Verify the full click path:
  - Start Here → Help Me Export → Export Now → Install → Run → Safety
- Confirm GitHub Discussions links are visible from the website and README.
- Confirm `mailto:needsofthemany@thepluralbridge.org` behaves reasonably in common browsers.
- Request search indexing for newly added or materially changed pages if needed.

### Developer-community publicizing

- Point developer outreach to GitHub Discussions before asking for broad participation.
- Use Discussions for design proposals, questions, feature ideas, documentation suggestions, and polls.
- Use Issues only for scoped, actionable work.
- Keep contributor-facing messaging clear that private exports, real System data, tokens, avatars, notes, logs, and database files must not be posted publicly.

### Documentation and contributor workflow

- Add a release checklist.
- Decide whether to add `CHANGELOG.md`.
- Keep `CONTRIBUTING.md` and `website/developer-workflow.html` aligned as contributor workflow evolves.

### Regular-user tooling

- Add export validation helper output.
- Consider first-pass Windows-friendly helper script.
- Improve notes export documentation once the workflow is ready.
- Add clearer success/failure reporting.

### Official Simply Plural export guidance

- Add user-facing guidance that the official Simply Plural export should be saved now and treated as a private backup.
- Explain that official Simply Plural export files may contain sensitive account, token, security, private, note, message, friend, avatar-reference, report, usage, and System data.
- Explain that the official Simply Plural export does not download avatar image files.
- Explain that PluralBridge API export can download actual member avatar files while they are still reachable.

### Private candidate review

- Review private repo `candidates` docs.
- Sort candidate docs into:
  - promote public
  - keep private
  - rewrite/sanitize
  - archive/discard
- Promote only public-safe material.

### Pluralpedia

- Review the current Pluralpedia page.
- Restore or rewrite missing public-facing project context where appropriate.
- Keep wording factual, calm, and focused on preservation and continuity.


---

## Current contributor-ready outreach state

The first starter Issues and developer landing Discussion have been created for developer-community outreach. Further Issues should still be created only when each item has a clear scope, privacy-safe boundaries, and a synthetic-data path.

### Current starter Issues

- #38 — Add synthetic export fixture set for tests and examples.
- #39 — Add export validation summary output.
- #40 — Draft initial SQLite schema for local/offline preserved data.
- #41 — Improve non-technical export success/failure guidance.
- #42 — Review documentation for accessibility and plain-language clarity.

### Privacy and data-safety boundary

All contributor-ready Issues should assume synthetic examples, redacted descriptions, and public-safe fixtures only. Contributors should not request, post, attach, or inspect private Simply Plural exports, real System data, API tokens, authorization headers, avatar images, notes, messages, friend data, fronting history, logs, screenshots containing private data, or database files created from real exports.

### Outreach sequencing

Developer-community outreach should come before the Contact/Support page. Mobile navigation repair has now been addressed in the v0.5.0 work. The Contact/Support page remains the next focused website task after developer outreach stabilizes.
