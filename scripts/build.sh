#!/usr/bin/env bash
set -euo pipefail

echo "Building backend..."
dotnet build src/backend/CreditFlow.slnx

echo "Building frontend..."
pnpm --dir src/frontend/creditflow-web build

echo "Build completed."
