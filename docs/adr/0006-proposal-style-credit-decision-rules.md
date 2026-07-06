# ADR 0006: Proposal-Style Credit Decision Rules

## Status

Accepted

## Context

CreditFlow needs a realistic credit decision flow for a fintech-style loan application system.

Legacy proposal-generation logic used external data such as credit bureau scores, SCR history, debt indicators, tax health, activity levels, and partner restrictions. Those inputs are realistic, but adding them to the MVP would significantly increase scope and require modeling external data providers.

For the current portfolio version, the system should demonstrate strong backend architecture without depending on external credit-data integrations.

## Decision

Use deterministic proposal-style credit decision rules based only on data currently available in the loan application flow:

- Borrower months in business
- Requested amount
- Monthly revenue
- Currency
- KYC outcome

The rules estimate:

- Credit score
- Proposal limit
- Base monthly rate
- Eligible products
- Decision reasons

External bureau/SCR/tax-health data will be treated as a future extension through a dedicated external credit data snapshot.

## Consequences

### Positive

- Keeps the MVP local-first and deterministic.
- Avoids external API dependencies.
- Makes tests simple and reliable.
- Supports Postman/OpenAPI demo flows.
- Preserves a clean path for future external credit data integration.

### Negative

- The current scoring model is simplified.
- The system does not yet model bureau risk, SCR debt exposure, tax status, or partner restrictions.
- Proposal limits are approximations, not production underwriting decisions.

## Future extension

A future version may introduce:

- ExternalCreditDataSnapshot
- Bureau score
- SCR debt history
- Current short-term debt
- Tax health
- Activity level
- Partner restriction indicators
- Cost-aware external data fetching policy
