# ADR 0004: Use React and Vite over Next.js

## Status

Accepted

## Context

The frontend is a dashboard SPA for creating and tracking credit applications.

It does not require SSR, server-side routing, or backend-for-frontend behavior.

## Decision

Use React, TypeScript, Vite, and pnpm.

## Consequences

Positive:
- Fast setup.
- Simple deployment as static assets.
- Lower complexity.
- Good fit for dashboard UI.

Negative:
- No built-in SSR.
- No file-based routing by default.
