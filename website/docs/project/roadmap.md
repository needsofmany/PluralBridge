# PluralBridge Roadmap and Post-Release Task List

This roadmap captures the PluralBridge post-release work queue after the initial public repository release. The priority remains preservation first. The next engineering layer is the local REST service boundary.

SQL Server remains the proven backend for the first service cut. SQLite, the Windows viewer, and cloud migration follow after the API contract is shaped.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## Purpose

This roadmap reflects the current service-side design priorities:

- Preserve exported data first.
- Establish a local REST service boundary above preserved storage.
- Keep the current SQL Server path as the known-good backend for the first service cut.
- Add SQLite, viewers, cloud migration, and future clients after the service contract has a working shape.

## Task Summary

The main post-release workstreams are:

1. Implement the local REST service boundary.
2. Define API contracts and versioning.
3. Use MVC-style service organization.
4. Build a console client or shell client.
5. Apply contract-centered testing.
6. Define the security model.
7. Create the non-technical user Word guide.
8. Add SQLite support after the service boundary is shaped.
9. Start the local viewer after the REST/service path is established.
10. Roadmap cloud migration and REST expansion.
11. Continue platform expansion and outreach.

## Implement the Local REST Service Boundary

Build the first service layer before migrating SQL scripts to SQLite or starting the viewer as the main development target.

Key tasks:

- Keep the current PluralBridge SQL Server database as the known-good backend for the first service cut.
- Create a local C# REST service that returns JSON from the populated PluralBridge database.
- Treat the REST layer as the formal separation point between storage and clients.
- Make future SQLite, Azure, AWS, viewer, and mobile work depend on the service contract rather than direct database access.
- Start with read-only GET behavior.
- Add PUT, POST, PATCH, and DELETE only when concrete workflows require them.

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

## Define API Contracts and Versioning

PluralBridge REST interfaces are contracts. During development they may change; after public release of a contract version, they become stable while supported.

Key tasks:

- Use route-level versioning from the start.
- Separate PluralBridge-native routes from Simply Plural/Apparyllis-shaped compatibility routes.
- Mark early contracts as draft until endpoint shape, error shape, date/time behavior, ID behavior, and null/empty behavior are settled.
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

## Use MVC-Style Service Organization

The service is not a monolithic app. The first executable may be a console-hosted service, but the parts should remain separated.

Service organization:

- Model: DTOs and response models.
- View: JSON returned over HTTP.
- Controller: routing/glue layer that accepts requests, calls business services, and returns serialized responses.
- Business layer: service-level rules, mappings, read-only policy, and future write-operation rules.
- Data layer: repository contracts and storage-specific SQL Server implementation.
- Swashbuckle/OpenAPI: API discovery, collaborator onboarding, and smoke-test target.

## Build the Console Client / Shell Client

Create a client that exercises the service boundary rather than talking directly to SQL Server.

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

## Apply Contract-Centered Testing

Tests should verify design promises and public contracts. They should not become line-by-line implementation policing or coverage theater.

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

## Define the Security Model

Separate local-service security from future hosted/cloud identity. The local service is the immediate concern; passkeys/WebAuthn belong in the future hosted-service design.

Local service rules:

- Bind to localhost by default.
- Require explicit opt-in for LAN binding.
- Require a generated local bearer key or equivalent local access credential for LAN/non-loopback mode.
- Keep the first service version read-only.
- Do not expose the local service directly to the public internet.
- Do not treat the Simply Plural token as a PluralBridge account credential.

Future hosted-service direction:

- Prefer passkeys/WebAuthn.
- Design account recovery deliberately.
- Provide authenticator management.
- Keep authentication and authorization separate.

## Create the Non-Technical User Word Guide

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

## Add SQLite Support After the Service Boundary Is Shaped

SQLite remains the preferred local/offline runtime database for ordinary users, but it should follow the service-boundary work rather than precede it.

Key tasks:

- Define SQLite schema.
- Create SQLite import path from exported JSON, notes, and avatar manifest data.
- Implement SQLite data-layer project behind the same repository contracts used by SQL Server.
- Create validation queries and sample reports.
- Document the SQLite workflow.
- Test with synthetic/redacted fixtures only.

## Start the Local Viewer After the REST/Service Path Is Established

Build a Visual Studio 2022 C++20 Win32 local/offline viewer after the service contract and local access model are stable enough to consume.

Initial viewer direction:

- Target Windows 7 SP1 through Windows 11.
- Produce 32-bit and 64-bit builds where feasible.
- List alters/slivers.
- Display avatar images.
- Use the REST service boundary where practical rather than binding the viewer to SQL Server internals.
- Brief mode TBD.
- Verbose/full-info mode TBD.

## Roadmap Cloud Migration and REST Expansion

Cloud migration and hosted services should build on the same versioned contract thinking established by the local service.

Key tasks:

- Design Azure migration around PluralBridge-owned authentication.
- Leave room for AWS support later.
- Preserve public API compatibility where feasible without copying Simply Plural/Apparyllis code, UI, branding, server implementation, app flow, or authentication systems.
- Support a compatibility-style API for preserved Simply Plural-shaped data.
- Support a native PluralBridge API for future clients and hosted services.
- Use versioned contracts and documented deprecation/retirement windows.

## Platform Expansion and Outreach

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

## Suggested Work Order

1. Create the local REST service design issue/milestone and make it the next major engineering workstream.
2. Define the initial read-only endpoint list from the current exported Simply Plural/Apparyllis-shaped data set.
3. Create the C# service-side solution skeleton with Api, Business, Models, Data, and Data.SqlServer projects.
4. Add Swashbuckle/OpenAPI and mark the first API contracts as draft.
5. Implement health/database health endpoints.
6. Implement the first GET endpoints against the current PluralBridge SQL Server database.
7. Create the first console client in C# to validate REST calls and JSON responses.
8. Add contract-centered tests using synthetic/redacted fixtures.
9. Document API versioning, deprecation, retirement, and read-only behavior.
10. Document the local-service security model: localhost default, LAN opt-in, local bearer key for LAN mode.
11. Create the non-technical `.docx` user guide.
12. Begin SQLite data-layer work behind the service boundary.
13. Begin Windows viewer design after the REST/service contract is usable.
14. Draft Azure/native API/compatibility API roadmap documentation.
15. Continue public documentation, support-channel planning, and contributor onboarding as the project grows.

## Guiding Principle

Preserve data first. Establish the local REST boundary. Stabilize the contract. Then make the preserved data portable through SQLite, local viewers, cloud migration, compatible APIs, and future clients.

