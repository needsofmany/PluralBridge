# PluralBridge Tag Task History

This file records the major work represented by each repository tag. It is intended as a project-facing release/task history for the repository root.

Tags are listed in reverse chronological order so the latest project changes appear first.

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

## Current post-v0.3.0 follow-up queue

### Public site and user-path verification

- Smoke-test the production site after deployment.
- Check:
  - `https://thepluralbridge.org/start-here.html`
  - `https://thepluralbridge.org/help-me-export.html`
  - `https://thepluralbridge.org/install.html`
  - `https://thepluralbridge.org/run.html`
  - `https://thepluralbridge.org/safety.html`
- Verify the full click path:
  - Start Here → Help Me Export → Install → Run → Safety
- Confirm `mailto:needsofthemany@thepluralbridge.org` behaves reasonably in common browsers.
- Request search indexing for newly added or materially changed pages if needed.

### Documentation and contributor workflow

- Add a release checklist.
- Decide whether to add `CHANGELOG.md`.
- Document branch discipline in `CONTRIBUTING.md`.
- Add a public contributor workflow page.
- Include preview-before-merge expectations.
- Include PR header visual-confirmation expectations.

### Regular-user tooling

- Add a guided runner or command wrapper.
- Add export validation helper output.
- Consider first-pass Windows-friendly helper script.
- Improve notes export documentation once the workflow is ready.
- Add clearer success/failure reporting.

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
