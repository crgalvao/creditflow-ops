#!/usr/bin/env bash
set -euo pipefail

export AWS_REGION="${AWS_REGION:-us-east-1}"
export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"

DYNAMODB_ENDPOINT="${DYNAMODB_SERVICE_URL:-http://localhost:8000}"
LOCALSTACK_ENDPOINT="${LOCALSTACK_SERVICE_URL:-http://localhost:4566}"

TABLE_NAME="${DYNAMODB_TABLE_NAME:-CreditFlowLocal}"
TOPIC_NAME="${DOMAIN_EVENTS_TOPIC_NAME:-creditflow-domain-events}"

DECISION_QUEUE_NAME="${DECISION_QUEUE_NAME:-creditflow-decision-queue}"
DECISION_DLQ_NAME="${DECISION_DLQ_NAME:-creditflow-decision-dlq}"

KYC_QUEUE_NAME="${KYC_QUEUE_NAME:-creditflow-kyc-queue}"
KYC_DLQ_NAME="${KYC_DLQ_NAME:-creditflow-kyc-dlq}"

CREDIT_QUEUE_NAME="${CREDIT_QUEUE_NAME:-creditflow-credit-queue}"
CREDIT_DLQ_NAME="${CREDIT_DLQ_NAME:-creditflow-credit-dlq}"

AUDIT_QUEUE_NAME="${AUDIT_QUEUE_NAME:-creditflow-audit-queue}"
AUDIT_DLQ_NAME="${AUDIT_DLQ_NAME:-creditflow-audit-dlq}"

mkdir -p .local/tmp

echo "Checking local services..."
aws dynamodb list-tables \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION" >/dev/null

aws sns list-topics \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" >/dev/null

echo "Creating DynamoDB table if needed: $TABLE_NAME"

if aws dynamodb describe-table \
  --table-name "$TABLE_NAME" \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION" >/dev/null 2>&1; then
  echo "Table already exists."
else
  aws dynamodb create-table \
    --table-name "$TABLE_NAME" \
    --attribute-definitions \
      AttributeName=PK,AttributeType=S \
      AttributeName=SK,AttributeType=S \
      AttributeName=GSI1PK,AttributeType=S \
      AttributeName=GSI1SK,AttributeType=S \
      AttributeName=GSI2PK,AttributeType=S \
      AttributeName=GSI2SK,AttributeType=S \
    --key-schema \
      AttributeName=PK,KeyType=HASH \
      AttributeName=SK,KeyType=RANGE \
    --global-secondary-indexes \
      "[
        {
          \"IndexName\": \"GSI1\",
          \"KeySchema\": [
            {\"AttributeName\": \"GSI1PK\", \"KeyType\": \"HASH\"},
            {\"AttributeName\": \"GSI1SK\", \"KeyType\": \"RANGE\"}
          ],
          \"Projection\": {\"ProjectionType\": \"ALL\"}
        },
        {
          \"IndexName\": \"GSI2\",
          \"KeySchema\": [
            {\"AttributeName\": \"GSI2PK\", \"KeyType\": \"HASH\"},
            {\"AttributeName\": \"GSI2SK\", \"KeyType\": \"RANGE\"}
          ],
          \"Projection\": {\"ProjectionType\": \"ALL\"}
        }
      ]" \
    --billing-mode PAY_PER_REQUEST \
    --endpoint-url "$DYNAMODB_ENDPOINT" \
    --region "$AWS_REGION" >/dev/null

  aws dynamodb wait table-exists \
    --table-name "$TABLE_NAME" \
    --endpoint-url "$DYNAMODB_ENDPOINT" \
    --region "$AWS_REGION"
fi

echo "Enabling TTL..."
aws dynamodb update-time-to-live \
  --table-name "$TABLE_NAME" \
  --time-to-live-specification "Enabled=true,AttributeName=ExpiresAtEpochSeconds" \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION" >/dev/null 2>&1 || true

echo "Creating SNS topic..."
TOPIC_ARN="$(aws sns create-topic \
  --name "$TOPIC_NAME" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query TopicArn \
  --output text)"

create_queue() {
  local queue_name="$1"

  aws sqs create-queue \
    --queue-name "$queue_name" \
    --endpoint-url "$LOCALSTACK_ENDPOINT" \
    --region "$AWS_REGION" \
    --query QueueUrl \
    --output text
}

get_queue_arn() {
  local queue_url="$1"

  aws sqs get-queue-attributes \
    --queue-url "$queue_url" \
    --attribute-names QueueArn \
    --endpoint-url "$LOCALSTACK_ENDPOINT" \
    --region "$AWS_REGION" \
    --query "Attributes.QueueArn" \
    --output text
}

set_redrive() {
  local queue_url="$1"
  local dlq_arn="$2"
  local file_path="$3"

  cat > "$file_path" <<REDRIVE_JSON
{
  "VisibilityTimeout": "60",
  "RedrivePolicy": "{\"deadLetterTargetArn\":\"$dlq_arn\",\"maxReceiveCount\":\"3\"}"
}
REDRIVE_JSON

  aws sqs set-queue-attributes \
    --queue-url "$queue_url" \
    --attributes "file://$file_path" \
    --endpoint-url "$LOCALSTACK_ENDPOINT" \
    --region "$AWS_REGION" >/dev/null
}

subscribe_queue() {
  local queue_arn="$1"
  local filter_policy="$2"

  local subscription_arn

  subscription_arn="$(aws sns subscribe \
    --topic-arn "$TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$queue_arn" \
    --return-subscription-arn \
    --endpoint-url "$LOCALSTACK_ENDPOINT" \
    --region "$AWS_REGION" \
    --query SubscriptionArn \
    --output text)"

  aws sns set-subscription-attributes \
    --subscription-arn "$subscription_arn" \
    --attribute-name RawMessageDelivery \
    --attribute-value true \
    --endpoint-url "$LOCALSTACK_ENDPOINT" \
    --region "$AWS_REGION" >/dev/null

  if [ -n "$filter_policy" ]; then
    aws sns set-subscription-attributes \
      --subscription-arn "$subscription_arn" \
      --attribute-name FilterPolicy \
      --attribute-value "$filter_policy" \
      --endpoint-url "$LOCALSTACK_ENDPOINT" \
      --region "$AWS_REGION" >/dev/null
  fi

  echo "$subscription_arn"
}

echo "Creating DLQs..."
DECISION_DLQ_URL="$(create_queue "$DECISION_DLQ_NAME")"
KYC_DLQ_URL="$(create_queue "$KYC_DLQ_NAME")"
CREDIT_DLQ_URL="$(create_queue "$CREDIT_DLQ_NAME")"
AUDIT_DLQ_URL="$(create_queue "$AUDIT_DLQ_NAME")"

DECISION_DLQ_ARN="$(get_queue_arn "$DECISION_DLQ_URL")"
KYC_DLQ_ARN="$(get_queue_arn "$KYC_DLQ_URL")"
CREDIT_DLQ_ARN="$(get_queue_arn "$CREDIT_DLQ_URL")"
AUDIT_DLQ_ARN="$(get_queue_arn "$AUDIT_DLQ_URL")"

echo "Creating worker queues..."
DECISION_QUEUE_URL="$(create_queue "$DECISION_QUEUE_NAME")"
KYC_QUEUE_URL="$(create_queue "$KYC_QUEUE_NAME")"
CREDIT_QUEUE_URL="$(create_queue "$CREDIT_QUEUE_NAME")"
AUDIT_QUEUE_URL="$(create_queue "$AUDIT_QUEUE_NAME")"

set_redrive "$DECISION_QUEUE_URL" "$DECISION_DLQ_ARN" ".local/tmp/decision-redrive.json"
set_redrive "$KYC_QUEUE_URL" "$KYC_DLQ_ARN" ".local/tmp/kyc-redrive.json"
set_redrive "$CREDIT_QUEUE_URL" "$CREDIT_DLQ_ARN" ".local/tmp/credit-redrive.json"
set_redrive "$AUDIT_QUEUE_URL" "$AUDIT_DLQ_ARN" ".local/tmp/audit-redrive.json"

DECISION_QUEUE_ARN="$(get_queue_arn "$DECISION_QUEUE_URL")"
KYC_QUEUE_ARN="$(get_queue_arn "$KYC_QUEUE_URL")"
CREDIT_QUEUE_ARN="$(get_queue_arn "$CREDIT_QUEUE_URL")"
AUDIT_QUEUE_ARN="$(get_queue_arn "$AUDIT_QUEUE_URL")"

echo "Subscribing queues..."

DECISION_FILTER='{"eventType":["LoanApplicationSubmitted","KycCompleted","KycFailed","KycNeedsReview","CreditProfileUpserted"]}'
KYC_FILTER='{"eventType":["KycCheckRequested"]}'
CREDIT_FILTER='{"eventType":["CreditAssessmentRequested"]}'

DECISION_SUBSCRIPTION_ARN="$(subscribe_queue "$DECISION_QUEUE_ARN" "$DECISION_FILTER")"
KYC_SUBSCRIPTION_ARN="$(subscribe_queue "$KYC_QUEUE_ARN" "$KYC_FILTER")"
CREDIT_SUBSCRIPTION_ARN="$(subscribe_queue "$CREDIT_QUEUE_ARN" "$CREDIT_FILTER")"
AUDIT_SUBSCRIPTION_ARN="$(subscribe_queue "$AUDIT_QUEUE_ARN" "")"

cat > .local/local-resources.env <<LOCAL_RESOURCES_ENV
AWS_REGION=$AWS_REGION
AWS_ACCESS_KEY_ID=$AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY=$AWS_SECRET_ACCESS_KEY
DYNAMODB_SERVICE_URL=$DYNAMODB_ENDPOINT
LOCALSTACK_SERVICE_URL=$LOCALSTACK_ENDPOINT
DYNAMODB_TABLE_NAME=$TABLE_NAME
DOMAIN_EVENTS_TOPIC_ARN=$TOPIC_ARN

DECISION_QUEUE_URL=$DECISION_QUEUE_URL
DECISION_DLQ_URL=$DECISION_DLQ_URL
DECISION_QUEUE_ARN=$DECISION_QUEUE_ARN
DECISION_SUBSCRIPTION_ARN=$DECISION_SUBSCRIPTION_ARN

KYC_QUEUE_URL=$KYC_QUEUE_URL
KYC_DLQ_URL=$KYC_DLQ_URL
KYC_QUEUE_ARN=$KYC_QUEUE_ARN
KYC_SUBSCRIPTION_ARN=$KYC_SUBSCRIPTION_ARN

CREDIT_QUEUE_URL=$CREDIT_QUEUE_URL
CREDIT_DLQ_URL=$CREDIT_DLQ_URL
CREDIT_QUEUE_ARN=$CREDIT_QUEUE_ARN
CREDIT_SUBSCRIPTION_ARN=$CREDIT_SUBSCRIPTION_ARN

AUDIT_QUEUE_URL=$AUDIT_QUEUE_URL
AUDIT_DLQ_URL=$AUDIT_DLQ_URL
AUDIT_QUEUE_ARN=$AUDIT_QUEUE_ARN
AUDIT_SUBSCRIPTION_ARN=$AUDIT_SUBSCRIPTION_ARN
LOCAL_RESOURCES_ENV

echo ""
echo "Local resources created."
echo "Saved: .local/local-resources.env"
