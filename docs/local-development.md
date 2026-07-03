# Local Development

This document explains how to run CreditFlow Ops locally.

The local setup is intentionally lightweight. The goal is to let the backend, workers, and frontend run without using real AWS resources during development.

## Prerequisites

Install:

- Docker Desktop with WSL integration enabled
- .NET 10 SDK
- Node.js 24
- pnpm 11.9.0 or newer
- AWS CLI
- GitHub CLI, optional but useful for PRs and issues

When using WSL, keep the repository inside the Linux filesystem instead of `/mnt/c` for better performance.

Recommended path example:

```bash
~/projects/GitHub_Portfolio/CreditFlow\ Ops/creditflow-ops
```

## Local Services

CreditFlow Ops uses Docker Compose for local infrastructure dependencies:

- DynamoDB Local on port `8000`
- LocalStack on port `4566` for SNS and SQS

Start services from the repository root:

```bash
docker compose up -d
```

Check running containers:

```bash
docker compose ps
```

Expected containers:

```text
creditflow-dynamodb-local
creditflow-localstack
```

Stop services:

```bash
docker compose down
```

Reset local containers and generated local resource files:

```bash
docker compose down -v
rm -rf .local
```

## DynamoDB Local

DynamoDB Local runs on:

```text
http://localhost:8000
```

The project uses DynamoDB Local in memory:

```text
-sharedDb -inMemory
```

This avoids file permission issues in WSL/Docker and keeps the local setup reproducible.

Because DynamoDB Local is in memory, all tables and data are lost when the container restarts. This is expected. Recreate local resources and seed data with:

```bash
./scripts/create-local-resources.sh
./scripts/seed-local-data.sh
```

Validate DynamoDB Local:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb list-tables \
  --endpoint-url http://localhost:8000 \
  --region us-east-1
```

Before creating local resources, the expected result is:

```json
{
    "TableNames": []
}
```

After running `./scripts/create-local-resources.sh`, the expected table is:

```text
CreditFlowLocal
```

## LocalStack SNS/SQS

LocalStack runs on:

```text
http://localhost:4566
```

LocalStack is pinned in `docker-compose.yml` instead of using `latest` to avoid unexpected breaking changes in local development.

Validate LocalStack health:

```bash
curl http://localhost:4566/_localstack/health
```

Validate SNS:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws sns list-topics \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

Validate SQS:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws sqs list-queues \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

Before creating local resources, no topics or queues are expected.

## Environment Variables

Use `.env.example` as the reference for local environment variables.

Do not commit real `.env` files.

Common local values:

```bash
AWS_REGION=us-east-1
AWS_ACCESS_KEY_ID=test
AWS_SECRET_ACCESS_KEY=test
DYNAMODB_SERVICE_URL=http://localhost:8000
LOCALSTACK_SERVICE_URL=http://localhost:4566
DYNAMODB_TABLE_NAME=CreditFlowLocal
AUTH_DISABLED=true
```

The local resource script generates this file:

```text
.local/local-resources.env
```

That file contains generated local ARNs and queue URLs. It is local-only and must not be committed.

## Creating Local AWS Resources

After Docker services are running, create local resources:

```bash
./scripts/create-local-resources.sh
```

This creates:

- DynamoDB table: `CreditFlowLocal`
- DynamoDB TTL configuration
- SNS topic: `creditflow-domain-events`
- SQS decision queue
- SQS decision DLQ
- SQS audit queue
- SQS audit DLQ
- SNS subscription from topic to decision queue
- SNS subscription from topic to audit queue
- SNS filter policy for `LoanApplicationSubmitted` events on the decision queue

The script writes generated local resource values to:

```text
.local/local-resources.env
```

Inspect generated values:

```bash
cat .local/local-resources.env
```

## Seeding Local Data

To insert a sample loan application and publish a sample domain event:

```bash
./scripts/seed-local-data.sh
```

The seed script creates:

- A sample loan application metadata record
- A sample timeline event
- A sample `LoanApplicationSubmitted` event
- Sample JSON files under `samples/dynamodb`
- Sample JSON files under `samples/events`

## Verifying Seeded DynamoDB Data

Scan the local table:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb scan \
  --table-name CreditFlowLocal \
  --endpoint-url http://localhost:8000 \
  --region us-east-1
```

You should see records with entity types such as:

```text
LoanApplication
TimelineEvent
```

Get the seeded application directly:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb get-item \
  --table-name CreditFlowLocal \
  --key '{"PK":{"S":"APP#app_local_demo_001"},"SK":{"S":"METADATA"}}' \
  --endpoint-url http://localhost:8000 \
  --region us-east-1
```

## Verifying SNS to SQS Delivery

Load generated local resource values:

```bash
source .local/local-resources.env
```

Receive a message from the decision queue:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws sqs receive-message \
  --queue-url "$DECISION_QUEUE_URL" \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

You should see the seeded `LoanApplicationSubmitted` event.

The message is not deleted by this command. That is useful while validating local setup.

## Full Local Setup From Zero

Use this sequence when you want to reset everything:

```bash
docker compose down -v
rm -rf .local
docker compose up -d
./scripts/create-local-resources.sh
./scripts/seed-local-data.sh
```

Then validate:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb scan \
  --table-name CreditFlowLocal \
  --endpoint-url http://localhost:8000 \
  --region us-east-1
```

```bash
source .local/local-resources.env

AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws sqs receive-message \
  --queue-url "$DECISION_QUEUE_URL" \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

## Backend

Backend implementation is added in later tasks.

Current validation:

```bash
dotnet test src/backend/CreditFlow.slnx
```

## Frontend

Frontend implementation is added in later tasks.

Current validation:

```bash
cd src/frontend/creditflow-web
pnpm build
cd ../../..
```

## Workers

Worker implementation is added in later tasks.

Planned workers:

- `CreditFlow.DecisionWorker`
- `CreditFlow.AuditWorker`

The local SNS/SQS setup already prepares the queues these workers will consume.

## Troubleshooting

### Docker permission denied in WSL

If you see:

```text
permission denied while trying to connect to the Docker daemon socket
```

Check whether Docker works with sudo:

```bash
sudo docker ps
```

If it works with sudo but not without sudo, add your user to the Docker group:

```bash
sudo usermod -aG docker "$USER"
```

Then restart WSL from Windows PowerShell:

```powershell
wsl --shutdown
```

Reopen Ubuntu and test:

```bash
docker ps
```

### Docker Desktop WSL integration

If Docker does not work from WSL, open Docker Desktop on Windows and enable:

```text
Settings → Resources → WSL Integration → Enable integration for your Ubuntu distro
```

Then restart WSL:

```powershell
wsl --shutdown
```

### LocalStack container exits

Check logs:

```bash
docker compose logs localstack --tail=100
```

This project pins LocalStack in `docker-compose.yml` to avoid unexpected behavior from the `latest` tag.

### DynamoDB Local SQLite errors

If DynamoDB Local logs show SQLite errors such as:

```text
unable to open database file
```

Use in-memory mode in `docker-compose.yml`:

```text
-sharedDb -inMemory
```

This is the intended setup for this project.

### AWS CLI credentials

Local AWS commands require dummy credentials.

Use inline credentials:

```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb list-tables \
  --endpoint-url http://localhost:8000 \
  --region us-east-1
```

Or export them for the current terminal:

```bash
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_REGION=us-east-1
```

### Ports already in use

Check port `8000`:

```bash
sudo lsof -i :8000
```

Check port `4566`:

```bash
sudo lsof -i :4566
```

### Inspect container logs

```bash
docker compose logs dynamodb-local --tail=100
docker compose logs localstack --tail=100
```

## Local Development Rule

For now, the reliable local workflow is:

```bash
docker compose up -d
./scripts/create-local-resources.sh
./scripts/seed-local-data.sh
```

Do not manually create local tables, topics, or queues unless debugging.

Do not commit `.local`.
Do not commit real `.env` files.
Do not use real personal, financial, or customer data.
