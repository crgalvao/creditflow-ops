# Architecture

## Overview

CreditFlow Ops is a serverless credit decision workflow built with .NET, React, DynamoDB, SNS, SQS, Cognito, and AWS Lambda.

This document will describe the system architecture, service boundaries, request flow, event flow, data storage, security, observability, and trade-offs.

## Diagrams

### Architecture

```mermaid
flowchart LR
  User["Credit Analyst"] --> Web["React + TypeScript + Vite"]
  Web --> Cognito["Amazon Cognito"]
  Web --> ApiGw["API Gateway HTTP API"]
  ApiGw --> ApiLambda["CreditFlow.Api"]
  ApiLambda --> DDB["DynamoDB"]
  ApiLambda --> SNS["SNS Domain Topic"]
  SNS --> DecisionQueue["SQS Decision Queue"]
  DecisionQueue --> DecisionWorker["Decision Worker"]
  DecisionWorker --> DDB
```

## Main Components

TODO.

## Request Flow

TODO.

## Event-Driven Flow

TODO.

## Data Storage

TODO.

## Authentication and Authorization

TODO.

## Observability

TODO.

## Security

TODO.

## Trade-offs

TODO.
