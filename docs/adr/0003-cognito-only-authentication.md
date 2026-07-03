# ADR 0003: Use Cognito-Only Authentication

## Status

Accepted

## Context

The project considered using Google Auth integrated through Cognito.

Google Auth improves user experience but adds OAuth setup, callback configuration, secret management, and documentation overhead.

## Decision

Use Cognito Hosted UI with Cognito-native users for the MVP and showcase version.

## Consequences

Positive:
- Fastest secure authentication path.
- AWS-native.
- Enough to demonstrate JWT-secured APIs.
- Avoids external OAuth setup.

Negative:
- Less polished than Google sign-in.
- Requires demo user setup.
