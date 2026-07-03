#!/usr/bin/env bash
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"

DYNAMODB_ENDPOINT="${DYNAMODB_SERVICE_URL:-http://localhost:8000}"
LOCALSTACK_ENDPOINT="${LOCALSTACK_SERVICE_URL:-http://localhost:4566}"

TABLE_NAME="${DYNAMODB_TABLE_NAME:-CreditFlowLocal}"
TOPIC_NAME="${DOMAIN_EVENTS_TOPIC_NAME:-creditflow-domain-events}"

DECISION_QUEUE_NAME="${DECISION_QUEUE_NAME:-creditflow-decision-queue}"
DECISION_DLQ_NAME="${DECISION_DLQ_NAME:-creditflow-decision-dlq}"
AUDIT_QUEUE_NAME="${AUDIT_QUEUE_NAME:-creditflow-audit-queue}"
AUDIT_DLQ_NAME="${AUDIT_DLQ_NAME:-creditflow-audit-dlq}"

export AWS_REGION AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY

mkdir -p .local/tmp

echo "Creating local DynamoDB table if needed..."

if aws dynamodb describe-table \
  --table-name "$TABLE_NAME" \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION" >/dev/null 2>&1; then
  echo "DynamoDB table already exists: $TABLE_NAME"
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

  echo "Waiting for DynamoDB table to exist..."
  aws dynamodb wait table-exists \
    --table-name "$TABLE_NAME" \
    --endpoint-url "$DYNAMODB_ENDPOINT" \
    --region "$AWS_REGION"
fi

echo "Configuring DynamoDB TTL..."
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

echo "SNS topic ARN: $TOPIC_ARN"

echo "Creating SQS DLQs..."

DECISION_DLQ_URL="$(aws sqs create-queue \
  --queue-name "$DECISION_DLQ_NAME" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query QueueUrl \
  --output text)"

AUDIT_DLQ_URL="$(aws sqs create-queue \
  --queue-name "$AUDIT_DLQ_NAME" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query QueueUrl \
  --output text)"

DECISION_DLQ_ARN="$(aws sqs get-queue-attributes \
  --queue-url "$DECISION_DLQ_URL" \
  --attribute-names QueueArn \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query "Attributes.QueueArn" \
  --output text)"

AUDIT_DLQ_ARN="$(aws sqs get-queue-attributes \
  --queue-url "$AUDIT_DLQ_URL" \
  --attribute-names QueueArn \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query "Attributes.QueueArn" \
  --output text)"

echo "Creating SQS queues..."

DECISION_QUEUE_URL="$(aws sqs create-queue \
  --queue-name "$DECISION_QUEUE_NAME" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query QueueUrl \
  --output text)"

AUDIT_QUEUE_URL="$(aws sqs create-queue \
  --queue-name "$AUDIT_QUEUE_NAME" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query QueueUrl \
  --output text)"

cat > .local/tmp/decision-queue-attributes.json <<JSON
{
  "VisibilityTimeout": "60",
  "RedrivePolicy": "{\"deadLetterTargetArn\":\"$DECISION_DLQ_ARN\",\"maxReceiveCount\":\"3\"}"
}
JSON

cat > .local/tmp/audit-queue-attributes.json <<JSON
{
  "VisibilityTimeout": "60",
  "RedrivePolicy": "{\"deadLetterTargetArn\":\"$AUDIT_DLQ_ARN\",\"maxReceiveCount\":\"3\"}"
}
JSON

echo "Configuring SQS queue attributes..."

aws sqs set-queue-attributes \
  --queue-url "$DECISION_QUEUE_URL" \
  --attributes file://.local/tmp/decision-queue-attributes.json \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION"

aws sqs set-queue-attributes \
  --queue-url "$AUDIT_QUEUE_URL" \
  --attributes file://.local/tmp/audit-queue-attributes.json \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION"

DECISION_QUEUE_ARN="$(aws sqs get-queue-attributes \
  --queue-url "$DECISION_QUEUE_URL" \
  --attribute-names QueueArn \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query "Attributes.QueueArn" \
  --output text)"

AUDIT_QUEUE_ARN="$(aws sqs get-queue-attributes \
  --queue-url "$AUDIT_QUEUE_URL" \
  --attribute-names QueueArn \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query "Attributes.QueueArn" \
  --output text)"

echo "Subscribing decision queue to SNS topic..."

DECISION_SUBSCRIPTION_ARN="$(aws sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$DECISION_QUEUE_ARN" \
  --return-subscription-arn \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query SubscriptionArn \
  --output text)"

aws sns set-subscription-attributes \
  --subscription-arn "$DECISION_SUBSCRIPTION_ARN" \
  --attribute-name RawMessageDelivery \
  --attribute-value true \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION"

aws sns set-subscription-attributes \
  --subscription-arn "$DECISION_SUBSCRIPTION_ARN" \
  --attribute-name FilterPolicy \
  --attribute-value '{"eventType":["LoanApplicationSubmitted"]}' \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION"

echo "Subscribing audit queue to SNS topic..."

AUDIT_SUBSCRIPTION_ARN="$(aws sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$AUDIT_QUEUE_ARN" \
  --return-subscription-arn \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION" \
  --query SubscriptionArn \
  --output text)"

aws sns set-subscription-attributes \
  --subscription-arn "$AUDIT_SUBSCRIPTION_ARN" \
  --attribute-name RawMessageDelivery \
  --attribute-value true \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION"

cat > .local/local-resources.env <<LOCAL_ENV
AWS_REGION=$AWS_REGION
DYNAMODB_SERVICE_URL=$DYNAMODB_ENDPOINT
LOCALSTACK_SERVICE_URL=$LOCALSTACK_ENDPOINT
DYNAMODB_TABLE_NAME=$TABLE_NAME
DOMAIN_EVENTS_TOPIC_ARN=$TOPIC_ARN
DECISION_QUEUE_URL=$DECISION_QUEUE_URL
DECISION_DLQ_URL=$DECISION_DLQ_URL
AUDIT_QUEUE_URL=$AUDIT_QUEUE_URL
AUDIT_DLQ_URL=$AUDIT_DLQ_URL
DECISION_QUEUE_ARN=$DECISION_QUEUE_ARN
AUDIT_QUEUE_ARN=$AUDIT_QUEUE_ARN
DECISION_SUBSCRIPTION_ARN=$DECISION_SUBSCRIPTION_ARN
AUDIT_SUBSCRIPTION_ARN=$AUDIT_SUBSCRIPTION_ARN
LOCAL_ENV

echo ""
echo "Local resources created."
echo "Resource output written to .local/local-resources.env"
echo ""
cat .local/local-resources.env
