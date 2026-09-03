# Tasks.FrontEnd

## Frontend Source of Truth

- Edit frontend code only in `app/src/**`.
- Do not manually edit runtime mirror files in `app/**` (outside `app/src`).
- Do not manually edit generated files in `api/PluralBridge.Api/PluralBridge.Api/wwwroot/app/**`.

## Build Automation

`dotnet build` and `dotnet publish` now run:

1. Frontend sync to `wwwroot/app`
2. Sync drift validation
3. Program allowlist validation

Build fails if any check fails.

## Contributor Quick Checklist

Before pushing frontend changes:

- Edit only `app/src/**`
- Run `dotnet build api/PluralBridge.Api/PluralBridge.Api/PluralBridge.Api.csproj`
- Confirm build output includes:
  - `SYNC_BROWSER_APP_OK`
  - `SYNC_CHECK_OK`
  - `ALLOWLIST_CHECK_OK`

## Migration Notes

Legacy mirror files remain temporarily for transition, but are not development sources.
