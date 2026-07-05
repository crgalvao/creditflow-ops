# validate.sh
#!/usr/bin/env bash
set -euo pipefail

echo "Checking whitespace..."
git diff --check

echo "Verifying frontend dependencies..."
pnpm --dir src/frontend/creditflow-web install --frozen-lockfile --prefer-offline

echo "Validation completed successfully."
