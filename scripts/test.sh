#!/usr/bin/env bash
set -euo pipefail

echo "Running backend tests..."
dotnet test src/backend/CreditFlow.slnx

echo "Running frontend lint..."
pnpm --dir src/frontend/creditflow-web lint

echo "Tests completed."
