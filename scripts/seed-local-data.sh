#!/usr/bin/env bash
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"

DYNAMODB_ENDPOINT="${DYNAMODB_SERVICE_URL:-http://localhost:8000}"
LOCALSTACK_ENDPOINT="${LOCALSTACK_SERVICE_URL:-http://localhost:4566}"
TABLE_NAME="${DYNAMODB_TABLE_NAME:-CreditFlowLocal}"

export AWS_REGION AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY

if [ -f .local/local-resources.env ]; then
  set -a
  # shellcheck disable=SC1091
  source .local/local-resources.env
  set +a
fi

if [ -z "${DOMAIN_EVENTS_TOPIC_ARN:-}" ]; then
  echo "DOMAIN_EVENTS_TOPIC_ARN is missing. Run ./scripts/create-local-resources.sh first."
  exit 1
fi

NOW="2026-07-03T12:00:00Z"
APPLICATION_ID="app_local_demo_001"
EVENT_ID="evt_local_demo_001"
CORRELATION_ID="corr_local_demo_001"
OWNER_USER_ID="local-user-001"

mkdir -p samples/dynamodb samples/events

cat > samples/dynamodb/local-seed-application.json <<JSON
{
  "PK": { "S": "APP#$APPLICATION_ID" },
  "SK": { "S": "METADATA" },
  "EntityType": { "S": "LoanApplication" },
  "ApplicationId": { "S": "$APPLICATION_ID" },
  "OwnerUserId": { "S": "$OWNER_USER_ID" },
  "BorrowerName": { "S": "Demo Coffee Imports Ltd" },
  "BorrowerTaxId": { "S": "DEMO-0001" },
  "Industry": { "S": "Wholesale" },
  "MonthsInBusiness": { "N": "48" },
  "RequestedAmount": { "N": "80000" },
  "MonthlyRevenue": { "N": "65000" },
  "Purpose": { "S": "Inventory financing" },
  "Status": { "S": "Submitted" },
  "CreatedAt": { "S": "$NOW" },
  "UpdatedAt": { "S": "$NOW" },
  "GSI1PK": { "S": "USER#$OWNER_USER_ID" },
  "GSI1SK": { "S": "CREATED#$NOW#APP#$APPLICATION_ID" },
  "GSI2PK": { "S": "STATUS#Submitted" },
  "GSI2SK": { "S": "UPDATED#$NOW#APP#$APPLICATION_ID" }
}
JSON

cat > samples/dynamodb/local-seed-timeline-event.json <<JSON
{
  "PK": { "S": "APP#$APPLICATION_ID" },
  "SK": { "S": "EVENT#$NOW#$EVENT_ID" },
  "EntityType": { "S": "TimelineEvent" },
  "ApplicationId": { "S": "$APPLICATION_ID" },
  "EventId": { "S": "$EVENT_ID" },
  "EventType": { "S": "ApplicationSubmitted" },
  "Message": { "S": "Application submitted by local seed script." },
  "CorrelationId": { "S": "$CORRELATION_ID" },
  "Timestamp": { "S": "$NOW" }
}
JSON

cat > samples/events/local-loan-application-submitted.json <<JSON
{
  "eventId": "$EVENT_ID",
  "eventType": "LoanApplicationSubmitted",
  "eventVersion": 1,
  "correlationId": "$CORRELATION_ID",
  "occurredAt": "$NOW",
  "source": "CreditFlow.LocalSeed",
  "data": {
    "applicationId": "$APPLICATION_ID",
    "ownerUserId": "$OWNER_USER_ID",
    "requestedAmount": 80000,
    "monthlyRevenue": 65000
  }
}
JSON

echo "Seeding DynamoDB application metadata..."
aws dynamodb put-item \
  --table-name "$TABLE_NAME" \
  --item file://samples/dynamodb/local-seed-application.json \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION"

echo "Seeding DynamoDB timeline event..."
aws dynamodb put-item \
  --table-name "$TABLE_NAME" \
  --item file://samples/dynamodb/local-seed-timeline-event.json \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION"

echo "Publishing LoanApplicationSubmitted event to SNS..."
aws sns publish \
  --topic-arn "$DOMAIN_EVENTS_TOPIC_ARN" \
  --message file://samples/events/local-loan-application-submitted.json \
  --message-attributes '{
    "eventType": {
      "DataType": "String",
      "StringValue": "LoanApplicationSubmitted"
    },
    "eventVersion": {
      "DataType": "Number",
      "StringValue": "1"
    },
    "correlationId": {
      "DataType": "String",
      "StringValue": "corr_local_demo_001"
    }
  }' \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$AWS_REGION"

echo ""
echo "Seed completed."
echo "ApplicationId: $APPLICATION_ID"
echo ""
echo "You can verify the application with:"
echo "aws dynamodb get-item --table-name $TABLE_NAME --key '{\"PK\":{\"S\":\"APP#$APPLICATION_ID\"},\"SK\":{\"S\":\"METADATA\"}}' --endpoint-url $DYNAMODB_ENDPOINT --region $AWS_REGION"
