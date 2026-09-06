# Contributing to PluralBridge

Thanks for your interest in contributing.

This guide explains how to propose work, where discussions happen, and the privacy/safety boundaries required for this project.

## Quick Start

1. Start from `dev` and create a focused feature branch.
2. Keep changes small, testable, and scoped to one coherent goal.
3. Open a pull request into `dev` with summary + test notes.
4. Never include real export data, tokens, or private user/System details.

## License for Contributions

By submitting a pull request, patch, or other contribution to this repository, you agree that your contribution is provided under the same license as this project:

GNU General Public License, version 3 or, at your option, any later version (GPL-3.0-or-later).

## Different Backgrounds, Shared Workflow

PluralBridge may attract contributors from several worlds: professional software development, open-source maintenance, plural-community support, documentation, data preservation, security, accessibility, and user advocacy.

Those backgrounds do not all use the same habits or vocabulary. A professional developer may be new to GitHub Discussions, public issue triage, or open-source maintainer norms. An experienced open-source contributor may be new to the privacy, safety, and release-stability requirements of a project preserving sensitive plural System data.

This workflow exists so contributors do not have to guess. It explains where conversations happen, how work becomes accepted, when maintainers need to decide, and how PluralBridge protects users while still welcoming outside help.

## Where Project Discussions Happen

Use these channels by purpose:

- **GitHub Discussions**: design proposals, broad questions, feature ideas, governance conversation before work is scoped ([https://github.com/needsofmany/PluralBridge/discussions](https://github.com/needsofmany/PluralBridge/discussions)).
- **GitHub Issues**: accepted, actionable work.
- **Pull requests**: review of actual code, documentation, website, or data-shape changes.
- **Community spaces** (Discord, Facebook, Reddit, email): informal conversation and outreach.

Project decisions that affect code, documentation, privacy posture, release behavior, supported workflows, or public messaging should be captured in GitHub so future contributors can see the reasoning.

Security-sensitive or privacy-sensitive reports should use the project security reporting path instead of public discussion.

## Branch Workflow

PluralBridge uses a small branch structure so production stays stable, development stays reviewable, and active work stays isolated.

### Branch roles

| Branch | Role | Rules |
|---|---|---|
| `master` | Production branch | Merge into `master` from `dev` only. Must never be in a broken state. Represents the current public release. |
| `dev` | Stable integration branch for next release | Merge into `dev` from project branches only. Must never be in a broken state. Collects reviewed project work before release. |
| `<project_branch>` | Active work branch | Create from `dev`. Keep scope to one coherent change. Test before merging back into `dev`. Use clear names like `feature/docs-rendering`, `feature/branching-workflow-docs`, or `patch/image-fix`. |

### Feature branch decisions

Feature branches are proposal branches. Opening a pull request does not guarantee acceptance.

Small fixes may be opened directly as pull requests. Larger changes should start with an issue or discussion before implementation.

Changes that should be discussed before coding include:

- visual design or branding changes
- navigation or public website structure changes
- public messaging changes
- privacy, safety, or trust-boundary wording changes
- database schema changes
- export format changes
- importer or normalizer behavior changes
- authentication, token, or secret-handling changes
- REST API compatibility decisions
- client architecture decisions
- large refactors
- new dependencies
- changes that affect nontechnical-user workflows

Maintainers may ask for changes, ask that work be split, defer work to a later milestone, redirect work into a different branch, or close work that does not fit the project. A change can be technically correct and still be declined if it does not fit the project mission, privacy posture, accessibility goals, documentation tone, release plan, or community safety needs.

### Patch branches and production fixes

Patch branches are exceptional production-fix branches.

Create a patch branch only when the current public release needs a targeted correction that should not wait for unreleased work already in `dev`.

Contributors may propose patch handling by opening an issue or discussion explaining the production impact. A maintainer decides whether the issue qualifies for patch handling.

Good reasons for a patch branch include:

- live website breakage
- privacy or safety wording already published on `master`
- broken public install, export, or run instructions
- release artifact problems
- serious production-facing bugs
- small urgent corrections that must ship without unrelated `dev` changes

Poor reasons for a patch branch include:

- ordinary feature work
- broad documentation rewrites
- speculative refactoring
- experimental importer or viewer work
- wording polish that can wait for the next release
- changes that would pull unrelated `dev` work into production

Patch workflow:

1. A contributor or maintainer identifies the production-impacting issue.
2. A maintainer approves patch handling.
3. The patch branch is created from `master`.
4. The patch is kept narrow and reviewed through a pull request into `master`.
5. `master` is tagged if the patch changes the released version.
6. After the patch release goes live, `master` is merged or fast-forwarded back into `dev` so the production fix remains present in future releases.

### Maintainer authority and delegation

PluralBridge welcomes outside contributions, but contribution access is not the same thing as production authority. Maintainers are responsible for protecting the project's privacy posture, public trust, release stability, and community safety.

## Deployment Workflow

### Web and REST service deployment

For website and REST service work, use GitHub pull requests and Cloudflare preview deployments so changes can be inspected before they merge forward.

**Pull request checklist**

- Open the pull request from the project branch into `dev`.
- Use a short title that describes the change.
- In the description, include a summary of what changed.
- List the branch model or workflow rules affected by the change, when applicable.
- List deployment notes, including whether the change affects the website, REST service, client apps, or documentation.
- List the testing performed before requesting review.

**Cloudflare preview steps**

After the pull request is created, GitHub should show an **All checks have passed** box when the Cloudflare Pages deployment succeeds.

1. Open the pull request conversation page.
2. Scroll to the merge/status box near the bottom of the pull request.
3. Open the checks details for the Cloudflare Pages status entry.
4. Follow the deployment or preview link shown in the check details.

The preview URL is generated by Cloudflare after the branch is pushed and the pull request check runs. Contributors do not need Cloudflare dashboard access to find it.

## Client Application Workflow (Current State)

For Windows, Linux, macOS, Android, and iOS client work, the release workflow is still TBD.

The project will need an automated build process that runs when client code is committed and pushed.

## Contributor Expectations

Contributors should keep changes small, reviewable, and safe.

Expected behavior:

- Start from the correct branch.
- Keep unrelated changes out of the same pull request.
- Do not commit generated exports, private data, credentials, local database files, or machine-specific artifacts.
- Include a clear summary of what changed and how it was tested.
- Update documentation when behavior, commands, website pages, or user-facing workflows change.

## Contributor Privacy Rules

PluralBridge works with sensitive preservation data. Contributors must keep real user and System data out of public project work.

Do not include any of the following in issues, pull requests, commits, examples, screenshots, logs, fixtures, documentation, or support discussions:

- real Simply Plural export files
- real PluralBridge export folders
- API tokens or authorization headers
- security logs
- IP addresses from exports or logs
- private notes or message content
- real member names or profile data
- avatar images from real Systems
- friend data
- fronting history
- private custom fields
- privacy bucket data
- database files created from real exports

Use synthetic test data or carefully redacted examples. Redaction must remove private values, stable identifiers, tokens, account data, and anything that could expose a real System.

Official Simply Plural exports should be treated as secret-bearing private backup files. They may contain account, token, security, private, note, message, friend, avatar-reference, report, usage, and System data.

## Project Independence Boundaries

PluralBridge is an independent preservation and continuity project. It is not affiliated with Simply Plural or Apparyllis.

Contributors must not copy or import Simply Plural or Apparyllis private implementation materials.

Do not contribute work based on:

- decompiled application code
- reverse engineered private implementation details
- copied Simply Plural UI layouts or app flows
- copied Simply Plural branding or visual identity
- private server behavior that is not exposed through public user-facing APIs or documented behavior
- authentication systems copied from Simply Plural or Apparyllis

Interoperability work should stay focused on user-owned exported data, documented/public API behavior, local preservation, validation, migration, and future compatible interfaces where feasible.

## Plural Community Conduct

PluralBridge serves plural Systems and people preserving sensitive personal records. Contributors should reduce friction and protect user agency.

Expected conduct:

- Respect plural Systems and plural-community language.
- Do not ask users to disclose private System details.
- Do not ask users to upload private exports for troubleshooting.
- Do not turn support or development threads into debates about identity, diagnosis, legitimacy, or community politics.
- Assume users may be under shutdown pressure and may be worried about losing years of records.
- Keep explanations practical, calm, and privacy-preserving.

## Current Ways Developers Can Help

Useful contribution areas include:

- official Simply Plural export inspection and normalization using synthetic fixtures
- tests for normalizer behavior and export validation
- SQLite support for local/offline use
- validation reports and readable summaries
- documentation for nontechnical users
- website accessibility and clarity improvements
- SQL review and report-query improvements
- local viewer prototypes
- import bridges for other plural tools when those projects want them
- safe synthetic sample data shaped like Simply Plural data without exposing real System data

## Developer Notes

- Preserve existing copyright and license notices.
- Keep changes focused and well-described.
- Update documentation when behavior changes.

## Pull Requests

Please include:

- a clear description of the change
- the reason for the change
- any testing performed

## Privacy and Safety

This section is a quick summary. The authoritative policy is in **Contributor Privacy Rules** above.

- Never include real exported Simply Plural data in pull requests, issues, examples, screenshots, test fixtures, or documentation.
- Never include tokens, IDs, member names, avatars, notes, friends lists, fronting history, custom fields, privacy buckets, or screenshots containing private data.
- Use synthetic fixtures and strongly redacted examples only.
