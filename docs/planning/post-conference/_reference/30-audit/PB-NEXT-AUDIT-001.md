# PB-NEXT-AUDIT-001 — Payload-Free Audit Event Schema With Authority References

Status: Planning draft  
Parent: PB-NEXT-001  
Depends on: `BLUEPRINT_CROSSWALK.md`, `PB-NEXT-DATA-001.md`, `PB-NEXT-USER-001.md`, `PB-NEXT-AUTHZ-001.md`, `PB-NEXT-REB-001.md`, `PB-NEXT-CONSENT-001.md`  
Scope: Audit event model, authority references, payload minimization, diagnostic-log separation, retention, and evidence integrity before implementation.  
Architecture feedback source: public r/softwarearchitecture comments by `u/bartekus`, June 2026.

## 1. Purpose

This document captures the sixth PB-NEXT foundation lane: how PluralBridge should model audit evidence before protected writes, import/export, support access, provider access, consent revocation, account closure, or System closure are implemented.

Audit records should answer:

1. Who acted?
2. What action occurred?
3. Which resource was affected?
4. Which System scoped the resource?
5. Under what authority was the action allowed or denied?
6. Which membership, relationship, consent, policy, or break-glass version applied?
7. When did the event occur?

Audit must remain separate from diagnostic logging.

Audit must not become a second copy of private System payload.

## 2. Working Decision

PluralBridge should use payload-free audit records with stable resource IDs and authority references.

Audit events should reference:

1. Actor identity
2. System identity
3. Resource identity
4. Action
5. Authorization decision result where applicable
6. Membership authority
7. Relationship authority
8. Consent authority
9. Policy version
10. Processing purpose
11. Audit classification
12. Timestamp

Audit events should avoid copying:

1. Raw Simply Plural export payload
2. Member descriptions
3. Notes
4. Private front-history content
5. Avatar content
6. Consent text
7. Private resource payload
8. Sensitive diagnostic details

## 3. Audit Versus Diagnostic Logging

Audit and diagnostic logging are separate concerns.

Diagnostic logs help operators debug application behavior.

Audit records preserve security, authority, policy, consent, and evidence facts.

Diagnostic logs may include:

1. request IDs
2. timing
3. exception summaries
4. service health
5. job status
6. non-sensitive operational metadata

Audit records may include:

1. stable actor ID
2. stable System ID
3. stable resource ID
4. action name
5. decision result
6. authority references
7. policy version
8. consent version
9. relationship version
10. processing purpose
11. timestamp

Diagnostic logs should not become the audit system.

Audit records should not become debugging dumps.

## 4. Candidate Audit Event Shape

Candidate fields:

1. `AuditEventId`
2. `SystemId`
3. `ActorType`
4. `ActorId`
5. `Action`
6. `ResourceType`
7. `ResourceId`
8. `ResourceStableId`
9. `Decision`
10. `ReasonCode`
11. `PolicyVersion`
12. `MembershipId`
13. `RelationshipIds`
14. `ConsentId`
15. `ConsentVersion`
16. `ConsentArtifactVersion`
17. `ProcessingPurpose`
18. `SupportAccessGrantId`
19. `BreakGlassRecordId`
20. `ImportBatchId`
21. `ExportPackageId`
22. `RequestId`
23. `CorrelationId`
24. `OccurredAt`
25. `RecordedAt`
26. `AuditClassification`
27. `HashPrevious`
28. `HashCurrent`

Hash fields are optional planning fields. They should be used only if tamper evidence becomes a requirement.

## 5. Stable Identity Requirements

Audit records depend on stable identities.

Required stable identity categories:

1. Account or actor ID
2. System ID
3. Resource ID
4. Membership ID
5. Relationship ID
6. Consent ID
7. Policy version
8. Import batch ID
9. Export package ID
10. Support access grant ID
11. Break-glass record ID

Audit records should remain understandable after names, display labels, descriptions, avatars, or private payload fields change.

## 6. Authority References

Authority references explain why an action was allowed or denied.

Candidate authority references:

1. Account lifecycle state
2. System lifecycle state
3. Membership ID and status
4. Relationship ID and status
5. Consent ID and version
6. Policy version
7. Processing purpose
8. Support access grant
9. Break-glass record
10. Import/export job authority

Authority references should be stable enough to explain past decisions after future edits, revocations, closures, or deletions.

## 7. Events To Audit

Audit should focus on state changes, sensitive access, authority changes, and policy-relevant decisions.

Candidate required audit events:

1. Account created
2. Account lifecycle changed
3. System created
4. System lifecycle changed
5. Membership created
6. Membership accepted
7. Membership revoked
8. Relationship created
9. Relationship changed
10. Relationship revoked
11. Consent granted
12. Consent renewed
13. Consent revoked
14. Import started
15. Import committed
16. Import failed
17. Export requested
18. Export generated
19. Support access granted
20. Support access used
21. Break-glass access requested
22. Break-glass access used
23. Protected resource created
24. Protected resource updated
25. Protected resource deleted or marked for deletion
26. Account closure requested
27. System closure requested
28. Erasure or crypto-shredding action completed

## 8. Read Auditing

Audit every read only where the risk, policy, or legal requirement justifies the cost.

Candidate read-audit cases:

1. Support access reads
2. Provider access reads
3. Break-glass reads
4. Export reads
5. Audit-event reads
6. Consent-record reads
7. Sensitive administrative reads

Ordinary first-party reads within an active System may use lower-cost logging or no audit event unless later policy requires otherwise.

The first implementation slice should explicitly decide which reads require audit.

## 9. Audit Classification

Audit events should have a classification that guides retention, review, and alerting.

Candidate classifications:

1. Security
2. Consent
3. Relationship
4. Membership
5. Import
6. Export
7. Support access
8. Break-glass
9. Account lifecycle
10. System lifecycle
11. Resource mutation
12. Administrative
13. Evidence

Classification should support targeted review without querying private payload.

## 10. Reliable Audit Recording

Missing audit events may be unacceptable for some actions.

Candidate reliability patterns:

1. Transactional write of resource mutation and audit event where practical
2. Reliable outbox pattern for events that need asynchronous handling
3. Idempotent audit-event recording
4. Correlation IDs linking request, job, mutation, and audit event
5. Failure handling that blocks protected action when audit recording is mandatory

A reliable outbox pattern should be considered where audit loss would damage evidence integrity.

## 11. Tamper Evidence

Hash chaining may be useful if tamper evidence becomes a requirement.

Candidate hash-chain fields:

1. previous audit event hash
2. current audit event hash
3. chain scope
4. chain sequence
5. hash algorithm version

Hash chaining should be scoped carefully, probably by System or audit partition.

Blockchain is not part of the PluralBridge audit plan.

## 12. Retention, Erasure, And Crypto-Shredding

Audit retention must be compatible with deletion and closure.

Design principles:

1. Keep PII and private payload out of audit records.
2. Keep audit records useful through stable IDs and authority references.
3. Preserve past disclosures as evidence after revocation.
4. Allow future erasure or crypto-shredding of payload records without turning audit into payload storage.
5. Decide which audit references survive account closure or System deletion.
6. Decide which pseudonymous IDs remain after erasure.

Crypto-shredding or key destruction may be useful where payload retention and audit retention have different lifecycles.

## 13. Relationship To Authorization

PB-NEXT-AUTHZ-001 defines the central authorization decision point.

Audit should capture authorization-compatible facts.

Candidate authorization-to-audit fields:

1. subject
2. action
3. resource
4. context summary
5. decision
6. reason code
7. obligations
8. authority references
9. policy version
10. consent version
11. relationship references

Authorization decisions may produce an obligation to write an audit event.

## 14. Relationship To Consent

PB-NEXT-CONSENT-001 defines consent as a policy input.

Audit should record consent authority where consent is required.

Consent-related audit events include:

1. consent grant
2. consent renewal
3. consent revocation
4. consent expiration handling
5. denied action due to missing consent
6. denied action due to revoked consent
7. denied action due to expired consent
8. action allowed under a specific consent version

Audit records should reference consent IDs, artifact versions, and policy versions without copying consent text.

## 15. Relationship To ReBAC

PB-NEXT-REB-001 defines the relationship layer.

Audit should record relationship authority where relationship facts are part of authorization.

Relationship-related audit events include:

1. relationship creation
2. relationship activation
3. relationship suspension
4. relationship expiration
5. relationship revocation
6. relationship supersession
7. action allowed through relationship path
8. action denied due to missing or inactive relationship

Audit records should reference relationship IDs and versions where applicable.

## 16. Relationship To Import And Export

Import and export are processing purposes.

Import audit should capture:

1. who initiated import
2. target System
3. import batch
4. processing purpose
5. consent or authority reference
6. source system identity
7. import state transition
8. commit or failure outcome

Export audit should capture:

1. who requested export
2. target System or resource scope
3. export package
4. recipient or destination reference where appropriate
5. processing purpose
6. consent or authority reference
7. export generation outcome
8. disclosure evidence

Audit records should not copy import payload or export payload.

## 17. Support And Break-Glass Access

Support access and break-glass access require strict audit treatment.

Support audit should capture:

1. support actor
2. support access grant
3. System scope
4. resource scope
5. purpose
6. duration
7. action
8. justification reference
9. decision result
10. timestamp

Break-glass audit should capture:

1. break-glass actor
2. emergency or justification record
3. scope
4. purpose
5. decision result
6. resource affected
7. review obligation
8. timestamp

Support and break-glass records should avoid becoming informal backdoors.

## 18. NT-Style Mental Model

PluralBridge can use an NT-style conceptual frame for audit planning.

Planning analogies:

1. Account resembles a security principal.
2. Stable Account identity resembles a SID-like identifier.
3. System resembles a protected domain or container boundary.
4. Resource resembles a protected object.
5. Relationship and membership resemble trustee and group authority.
6. Central authorization resembles an access check.
7. Audit classification resembles SACL-driven audit policy.
8. Audit event resembles a security event log record.

This analogy keeps audit focused on authority, object identity, and access decisions rather than payload copies.

## 19. First Implementation Gate Contribution

PB-NEXT-AUDIT-001 contributes these gate requirements:

1. Audit records are payload-free.
2. Audit and diagnostic logging remain separate.
3. Audit records use stable actor, System, resource, and authority references.
4. Authorization decisions can emit audit obligations.
5. Consent, relationship, membership, and policy authority can be referenced.
6. Import/export/support/break-glass events have audit paths.
7. Missing audit events are treated as blocking where audit is mandatory.
8. Retention and erasure are compatible because audit avoids private payload.
9. Hash chaining remains optional and scoped to tamper-evidence needs.
10. Blockchain is out of scope.

## 20. Open Design Questions

1. What is the smallest audit table needed for the first implementation slice?
2. Which first-slice actions require audit?
3. Which reads require audit?
4. Which denied actions require audit?
5. Which audit events are blocking if recording fails?
6. Which authority references are required in the first slice?
7. Which audit classifications are required immediately?
8. How long should audit records be retained?
9. Which audit records survive account closure?
10. Which audit records survive System deletion?
11. Which audit fields require pseudonymous identifiers?
12. Which fields need hash chaining, if any?
13. Which audit records need outbox delivery?
14. Which diagnostic logs must be scrubbed to avoid audit/payload leakage?
15. How should audit reads themselves be audited?

## 21. Explicit Non-Goals

Out of scope for this document:

1. Implementing audit tables
2. Implementing diagnostic logging
3. Implementing authorization code
4. Implementing consent schema
5. Implementing ReBAC
6. Implementing import/export behavior
7. Implementing support access
8. Implementing break-glass access
9. Implementing account closure
10. Implementing System deletion
11. Azure runtime changes
12. Website changes
13. Release promotion
14. Blockchain

Those decisions belong to later PB-NEXT planning documents and implementation slices.
