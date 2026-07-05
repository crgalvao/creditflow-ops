# Event Samples

Local domain-event envelope samples for the CreditFlow Ops event-driven flow.

Flow:

```text
LoanApplicationSubmitted
→ KycCheckRequested
→ KycCompleted / KycFailed / KycNeedsReview
→ CreditAssessmentRequested
→ CreditAssessmentCompleted
→ CreditProfileUpserted
→ LoanDecisionCompleted
```
