# ADR 0001: Use DynamoDB over DocumentDB

## Status

Accepted

## Context

The project originally considered Amazon DocumentDB because loan applications are document-shaped and DocumentDB aligns with MongoDB-style modeling.

However, this portfolio version prioritizes low cost, serverless-first deployment, simpler local development, and avoiding VPC/networking overhead.

## Decision

Use DynamoDB as the primary database.

## Consequences

Positive:
- Lower operational cost for a portfolio project.
- Serverless-first architecture.
- No always-on database cluster.
- No VPC requirement for the database path.
- Good fit for known access patterns.

Negative:
- Requires access-pattern-first modeling.
- Requires denormalization.
- Less natural than a document database for nested aggregate storage.
