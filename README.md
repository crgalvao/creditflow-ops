# CreditFlow Ops

Serverless credit decision workflow using .NET, React, AWS Lambda, DynamoDB, SNS, SQS, and Cognito.

## Purpose

CreditFlow Ops is a senior-level portfolio project that demonstrates backend engineering, cloud-native architecture, event-driven processing, DynamoDB modeling, authentication, observability, and pragmatic technical trade-offs.

## Planned Stack

- Backend: .NET 10 / C#
- Frontend: React + TypeScript + Vite
- Cloud: AWS Lambda, API Gateway, DynamoDB, SNS, SQS, Cognito, CloudWatch
- Infrastructure: AWS CDK v2 TypeScript
- Testing: xUnit, frontend build checks

## Repository Structure

```text
src/
  backend/
  frontend/
  infrastructure/
docs/
samples/
scripts/
```

## Root Commands

```bash
make build        # Build backend and frontend
make test         # Run backend tests and frontend lint
make validate     # Full pre-PR validation
make local-reset  # Hard reset local environment and storage
make local-init   # Start local services and seed local data
```
