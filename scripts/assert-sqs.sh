#!/usr/bin/env bash
set -euo pipefail

echo -e "\n========================================"
echo "Verifying SQS seed data..."
echo -e "========================================\n"

# 1. Safely source the environment variables
if [ -f .local/local-resources.env ]; then
  source .local/local-resources.env
else
  echo "❌ Error: .local/local-resources.env not found. Did local-init run?"
  exit 1
fi

# 2. Fetch the message from SQS
OUTPUT=$(AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws sqs receive-message \
  --queue-url "$KYC_QUEUE_URL" \
  --endpoint-url "$LOCALSTACK_SERVICE_URL" \
  --region "$AWS_REGION")

# 3. Validate the output
if echo "$OUTPUT" | grep -q '"Messages"'; then
  echo "✅ Validation Passed: Seed message successfully found in SQS queue."
else
  echo "❌ Validation Failed: No messages found in SQS queue."
  echo "Raw AWS Output:"
  echo "$OUTPUT"
  exit 1
fi
