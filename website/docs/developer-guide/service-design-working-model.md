# PluralBridge Service Design Working Model

This document captures the current working model for the PluralBridge service side: a local REST service boundary above preserved data storage, validated with Swagger/OpenAPI and Fiddler before clients are built against it.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## Purpose

This document captures the current working model for the PluralBridge service side. The design establishes a formal service boundary above the preserved database so future clients, viewers, importers, converters, storage backends, and cloud services do not couple themselves directly to SQL scripts or local database details.

The updated priority is to build the local REST service boundary first, validate it with Swagger/OpenAPI and Fiddler, prove one full vertical slice through GET /sp/v1/me, and defer the command-line client until the first endpoint contract has settled.

The central design statement is:

> The local REST layer is the first formal separation point between preserved data storage and future PluralBridge clients.

## Architectural Spine

The immediate architecture keeps the already-proven SQL Server path in place and adds a service boundary above it. SQLite, Azure, AWS, and client applications should be introduced after the service contract has a working shape.

```text
Simply Plural export data
        ->
PluralBridge SQL Server database
        ->
Local PluralBridge REST service
        ->
Swagger/OpenAPI and Fiddler validation
        ->
Contract tests
        ->
Console client / shell client
        ->
Future viewers, importers, converters, cloud services, and mobile clients
```

The REST service becomes the canonical interface for clients. Clients should call the service; they should not query SQL Server, SQLite, Azure, AWS, or raw exported JSON directly unless the tool is explicitly an importer or storage utility.

## Updated Near-Term Priority Order

## Service-Side Separation of Concerns

The service should be implemented as a multi-project solution rather than a monolithic console program. The first host may run as a console application, but the executable host is only the transport boundary. API routing, business logic, storage contracts, SQL Server persistence, shared models, and common utilities remain separate.

### Proposed Solution Layout

```text
PluralBridge.Service.sln

src/
  PluralBridge.Api/
  PluralBridge.Business/
  PluralBridge.Data/
  PluralBridge.Data.SqlServer/
  PluralBridge.Models/
  PluralBridge.Common/

tests/
  PluralBridge.Api.Tests/
  PluralBridge.Business.Tests/
  PluralBridge.Data.Tests/
```

### Future Backend Projects

```text
src/
  PluralBridge.Data.Sqlite/
  PluralBridge.Data.Azure/
  PluralBridge.Data.Aws/
```

## Project Responsibilities

### PluralBridge.Api

Hosted REST service and HTTP boundary.

- Application startup
- HTTP routing and controllers
- JSON serialization
- Swagger/Swashbuckle setup
- Request validation at the transport edge
- Configuration loading
- Dependency-injection wiring
- Local bind address and port configuration

### PluralBridge.Models

DTOs and API-facing model shapes.

- Compatibility models for Simply Plural/Apparyllis-shaped API
- Native PluralBridge models after the compatibility surface is proven
- Error response DTOs
- Version/status metadata models

### PluralBridge.Business

Service-level operations and design-policy enforcement.

- Read-only policy enforcement
- Compatibility mapping decisions
- Member, note, avatar, and front-history aggregation
- Future write-rule enforcement when PUT, POST, PATCH, or DELETE are designed

### PluralBridge.Data

Storage-neutral repository contracts.

- Repository interfaces
- Storage-neutral records
- Query abstractions that avoid SQL Server leakage

### PluralBridge.Data.SqlServer

Current proven backend implementation.

- SQL Server connection handling
- Queries against PluralBridge tables/views
- Mapping rows to storage records
- Implementation of PluralBridge.Data interfaces

### PluralBridge.Common

Small shared infrastructure.

- Constants
- Configuration helpers
- Result/error helpers
- Logging helpers; keep this project small and boring

## MVC Mental Model

MVC is a good fit for the service side. There is no UI state to bind. The model is the DTO or response model. The view is the JSON response body. The controller is the HTTP endpoint glue.

```text
Model      -> DTOs / response models
View       -> JSON response body
Controller -> HTTP endpoint glue
```

Controller actions should stay thin. The controller accepts an HTTP request, calls a business service, and returns a serialized response. Policy belongs in the business layer. Storage details belong behind repository interfaces.

```text
HTTP request
    -> Controller
    -> Business service
    -> Repository interface
    -> SQL Server implementation
    -> Business service
    -> Controller returns JSON
```

## OpenAPI / Swashbuckle

Swashbuckle should be included in the first cut. The local API needs discovery, collaborator onboarding, and a live contract surface. Swagger/OpenAPI output should identify the contract family, version, status, release date, and whether the surface is read-only.

```text
/swagger
/swagger/v1/swagger.json
/swagger/sp-v1/swagger.json
```

Swagger is the API map. Fiddler is the first API workbench. Contract tests come after enough endpoint behavior deserves to be pinned down.

## Initial REST Validation Strategy

The first PluralBridge REST service should be validated with Fiddler before a dedicated command-line client is written. During early service development, the API contract is still expected to change. Fiddler provides a direct way to compose requests, inspect JSON responses, verify status codes, examine headers, and confirm error behavior without coupling validation work to an early client implementation.

Fiddler record/playback should be used as an early service-development aid. Known-good request sequences can be captured and replayed while the service implementation is being refactored. This provides a practical check that endpoint behavior, response shape, headers, and error handling remain stable before the command-line client or full automated contract-test suite exists.

```text
service -> Swagger/OpenAPI -> Fiddler Composer -> Fiddler record/playback -> contract tests -> CLI
```

### Fiddler Capture to Test Seed

Fiddler captures should seed tests, not blindly become tests. Useful captures should be curated into representative request/response cases that protect route contracts, JSON shape, read-only behavior, error behavior, auth behavior, and version/deprecation behavior.

```text
Fiddler capture -> curated examples -> contract tests -> regression tests
```

Preserve method, route, query string, required headers, expected status code, expected content type, expected response shape, important fields, and error behavior.

Treat these as API contract or integration tests rather than narrow unit tests.

Do not convert every captured session into a test; keep only cases that protect a design promise.

## First Vertical Slice: GET /sp/v1/me

The first endpoint should be `GET /sp/v1/me`. This endpoint is small enough to build quickly but large enough to prove the full service path from HTTP request to SQL Server query and JSON response.

```text
GET /sp/v1/me
```

This first vertical slice proves:

- Service starts
- Routing works
- Swagger sees the endpoint
- Fiddler can call it
- SQL Server connection works
- Data layer can read from `dbo.me`
- Business layer maps database output to API JSON
- Controller returns the response
- Error handling works when the row is missing

### Suggested Implementation Sequence

- Create solution/project skeleton.
- Add configuration and localhost binding.
- Add Swagger/Swashbuckle.
- Add HealthController.
- Add MeController.
- Add IMeRepository.
- Add SqlServerMeRepository.
- Add MeService.
- Map `dbo.me` to `MeDto`.
- Validate with Fiddler.
- Capture Fiddler request/response.
- Convert the captured behavior into a contract/integration test.

### Endpoint Expansion After /me

```text
GET /sp/v1/me
GET /sp/v1/user/{uid}
GET /sp/v1/members/{uid}
GET /sp/v1/groups/{uid}
GET /sp/v1/customFields/{uid}
GET /sp/v1/privacyBuckets
GET /sp/v1/frontHistory
GET /sp/v1/frontHistory/{uid}?startTime=...&endTime=...
GET /sp/v1/notes/{uid}/{memberId}
GET /sp/v1/avatars
```

## Initial Endpoint Shape

The first service should support a read-only compatibility-shaped route set first. A native PluralBridge route set can be added after the compatibility surface is proven.

### Compatibility-Shaped Routes

```text
GET /sp/v1/me
GET /sp/v1/user/{uid}
GET /sp/v1/members/{uid}
GET /sp/v1/groups/{uid}
GET /sp/v1/frontHistory
GET /sp/v1/frontHistory/{uid}?startTime=0&endTime=9999999999999
GET /sp/v1/notes/{uid}/{memberId}
```

### Native PluralBridge Routes

```text
GET /api/v1/members
GET /api/v1/members/{memberId}
GET /api/v1/members/{memberId}/notes
GET /api/v1/members/{memberId}/avatar
GET /api/v1/front-history
GET /api/v1/manifest
```

### Health Endpoints

```text
GET /health
GET /health/database
```

## Read-Only First Version

The first service version should be explicitly read-only. GET and HEAD may be supported. PUT, POST, PATCH, and DELETE should return 405 Method Not Allowed unless and until those operations are deliberately designed.

```text
First API version: read-oriented.
Write operations grow only from concrete workflows.
```

## Configuration Model

The service needs configuration from the beginning, even if the first configuration set is small. Commit example files only. Real local configuration files must be ignored.

```json
{
  "PluralBridgeService": {
    "BindUrl": "http://127.0.0.1:5217",
    "Backend": "SqlServer",
    "ReadOnly": true,
    "AllowLanBinding": false,
    "RequireLocalToken": false
  },
  "ConnectionStrings": {
    "PluralBridgeSqlServer": "Server=localhost;Database=PluralBridge;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

```text
Commit:
  appsettings.example.json
  appsettings.Development.example.json

Ignore:
  appsettings.Local.json
  appsettings.Development.json
  *.user
  .env
```

## Data Access Boundary

The API and business layers should not depend directly on SQL Server table layout. The data layer should expose repository interfaces and storage-neutral records. SQL Server, SQLite, Azure, and AWS backends can then implement the same contracts.

```csharp
public interface IMeRepository
{
    Task<MeRecord?> GetMeAsync(CancellationToken ct);
}

public interface IMemberRepository
{
    Task<IReadOnlyList<MemberRecord>> GetMembersAsync(string systemUid, CancellationToken ct);
    Task<MemberRecord?> GetMemberAsync(string memberId, CancellationToken ct);
}
```

Where stable SQL Server views exist, the service should prefer those views for higher-level response shapes. Tables can be used directly where necessary, but composite responses should eventually sit on views or clear repository queries.

## Console Client Direction

The command-line client remains important, but it should follow the first service slice and Fiddler validation. The client should be a consumer of a known draft contract, not the tool used to discover the contract.

```text
pbcli members list
pbcli member get <member-id>
pbcli fronts list
pbcli notes list <member-id>
pbcli avatars list

pbcli shell
PluralBridge> members list
PluralBridge> member get <id>
PluralBridge> notes list <id>
PluralBridge> fronts recent
PluralBridge> quit
```

A C# validation client is likely the fastest first cut after the service stabilizes. A later small C++ reference client has value because it proves the REST layer is language-neutral and helps future Windows/Linux developers.

## Testing Philosophy

PluralBridge tests should be contract-centered. The goal is to verify that the system behaves according to its design boundaries: exported data is preserved safely, generated files are named and mapped correctly, safety checks prevent accidental leakage, SQL/import scripts remain ordered and usable, REST endpoints return documented JSON shapes, and the service remains read-only until write operations are deliberately designed.

Tests should avoid line-by-line implementation policing. They should not exist to chase coverage percentages or duplicate trivial code behavior. A useful test protects a design promise, a public contract, a data transformation, a safety boundary, or a historically fragile behavior.

### Useful Test Targets

- Exporter creates the expected preservation layout.
- Manifest entries map exported files to their source endpoints.
- Notes remain decorrelated from member/decorator names in filenames.
- Avatar manifest maps member IDs to local files.
- Safety check rejects tokens, private paths, export folders, database files, and unintended images.
- SQL scripts exist in the required order.
- REST service returns the documented JSON shape.
- REST service is read-only for the first version.
- REST service does not require or use a Simply Plural token.
- Data layer can be swapped behind repository interfaces.
- Fiddler captures can be curated into API contract/integration tests.

### Initial Test Groups

```text
tests/
  ExporterContractTests
  ManifestContractTests
  NotesExportTests
  AvatarManifestTests
  SafetyScriptTests
  SqlScriptOrderTests
  RestApiContractTests
  ReadOnlyBoundaryTests
  FiddlerSeededContractTests
```

## REST Contract Versioning

PluralBridge REST interfaces are versioned contracts. During early development, API routes and payloads may change while the design is being shaped. Once a contract version is publicly released, that version becomes immutable while supported. Clients written against a released version must continue to work during its support window.

Breaking changes require a new versioned contract. Existing released contracts may be extended by adding optional fields, optional query parameters, or new endpoints, but existing routes, required parameters, response field names, field meanings, status-code behavior, error shape, identifier formats, and date/time formats must remain stable.

```text
/api/v1/...    PluralBridge native API
/sp/v1/...     Simply Plural/Apparyllis-shaped compatibility API
```

## Contract Lifecycle, Deprecation, and Retirement

A released interface version is immutable while supported, but no interface version is immortal. PluralBridge needs a compatibility promise with a retirement valve so the codebase does not become a cluster of obsolete compatibility paths.

```text
draft -> preview -> stable -> deprecated -> retired
```

Deprecated endpoints should advertise their status through OpenAPI documentation and runtime HTTP headers. Response bodies should not be changed merely to announce deprecation because that can break clients that parse those bodies.

```text
Deprecation: true
Sunset: Wed, 01 Jul 2027 00:00:00 GMT
Link: </docs/api/sp-v2-migration>; rel="successor-version"
```

Older contract versions should be preserved at the HTTP/model adapter layer where feasible, while shared business services and data repositories continue to evolve behind that boundary.

## Security Model

PluralBridge should separate local-service security from future hosted-service identity. The first local REST service is read-only and binds to localhost by default. It does not use the Simply Plural API token. The Simply Plural token is an export credential only and is never treated as a PluralBridge account credential.

### Local Service Defaults

- Bind to 127.0.0.1 / localhost by default.
- Keep the first API version read-only.
- Require explicit opt-in for LAN binding.
- Do not support direct public-internet exposure for the local service.
- Do not use the Simply Plural token for the service.
- Use a PluralBridge-owned local token only when the service is configured to require one, especially for LAN/non-loopback access.

### LAN Binding

LAN binding should require a deliberate configuration change. The service should refuse to bind to a non-loopback address unless LAN binding is explicitly enabled.

```json
{
  "PluralBridgeService": {
    "BindUrl": "http://192.168.1.50:5217",
    "AllowLanBinding": true,
    "RequireLocalToken": true
  }
}
```

### PluralBridge-Owned Local Token

SP_TOKEN is export-only and belongs only to the Simply Plural/Apparyllis export phase. PB_LOCAL_TOKEN is PluralBridge-owned and belongs only to the local PluralBridge REST service. This is a direct credential replacement for service access, not a reuse or reshaping of the Simply Plural token.

For the first implementation, PB_LOCAL_TOKEN may be represented as a PluralBridge-prefixed GUID string. If stronger local bearer-key material is desired while retaining the universal, public GUID shape, two or more GUIDs can be concatenated.

```text
SP_TOKEN          used only by exporter
PB_LOCAL_TOKEN    used only by local PluralBridge REST service

PB_LOCAL_TOKEN=pb_local_<guid>
PB_LOCAL_TOKEN=pb_local_<guid>_<guid>
```

GUIDs are universal and public. Using a GUID-shaped PluralBridge token further decouples the local service credential from any Simply Plural/Apparyllis token format or authentication model.

```text
Authorization: Bearer pb_local_<guid>
```

### Hosted / Cloud Identity Direction

Future hosted PluralBridge services should use a real identity platform. Passkeys/WebAuthn should be the preferred authentication method because they provide phishing-resistant authentication using public-key cryptography. Password-based login may be supported only as a compatibility or recovery path, and should require strong account protection.

- Support multiple passkeys per account.
- Provide recovery codes.
- Provide authenticator/passkey management and revocation.
- Separate authentication from authorization.
- Use short-lived access tokens and refresh-token rotation where appropriate.
- Enforce per-system data ownership checks.

### Authorization Scopes

```text
pluralbridge.read
pluralbridge.write
pluralbridge.export
pluralbridge.import
pluralbridge.admin
```

For the local first cut, only read behavior exists. Write scopes become relevant when PUT, POST, PATCH, or DELETE operations are designed.

### Threat Model for Local Service

- Accidental LAN exposure.
- Accidental internet exposure.
- Malware or a local process reading from localhost.
- Leaked config file.
- Committed local API key.
- Committed connection string.
- Accidental write endpoints.
- Sensitive member/note/avatar data exposed through logs.

### Logging Rule

Logs should support diagnosis without leaking preserved user data.

- Do not log JSON response bodies by default.
- Do not log notes.
- Do not log member names unless debug logging is explicitly enabled.
- Do not log bearer tokens or connection strings.
- Do log request path, method, status code, elapsed time, and correlation ID.

## Outside Collaborator Expectations

This architecture tells collaborators where to plug in. The service boundary is the work surface. Contributors should not couple new tools to local SQL scripts unless they are working specifically on storage/import tasks.

- Client tools should call the PluralBridge REST service.
- Importer tools may read exported JSON/notes/avatar files when that is their explicit job.
- Database-specific work belongs behind storage interfaces.
- API consumers should target documented contract versions.
- Stable contracts should be treated as promises while they remain supported.
- Early collaborators should expect draft contracts to move until a stable API release is declared.

## Near-Term Engineering Tasks

- Create the C# service solution and project skeleton.
- Add Swashbuckle/OpenAPI.
- Add localhost-only read-only health endpoints.
- Implement the first vertical slice: `GET /sp/v1/me`.
- Validate `/health` and `/sp/v1/me` with Fiddler.
- Capture known-good Fiddler request/response pairs.
- Curate captures into API contract/integration test seeds.
- Add SQL Server repository interfaces and first implementations.
- Add configuration examples and Git ignore rules for local config.
- Add PB_LOCAL_TOKEN handling for non-loopback/LAN mode if enabled.
- Add safety-script checks for PB_LOCAL_TOKEN, Authorization headers, unsafe config, and connection strings.
- Add the next compatibility-shaped GET endpoints after `/me` is stable.
- Create the command-line client after the first endpoint set and error model stabilize.

## Guiding Principle

Preserve data first. Put a service boundary around the preserved data. Validate the boundary with Swagger and Fiddler. Prove one full vertical slice. Stabilize the contract. Then build clients and storage backends around that contract.
