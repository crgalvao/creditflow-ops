# ADR 0002: Use SNS and SQS over Direct Lambda Invocation

## Status

Accepted

## Context

The API could directly invoke a decision worker after creating a loan application.

Direct invocation would be simpler, but it would couple the API to the worker and make fanout, buffering, retries, and DLQs less explicit.

## Decision

Use SNS as the domain event topic and SQS queues as durable subscriptions for async consumers.

## Consequences

Positive:
- Decouples the API from workers.
- Allows fanout to multiple consumers.
- Provides durable buffering.
- Supports retry and DLQ behavior.
- Makes the event-driven architecture explicit.

Negative:
- Adds more AWS resources.
- Requires event schema discipline.
- Requires local SNS/SQS simulation or AWS-based testing.
