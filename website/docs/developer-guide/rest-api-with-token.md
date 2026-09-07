# API Endpoint Reference

This page documents the current PluralBridge API surface in Swagger-style groups.

Click an endpoint in the table to expand usage and programming examples for that row.

Base behavior:

- Response format: JSON
- Correlation header supported: `X-Correlation-ID`
- Protected endpoints require authenticated session/cookie unless noted otherwise
- Route parameters:
  - `{systemId}` = system GUID
  - `{memberId}` = member GUID

## Account

Base route: `/api/account`

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| POST | `/api/account/register` | Anonymous | Start account registration. |
| POST | `/api/account/verify-registration` | Anonymous | Verify registration with code. |
| POST | `/api/account/login` | Anonymous | Sign in and create session. |
| POST | `/api/account/forgot-username` | Anonymous | Start username recovery flow. |
| POST | `/api/account/forgot-password` | Anonymous | Start password reset flow. |
| POST | `/api/account/reset-password` | Anonymous | Complete password reset with code. |
| POST | `/api/account/change-password` | Anonymous | Change password using validated request flow. |
| PUT | `/api/account/profile` | Protected | Update account profile values. |
| PUT | `/api/account/contact` | Protected | Update account contact destination. |
| POST | `/api/account/verify-contact` | Protected | Verify contact update with code. |

## Me

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/me` | Protected | Return current access context, resolved system, proof metadata, and table counts. |

## Source Systems

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/source-systems` | Protected | List source application families present in imported data. |

## Systems

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems` | Protected | List systems visible to current account context. |

## Members

Base route: `/api/systems/{systemId}/members`

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/members` | Protected | List members for one system. |
| GET | `/api/systems/{systemId}/members/{memberId}` | Protected | Get one member by id. |
| POST | `/api/systems/{systemId}/members` | Protected | Create one member in the current system context. |
| PUT | `/api/systems/{systemId}/members/{memberId}` | Protected | Update one member in the current system context. |

## Front History

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/front-history` | Protected | Return front-history rows for one system context. |

## Custom Fields

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/custom-fields` | Protected | Return custom-field definitions/values for one system. |

## Privacy Buckets

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/privacy-buckets` | Protected | Return privacy bucket data for one system. |

## Import Batches

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/import-batches` | Protected | Return import batch metadata for one system. |

## Import Metadata

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/import-metadata` | Protected | Return import metadata rows for one system. |

## Source Records

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/source-records` | Protected | Return source record inventory and metadata for one system. |

## Source ID Mappings

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| GET | `/api/systems/{systemId}/source-id-mappings` | Protected | Return source-to-PluralBridge id mapping rows for one system. |

## Notes

- API routes above are derived from current controller routes in `PluralBridge.Api`.
- Endpoint behavior remains contract-governed and versioning rules apply as the public contract evolves.
