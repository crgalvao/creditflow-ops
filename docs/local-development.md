# Local Development

This document explains how to run CreditFlow Ops locally.

## Prerequisites

Install:

- Docker
- Docker Compose
- .NET SDK
- Node.js
- pnpm
- AWS CLI, optional but useful for validating local AWS-compatible services

If using WSL, keep the repository inside the Linux filesystem rather than `/mnt/c` for better performance.

## Local Services

CreditFlow Ops uses Docker Compose for local infrastructure dependencies:

- DynamoDB Local on port `8000`
- LocalStack on port `4566` for SNS and SQS

Start services from the repository root:

```bash
docker compose up -d
```

## Local Dependencies

TODO.

## Backend

TODO.

## Frontend

TODO.

## Workers

TODO.

## Seed Data

TODO.

## Troubleshooting

TODO.
