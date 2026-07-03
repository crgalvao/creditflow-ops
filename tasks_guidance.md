# CreditFlow Ops — Final Task Execution Order

Use this as the **actual execution order**, regardless of GitHub issue number.

The GitHub issues were created in a reasonable order, but this sequence is better for execution because it prioritizes a working local vertical slice before AWS deployment and authentication complexity.

---

## Phase 1 — Local Foundation

### 1. #1 — Initialize monorepo and CI skeleton

Start here. Everything depends on this.

**Goal:** Create the initial repository structure, .NET solution, React app folder, CDK folder, `.gitignore`, and basic GitHub Actions workflow.

---

### 2. #2 — Create docs skeleton and ADR stubs

Do this early, but keep it lightweight. Do not polish yet.

**Goal:** Create the documentation structure so architecture decisions can be captured as the project evolves.

---

### 3. #3 — Add initial Mermaid diagrams

Draft only. Update later after implementation.

**Goal:** Add initial architecture, sequence, event flow, and user flow diagrams.

---

### 4. #4 — Configure Docker Compose local services

**Goal:** Add local services for DynamoDB Local and LocalStack.

This enables local development without requiring AWS from the beginning.

---

### 5. #5 — Add local resource and seed scripts

**Goal:** Create scripts for local DynamoDB table setup, SNS topic, SQS queues, subscriptions, and seed data.

After this, the local backend foundation should be ready.

---

## Phase 2 — Core Domain and Persistence

### 6. #10 — Implement domain model and status lifecycle

**Goal:** Define the core business model:

- Loan application
- Borrower
- Decision
- Timeline event
- Application statuses
- Valid status transitions

---

### 7. #11 — Implement validation and decision rules

**Goal:** Add validation rules and deterministic scoring logic.

This keeps business logic simple while still giving the project a realistic workflow.

---

### 8. #12 — Implement DynamoDB key builder and repository

**Goal:** Implement DynamoDB persistence for:

- Application metadata
- Timeline events
- Decision records
- Application listing
- Application detail retrieval

---

### 9. #13 — Implement idempotency store

**Goal:** Add idempotency for `POST /applications`.

This is important because duplicate submissions are realistic in financial workflows.

---

### 10. #14 — Implement error handling and correlation middleware

**Goal:** Add consistent error responses and correlation ID propagation.

Every API response and log should be traceable through a correlation ID.

---

## Phase 3 — Messaging and API Vertical Slice

### 11. #17 — Implement SNS event publisher and envelope

**Goal:** Create the domain event envelope and SNS publisher abstraction.

This enables the API to publish events without knowing about workers directly.

---

### 12. #15 — Implement create application endpoint

**Goal:** Build `POST /applications`.

The endpoint should:

- Validate the request
- Enforce idempotency
- Store the application as `Submitted`
- Write a timeline event
- Publish `LoanApplicationSubmitted`
- Return `202 Accepted`

---

### 13. #16 — Implement list detail timeline endpoints

**Goal:** Build:

- `GET /applications`
- `GET /applications/{id}`
- `GET /applications/{id}/timeline`

At this point, you should be able to create an application locally through the API and retrieve it.

---

## Phase 4 — Async Processing

### 14. #18 — Implement Decision Worker processing flow

**Goal:** Create the .NET SQS worker that consumes `LoanApplicationSubmitted`.

The worker should:

- Mark the application as `Processing`
- Run decision rules
- Store the decision
- Update the final status
- Publish completion or failure events

---

### 15. #19 — Implement worker idempotency retry partial batch

**Goal:** Make the worker retry-safe.

The worker should handle:

- Duplicate messages
- Conditional updates
- Partial batch failures
- Retryable failures
- Non-retryable domain failures

---

### 16. #20 — Implement Audit Worker minimal persistence

**Goal:** Create a minimal audit worker that consumes domain events and stores immutable audit records.

This is useful, but not as critical as the Decision Worker. If time gets tight, reduce this task.

At this point, you should have the core backend story working:

```text
API → DynamoDB → SNS → SQS → Worker → DynamoDB status update
```

---

## Phase 5 — Frontend Local Demo

### 17. #23 — Scaffold React app routes layout

**Goal:** Create the Vite React app structure, routing, layout, and placeholder pages.

---

### 18. #24 — Build typed API client DTOs

**Goal:** Create frontend DTOs and a centralized API client.

The API client should handle:

- Base URL
- Authorization header
- Correlation ID
- Error handling

---

### 19. #25 — Build create application form

**Goal:** Implement the loan application form.

The form should submit to the backend, handle the `202 Accepted` response, and redirect to the detail page.

---

### 20. #26 — Build dashboard list and status badges

**Goal:** Implement the dashboard list.

The dashboard should show:

- Borrower name
- Requested amount
- Status
- Created date
- Updated date
- Link to detail page

---

### 21. #27 — Build detail timeline polling view

**Goal:** Implement the detail page with async progress tracking.

The page should:

- Show borrower data
- Show current status
- Show decision result and reasons
- Show timeline events
- Poll while status is `Submitted` or `Processing`
- Stop polling when the status is final

At this point, you should have the local end-to-end demo.

---

## Phase 6 — Infrastructure and Authentication

Do this after the local flow works. Otherwise, AWS setup can consume your time too early.

### 22. #6 — Create CDK app skeleton

**Goal:** Create the CDK TypeScript app and stack structure.

---

### 23. #7 — Define DynamoDB table and GSIs in CDK

**Goal:** Define the DynamoDB table, primary key, GSIs, on-demand billing, and TTL.

---

### 24. #8 — Define SNS SQS queues and DLQs in CDK

**Goal:** Define:

- SNS domain events topic
- Decision queue
- Audit queue
- DLQs
- Subscriptions
- Filter policy for decision events

---

### 25. #9 — Define Lambda API Gateway Cognito resources in CDK

**Goal:** Define:

- API Lambda
- Worker Lambdas
- API Gateway HTTP API
- Cognito User Pool
- JWT authorizer
- IAM roles
- Environment variables

---

### 26. #21 — Configure Cognito JWT authorization path

**Goal:** Connect API Gateway JWT authorization and backend user claim extraction.

Local development should still support auth-disabled mode.

---

### 27. #22 — Add frontend auth token handling

**Goal:** Implement Cognito Hosted UI integration in the frontend.

The frontend should:

- Redirect to login
- Handle the auth callback
- Store token state
- Attach the token to API calls
- Support logout

---

## Phase 7 — Tests and Observability

### 28. #28 — Add domain and API tests

**Goal:** Add tests for:

- Validation
- Scoring
- Status transitions
- Idempotency
- API happy path
- API validation errors

---

### 29. #29 — Add repository and worker tests

**Goal:** Add tests for:

- DynamoDB repository behavior
- Worker processing
- Duplicate messages
- Partial batch failure
- Status updates

---

### 30. #30 — Add frontend build smoke and API samples

**Goal:** Add:

- Frontend build validation
- Basic frontend smoke check
- `.http` API sample files

---

### 31. #31 — Add structured logs metrics correlation docs

**Goal:** Add structured logging, correlation ID propagation, and debugging documentation.

This is important for the senior-level portfolio story because async systems must be observable.

---

## Phase 8 — GitHub Polish and Final Demo

### 32. #32 — Complete README setup deployment docs

**Goal:** Finish the README.

It should explain:

- What the project is
- Architecture
- Local setup
- AWS deployment
- Testing
- Cost warning
- Destroy/cleanup instructions

---

### 33. #33 — Complete ADRs event samples data model docs

**Goal:** Finish ADRs and examples.

Include:

- DynamoDB over DocumentDB
- SNS/SQS over direct invocation
- Cognito-only auth
- React/Vite
- Focused .NET architecture
- Event samples
- DynamoDB item examples

---

### 34. #34 — Final demo run screenshots cost disclaimer

**Goal:** Run the full flow and capture portfolio materials.

Capture:

- Dashboard screenshot
- Create form screenshot
- Detail timeline screenshot
- CloudWatch/correlation log screenshot if deployed

Also verify:

- Cost disclaimer
- Cleanup instructions
- Demo flow

---

## Critical Path

These tasks matter most:

```text
#1
#4
#5
#10
#11
#12
#13
#17
#15
#16
#18
#19
#23
#24
#25
#26
#27
#32
#34
```

If time gets tight, prioritize these.

---

## Tasks You Can Cut or Reduce If Needed

Reduce scope in this order:

```text
#20 Audit Worker minimal persistence
#22 Frontend auth token handling
#21 Cognito JWT authorization path
#31 Custom metrics/docs depth
#33 ADR polish depth
```

Do **not** cut:

```text
DynamoDB
SNS
SQS
Decision Worker
React frontend
README
End-to-end demo
```

---

## Best First Issues to Move to In Progress

Start exactly here:

```text
#1 Initialize monorepo and CI skeleton
#2 Create docs skeleton and ADR stubs
#3 Add initial Mermaid diagrams
#4 Configure Docker Compose local services
#5 Add local resource and seed scripts
```

Then move into:

```text
#10 Implement domain model and status lifecycle
#11 Implement validation and decision rules
#12 Implement DynamoDB key builder and repository
```

This is the cleanest path to a working project without getting trapped in AWS setup too early.

---

## Working Rule

For every issue:

1. Move the issue to **In Progress**.
2. Create a branch.
3. Implement the work.
4. Open a pull request.
5. Add `Closes #issue-number` to the PR body.
6. Merge.
7. Confirm the issue moved to **Done** or close it manually.

Recommended branch naming:

```text
task/initialize-monorepo
task/dynamodb-repository
task/decision-worker
task/react-application-detail
docs/readme-architecture
```

This creates a clean portfolio trail:

```text
Issue → Branch → Pull Request → Merge → Done
```
