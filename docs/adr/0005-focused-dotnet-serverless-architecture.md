# ADR 0005: Use a Focused .NET Serverless Architecture

## Status

Accepted

## Context

The initial idea considered multiple backend languages and multiple frontend stacks.

Within a 30-hour implementation window, that would increase context switching, CI/CD complexity, local setup complexity, and risk of an unfinished project.

## Decision

Use a focused .NET backend with React frontend and AWS serverless services.

## Consequences

Positive:
- Higher chance of completion.
- Better implementation quality.
- Clearer portfolio story.
- Strong alignment with senior backend and cloud architecture roles.

Negative:
- Less polyglot demonstration.
- JVM, Python, and additional frontend stacks are intentionally excluded from MVP.
