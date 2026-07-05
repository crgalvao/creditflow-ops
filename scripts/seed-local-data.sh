#!/usr/bin/env bash
set -euo pipefail

export AWS_REGION="${AWS_REGION:-us-east-1}"
export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"

DYNAMODB_ENDPOINT="${DYNAMODB_SERVICE_URL:-http://localhost:8000}"
LOCALSTACK_ENDPOINT="${LOCALSTACK_SERVICE_URL:-http://localhost:4566}"
TABLE_NAME="${DYNAMODB_TABLE_NAME:-CreditFlowLocal}"

if [ -f .local/local-resources.env ]; then
  set -a
  source .local/local-resources.env
  set +a
fi

if [ -z "${DOMAIN_EVENTS_TOPIC_ARN:-}" ]; then
  echo "DOMAIN_EVENTS_TOPIC_ARN is missing. Run ./scripts/create-local-resources.sh first."
  exit 1
fi

NOW="${SEED_NOW:-2026-07-03T12:00:00Z}"
APPLICATION_ID="${SEED_APPLICATION_ID:-app_local_demo_001}"
CLIENT_ID="${SEED_CLIENT_ID:-client_local_demo_001}"
OWNER_USER_ID="${SEED_OWNER_USER_ID:-local-user-001}"
TAX_ID="${SEED_TAX_ID:-DEMO-0001}"
CORRELATION_ID="${SEED_CORRELATION_ID:-corr_local_demo_001}"

mkdir -p samples/dynamodb samples/events

cat > samples/dynamodb/local-seed-application.json <<LOCAL_SEED_APPLICATION_JSON
{
  "PK": { "S": "APP#$APPLICATION_ID" },
  "SK": { "S": "METADATA" },
  "EntityType": { "S": "LoanApplication" },
  "ApplicationId": { "S": "$APPLICATION_ID" },
  "OwnerUserId": { "S": "$OWNER_USER_ID" },
  "BorrowerLegalName": { "S": "Demo Coffee Imports Ltd" },
  "BorrowerTaxId": { "S": "$TAX_ID" },
  "Industry": { "S": "Wholesale" },
  "MonthsInBusiness": { "N": "48" },
  "RequestedAmount": { "N": "80000" },
  "RequestedAmountCurrency": { "S": "USD" },
  "MonthlyRevenue": { "N": "65000" },
  "MonthlyRevenueCurrency": { "S": "USD" },
  "Purpose": { "S": "Inventory financing" },
  "Status": { "S": "KycInProgress" },
  "KycState": { "S": "Pending" },
  "CreditState": { "S": "Pending" },
  "CreatedAt": { "S": "$NOW" },
  "UpdatedAt": { "S": "$NOW" },
  "GSI1PK": { "S": "USER#$OWNER_USER_ID" },
  "GSI1SK": { "S": "CREATED#$NOW#APP#$APPLICATION_ID" },
  "GSI2PK": { "S": "STATUS#KycInProgress" },
  "GSI2SK": { "S": "UPDATED#$NOW#APP#$APPLICATION_ID" }
}
LOCAL_SEED_APPLICATION_JSON

cat > samples/dynamodb/local-seed-timeline-event.json <<LOCAL_SEED_TIMELINE_JSON
{
  "PK": { "S": "APP#$APPLICATION_ID" },
  "SK": { "S": "EVENT#$NOW#evt_local_demo_001" },
  "EntityType": { "S": "TimelineEvent" },
  "ApplicationId": { "S": "$APPLICATION_ID" },
  "EventId": { "S": "evt_local_demo_001" },
  "EventType": { "S": "LoanApplicationSubmitted" },
  "Message": { "S": "Application submitted and KYC check requested by local seed script." },
  "CorrelationId": { "S": "$CORRELATION_ID" },
  "Timestamp": { "S": "$NOW" }
}
LOCAL_SEED_TIMELINE_JSON

write_event() {
  local file_path="$1"
  local event_id="$2"
  local event_type="$3"
  local data_json="$4"

  cat > "$file_path" <<EVENT_JSON
{
  "eventId": "$event_id",
  "eventType": "$event_type",
  "eventVersion": 1,
  "correlationId": "$CORRELATION_ID",
  "occurredAt": "$NOW",
  "source": "CreditFlow.LocalSeed",
  "data": $data_json
}
EVENT_JSON
}

write_event "samples/events/local-loan-application-submitted.json" "evt_local_loan_submitted_001" "LoanApplicationSubmitted" '{
    "applicationId": "app_local_demo_001",
    "ownerUserId": "local-user-001",
    "taxId": "DEMO-0001",
    "requestedAmount": 80000,
    "monthlyRevenue": 65000
  }'

write_event "samples/events/local-kyc-check-requested.json" "evt_local_kyc_requested_001" "KycCheckRequested" '{
    "applicationId": "app_local_demo_001",
    "taxId": "DEMO-0001"
  }'

write_event "samples/events/local-kyc-completed.json" "evt_local_kyc_completed_001" "KycCompleted" '{
    "clientId": "client_local_demo_001",
    "taxId": "DEMO-0001",
    "status": "Verified"
  }'

write_event "samples/events/local-kyc-failed.json" "evt_local_kyc_failed_001" "KycFailed" '{
    "clientId": "client_local_demo_001",
    "taxId": "DEMO-0001",
    "reasons": [
      "Matched blocked name rule."
    ]
  }'

write_event "samples/events/local-kyc-needs-review.json" "evt_local_kyc_needs_review_001" "KycNeedsReview" '{
    "clientId": "client_local_demo_001",
    "taxId": "DEMO-0001",
    "reasons": [
      "Missing required documentation."
    ]
  }'

write_event "samples/events/local-credit-assessment-requested.json" "evt_local_credit_requested_001" "CreditAssessmentRequested" '{
    "applicationId": "app_local_demo_001",
    "clientId": "client_local_demo_001",
    "taxId": "DEMO-0001",
    "requestedAmount": 80000
  }'

write_event "samples/events/local-credit-assessment-completed.json" "evt_local_credit_completed_001" "CreditAssessmentCompleted" '{
    "assessmentId": "assess_local_demo_001",
    "clientId": "client_local_demo_001",
    "applicationId": "app_local_demo_001",
    "result": "Approved",
    "score": 85
  }'

write_event "samples/events/local-credit-profile-upserted.json" "evt_local_credit_profile_upserted_001" "CreditProfileUpserted" '{
    "clientId": "client_local_demo_001",
    "lastAssessmentId": "assess_local_demo_001",
    "currentScore": 85,
    "currentResult": "Approved"
  }'

write_event "samples/events/local-loan-decision-completed.json" "evt_local_decision_completed_001" "LoanDecisionCompleted" '{
    "applicationId": "app_local_demo_001",
    "finalStatus": "Approved"
  }'

publish_event() {
  local file_path="$1"
  local event_type="$2"

  aws sns publish \
    --topic-arn "$DOMAIN_EVENTS_TOPIC_ARN" \
    --message "file://$file_path" \
    --message-attributes "{
      \"eventType\": {
        \"DataType\": \"String\",
        \"StringValue\": \"$event_type\"
      },
      \"eventVersion\": {
        \"DataType\": \"Number\",
        \"StringValue\": \"1\"
      },
      \"correlationId\": {
        \"DataType\": \"String\",
        \"StringValue\": \"$CORRELATION_ID\"
      }
    }" \
    --endpoint-url "$LOCALSTACK_ENDPOINT" \
    --region "$AWS_REGION" >/dev/null
}

echo "Writing seed application to DynamoDB..."
aws dynamodb put-item \
  --table-name "$TABLE_NAME" \
  --item file://samples/dynamodb/local-seed-application.json \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION" >/dev/null

echo "Writing seed timeline event to DynamoDB..."
aws dynamodb put-item \
  --table-name "$TABLE_NAME" \
  --item file://samples/dynamodb/local-seed-timeline-event.json \
  --endpoint-url "$DYNAMODB_ENDPOINT" \
  --region "$AWS_REGION" >/dev/null

echo "Publishing initial events..."
publish_event "samples/events/local-loan-application-submitted.json" "LoanApplicationSubmitted"
publish_event "samples/events/local-kyc-check-requested.json" "KycCheckRequested"

echo ""
echo "Seed completed."
echo "ApplicationId: $APPLICATION_ID"
echo "ClientId: $CLIENT_ID"
echo "Samples written to samples/events and samples/dynamodb."
