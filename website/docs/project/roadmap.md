# PluralBridge Roadmap and Post-Release Task List

This roadmap captures the work after the first public repository release.

PluralBridge stays preservation-first: protect preserved data, define stable service contracts, then expand clients and hosting.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## Purpose

This roadmap is the working queue for service-side architecture and post-release delivery.

Core priorities:

- Preserve exported data first.
- Establish a local REST service boundary above preserved storage.
- Use SQL Server as the known-good backend for the first service cut.
- Add SQLite, viewer clients, cloud migration, and expanded APIs after the contract shape is reliable.
- Define explicit mobile storage targets for parity and data fidelity:
  - Android: local SQLite (via Room or equivalent typed access layer), not JSON-as-database.
  - iOS/iPadOS: local SQLite-backed store (native wrapper acceptable), not JSON-as-database.

Why this matters:

- Database-shaped data should stay in a real database format so relations, constraints, and migrations are explicit and testable.
- Treating a `*.db` artifact as JSON encourages lossy transforms, weak validation, and brittle import/export behavior.
- PluralBridge should preserve source fidelity while using storage primitives that are auditable, queryable, and maintainable.

## Status Legend

- `Now` = immediate active focus.
- `Next` = queued behind current service-boundary work.
- `Later` = post-contract or scale-dependent work.

## Status Checklist

Legend:

- ✅ complete
- 🟡 in progress
- ⬜ planned / queued

Current checklist:

- ✅ Preservation-first documentation baseline is published.
- ✅ SQL Server path is established as known-good for first service cut.
- 🟡 Read-only REST contract shape and endpoint coverage are being finalized.
- 🟡 Local security defaults (localhost-first, LAN opt-in, local credential boundary) are being hardened.
- 🟡 Contract-centered tests for schema/version/read-only behavior are active.
- 🟡 Phone recovery research (Samsung/Pixel/iPhone) is ongoing and feeding import fidelity docs.
- ⬜ SQLite runtime migration tracking and validation-query pack.
- ⬜ Native Android foundation (SQLite via Room/typed layer).
- ⬜ Native iOS/iPadOS foundation (SQLite-backed runtime with migrations).
- ⬜ Cloud roadmap execution (Azure first, AWS later) on stable contract base.

## Current Workstreams

### Local REST Service Boundary

Build the first service layer before SQLite migration and before viewer-first work.

Key tasks:

- Keep the current SQL Server database as the known-good backend.
- Create a local C# REST service that returns JSON from the populated PluralBridge database.
- Treat REST as the formal separation between storage and clients.
- Keep first contract behavior read-only (`GET`) until write workflows are clearly defined.
- Make future SQLite, Azure, AWS, viewer, and mobile work depend on the service contract, not direct DB access.

Initial service-side solution shape:

```text
PluralBridge.Service.sln

src/PluralBridge.Api/
src/PluralBridge.Business/
src/PluralBridge.Data/
src/PluralBridge.Data.SqlServer/
src/PluralBridge.Models/
src/PluralBridge.Common/

tests/PluralBridge.Api.Tests/
tests/PluralBridge.Business.Tests/
tests/PluralBridge.Data.Tests/
```

### API Contracts and Versioning

PluralBridge REST interfaces are contracts. Before release they can iterate; once published, they stay stable for their support window.

Key tasks:

- Use route-level versioning from the start.
- Separate PluralBridge-native routes from Simply Plural/Apparyllis-shaped compatibility routes.
- Mark early contracts as `draft` until endpoint shape, error shape, date/time behavior, ID behavior, and null/empty behavior are settled.
- Expose versioned OpenAPI/Swagger output with Swashbuckle.
- Protect stable contracts with contract tests.

Recommended route families:

```text
/api/v1/...    PluralBridge native API
/sp/v1/...     Simply Plural/Apparyllis-shaped compatibility API
```

Contract lifecycle:

```text
draft -> preview -> stable -> deprecated -> retired
```

### Service Organization (MVC-Style Separation)

The first executable may be console-hosted, but architecture should stay layered and non-monolithic.

Service organization:

- Model: DTOs and response models.
- View: JSON returned over HTTP.
- Controller: request routing and service orchestration.
- Business layer: mapping, service rules, read-only policy, and future write-operation rules.
- Data layer: repository contracts with SQL Server implementation.
- Swashbuckle/OpenAPI: API discovery, onboarding, and smoke-test targets.

### Console Client / Shell Client

Create a client that exercises REST contracts instead of direct SQL Server access.

Key tasks:

- Support command-line one-shot mode for startup/init and simple calls.
- Support an interactive shell mode that behaves like a simple Bash-style command dispatcher.
- Convert typed commands into REST calls against the local PluralBridge service.
- Dump or format JSON responses initially.
- Use C# first if rapid validation matters most.
- Add a small C++ reference client later if useful for portability and outside developers.

Example shell direction:

```text
pbcli shell
PluralBridge> members list
PluralBridge> member get <id>
PluralBridge> notes list <id>
PluralBridge> fronts recent
PluralBridge> quit
```

### Contract-Centered Testing

Tests should verify behavior and contract promises, not implementation trivia.

Useful test targets:

- Exporter creates the expected preservation layout.
- `manifest.json` maps exported files to source endpoints.
- Notes remain decorrelated from member/decorator names in filenames.
- `avatar_manifest.tsv` maps member IDs to local files.
- Safety check rejects tokens, private paths, export folders, database files, and unintended images.
- SQL scripts exist in required order.
- REST endpoints return documented JSON shapes.
- First service version remains read-only.
- Stable API versions remain compatible while supported.

### Local Security Model

Separate immediate local-service security from future hosted identity design.

Local service rules:

- Bind to localhost by default.
- Require explicit opt-in for LAN binding.
- Require a generated local bearer key or equivalent local access credential for LAN/non-loopback mode.
- Keep the first service version read-only.
- Do not expose the local service directly to the public internet.
- Do not treat the Simply Plural token as a PluralBridge account credential.

Future hosted-service direction (later):

- Prefer passkeys/WebAuthn.
- Design account recovery deliberately.
- Provide authenticator management.
- Keep authentication and authorization separate.

### Non-Technical User Word Guide

Create a root-level `.docx` guide for ordinary users. The guide should be a Word document, not Markdown.

Key tasks:

- Explain PluralBridge for users who know how to install and run software.
- Explain how to open/display the Word document.
- Include the basic double-click workflow for opening a `.docx` file.
- Include options for users without Microsoft Word or LibreOffice.
- Keep the tone user-facing, low-assumption, and preservation-first.

Suggested filename:

```text
PluralBridge_User_Guide.docx
```

### SQLite Support (After Service Boundary)

SQLite remains the preferred local/offline runtime database for ordinary users, but should follow service-boundary stabilization.

Key tasks:

- Define SQLite schema.
- Create SQLite import path from exported JSON, notes, and avatar manifest data.
- Implement SQLite data-layer project behind the same repository contracts used by SQL Server.
- Create validation queries and sample reports.
- Document the SQLite workflow.
- Test with synthetic/redacted fixtures only.

Mobile storage targets tied to this workstream:

- Android: SQLite with a typed data-access layer and migration tracking.
- iOS/iPadOS: SQLite-backed local store with schema migration tracking.
- Keep JSON as interchange/import format only, not as the primary runtime database.

### Local Viewer (After REST Path Is Usable)

Build a Visual Studio 2022 C++20 Win32 local/offline viewer after the service contract and local access model are stable enough to consume.

Initial viewer direction:

- Target Windows 7 SP1 through Windows 11.
- Produce 32-bit and 64-bit builds where feasible.
- List alters/slivers.
- Display avatar images.
- Use the REST service boundary where practical rather than binding the viewer to SQL Server internals.
- Brief mode TBD.
- Verbose/full-info mode TBD.

### Native Phone Apps (Android and iOS/iPadOS)

Native phone apps are part of the roadmap and should follow contract stabilization, local security hardening, and SQLite migration maturity.

Platform direction:

- ![](https://cdn.simpleicons.org/android/3DDC84) Android native app:
  - Local runtime storage: SQLite (Room or equivalent typed access layer).
  - Storage model: normalized relational schema with migration tracking.
  - API usage: consume versioned PluralBridge contracts rather than direct SQL scripting.
- ![](https://cdn.simpleicons.org/apple/FFFFFF) iOS/iPadOS native app:
  - Local runtime storage: SQLite-backed local store (native wrapper acceptable).
  - Storage model: normalized relational schema with migration tracking.
  - API usage: consume versioned PluralBridge contracts rather than direct SQL scripting.

Boundary rule:

- JSON remains an interchange/import format, not the primary runtime database for phone apps.

### Cloud Migration and REST Expansion

Cloud migration and hosted services should build on the same versioned contract thinking established by the local service.

Key tasks:

- Design Azure migration around PluralBridge-owned authentication.
- Leave room for AWS support later.
- Preserve public API compatibility where feasible without copying Simply Plural/Apparyllis code, UI, branding, server implementation, app flow, or authentication systems.
- Support a compatibility-style API for preserved Simply Plural-shaped data.
- Support a native PluralBridge API for future clients and hosted services.
- Use versioned contracts and documented deprecation/retirement windows.

### Platform Expansion and Outreach

Long-term targets after outside developer help becomes realistic:

- Windows desktop viewer.
- Linux tooling/support.
- Web client.
- Android app.
- iOS/iPadOS app.
- AWS migration support after Azure.
- Native macOS deferred unless demand or outside development support appears.

Outreach and support tasks:

- Provide email support; details TBD.
- Create GitHub Issues with `help-wanted` and `good-first-issue` labels.
- Use Discord outreach only with moderator permission.
- Contact adjacent plural-tool maintainers.
- Position PluralBridge as an import/preservation bridge, including for other tools.

## Consolidated Work Order

Legend:

✅ complete · 🟡 in progress · ⬜ planned / queued

- 🟡 Lock the first read-only service contract (`GET`) for preserved-data access and publish draft OpenAPI output.
- 🟡 Harden local-service security defaults (localhost-first, LAN opt-in, local credential requirement for non-loopback use).
- 🟡 Complete compatibility and native route coverage for core preserved data shapes.
- 🟡 Finalize contract tests for schema shape, version behavior, and read-only guarantees.
- ⬜ Stabilize SQLite as the cross-platform local runtime store, with migration tracking and validation queries.
- ⬜ Implement native phone app foundations:
   - Android: SQLite runtime via Room (or equivalent typed layer).
   - iOS/iPadOS: SQLite-backed runtime store with migration tracking.
   - JSON remains import/interchange only, not runtime primary storage.
- ⬜ Expand client surfaces on stable contracts (console tooling, Windows viewer, and future web/mobile client paths).
- 🟡 Continue Samsung/Pixel/iPhone recovery research and feed verified findings back into import fidelity rules and docs.
- ⬜ Draft hosted roadmap in dependency order (Azure first, AWS later) using the same versioned contract model.
- ⬜ Scale contributor onboarding and support-channel docs as architecture and contracts stabilize.

## Suggested Work Order

Use the consolidated work order above as the active sequence. Reorder only when a dependency changes or a blocker appears.

## Guiding Principle

Preserve data first. Establish the local REST boundary. Stabilize contracts. Then expand portability through SQLite, local viewers, cloud migration, compatibility APIs, and future clients.

