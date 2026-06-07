# Cloud Step 17B Azure Portal Setup Audit

Date: 2026-06-06 PT

## Checkpoint

- Branch: feature/002-cloud-readiness
- Local HEAD entering Azure setup: 8a38e90
- Remote branch before later push: origin/feature/002-cloud-readiness at a5a6dc1
- Repository/SSMS work paused because no Azure SQL target database existed yet.

## Azure resources

- Subscription shown: Azure subscription 1
- Resource group: rg-pluralbridge-cloudproof
- Database: PluralBridgeCloudProof001
- Logical server: pluralbridge-cloudproof-syf001
- Server endpoint: pluralbridge-cloudproof-syf001.database.windows.net
- Region: West US 2
- East US rejected by subscription; West US 2 accepted.

## Configuration summary

- Authentication: SQL authentication
- Admin login: pbadmin
- Password: excluded from repository and audit text
- Workload: Development
- Elastic pool: No
- Compute: General Purpose, Serverless, Standard-series Gen5, 1 max vCore, 0.5 min vCore
- Auto-pause: enabled, 1 hour
- Data max size: 32 GB
- Zone redundant: No
- Backup redundancy: Locally-redundant backup storage
- Data source: Blank
- Collation: SQL_Latin1_General_CP1_CI_AS

## Networking and security

- Connectivity: Public endpoint
- Allow Azure services: No
- Client IP added in create flow: 169.197.57.232
- Minimum TLS: 1.2
- Microsoft Defender for SQL: Not now
- TDE: service-managed key
- Always Encrypted secure enclaves: Off

## Result and next action

- Azure portal reported deployment succeeded.
- Database overview page loaded for PluralBridgeCloudProof001.
- Next: verify server firewall rule, then connect SSMS to PluralBridgeCloudProof001.
