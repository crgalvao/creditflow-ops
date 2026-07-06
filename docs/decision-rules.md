# CreditFlow Decision Rules

CreditFlow uses deterministic proposal-style credit rules for the MVP.

The goal is to approximate a realistic fintech pre-qualification and proposal flow without calling external KYC, bureau, SCR, or tax-data providers.

## Principles

- Reject obviously bad requests early.
- Avoid expensive downstream checks when the application is clearly not viable.
- Separate automatic approval, manual review, rejection, and workflow failure.
- Explain decisions through reasons, not only statuses.
- Keep rules deterministic and testable.

## Pre-qualification rules

The API validates applications before creating the loan application.

Current pre-qualification checks:

- Owner user ID is required.
- Borrower legal name is required.
- Borrower tax ID is required.
- Borrower industry is required.
- Borrower must have enough operating history.
- Requested amount must be within supported bounds.
- Monthly revenue must be within supported bounds.
- Currency must be a 3-letter uppercase code.
- Requested amount must not be obviously excessive relative to annual revenue.

These checks are intentionally cheap. They use only request data and avoid external lookups.

## Credit decision inputs

The credit decision engine currently uses:

- Requested amount
- Monthly revenue
- Currency
- Borrower months in business
- KYC status

The engine does not currently use:

- Serasa score
- SCR history
- Current debt
- Tax health
- Partner restrictions
- External bureau data

Those can be added later through an external credit data snapshot without changing the core loan flow.

## Decision outcomes

### Approved

The application can be automatically approved when:

- KYC is verified.
- Requested amount is within the estimated proposal limit.
- Score is high enough.
- No manual review condition is active.

### ManualReview

The application goes to manual review when:

- KYC needs review.
- Requested amount exceeds the estimated proposal limit but a smaller proposal may be viable.
- Score is medium.
- Business/revenue/affordability indicators are mixed.

KYC needing review must never produce automatic approval.

### Rejected

The application is rejected when:

- KYC failed.
- Requested amount is too high relative to annual revenue.
- Estimated proposal limit is below the minimum product amount.
- Score is too low.

### Failed

Failed is a workflow/system outcome, not a credit score.

Use Failed when the process cannot continue due to a workflow failure, invalid state, unrecoverable processing issue, or failed KYC transition.

## Proposal behavior

The engine estimates:

- Proposal limit
- Base monthly rate
- Eligible products
- Reasons explaining the result

When the requested amount is too high but the borrower is not clearly bad, the engine should prefer ManualReview with a lower proposal instead of a silent rejection.
