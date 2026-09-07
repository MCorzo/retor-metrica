# Event Platform MVP

Online events platform MVP built as **two independently deployable .NET 10
microservices**:

- **EventService** — RESTful API to create events with zones (atomic
  transaction), list events with role-based visibility served from a fast
  Redis cache, and retrieve event detail. On creation it reliably publishes an
  `EventCreated` event to AWS SNS through a transactional outbox + relay.
- **NotificationService** — consumes `EventCreated` from a subscribed AWS SQS
  queue, persists a durable notification record, and sends a single simple-text
  email to the Admin via MailKit (MailHog in dev). Exactly-once dedup by
  correlation id; SMTP failures are recorded and retried.

Documentation for the feature lives in
[`specs/001-event-platform-mvp/`](specs/001-event-platform-mvp/) (`quickstart.md`,
`spec.md`, `plan.md`, `tasks.md`, `data-model.md`, `contracts/`).

The AWS SNS/SQS/CloudWatch migration is documented in
[`specs/002-aws-sns-migration/`](specs/002-aws-sns-migration/)
(`plan.md`, `spec.md`, `tasks.md`, `data-model.md`, `contracts/aws-environment-variables.md`,
`quickstart.md`).

## Architecture

```text
services/
├── event-service/          # Create/list/detail events + publish EventCreated
│   └── src/
│       ├── EventService.Domain/          # Entities, value objects, rules
│       ├── EventService.Application/     # MediatR handlers, validators, DTOs
│       ├── EventService.Infrastructure/  # EF Core, outbox, Redis, SNS publish
│       └── EventService.Api/             # REST endpoints, auth, rate limiting, health, Scalar
├── notification-service/   # Consume EventCreated → record + email
│   └── src/
│       ├── NotificationService.Domain/   # NotificationRecord entity
│       ├── NotificationService.Application/
│       ├── NotificationService.Infrastructure/  # EF Core, SQS consumer, MailKit
│       └── NotificationService.Api/      # Health, notifications query, Scalar
├── contracts/              # Shared event contracts (EventPlatform.Contracts)
└── docker/                 # docker-compose dev stack (postgres, redis, mailhog, keycloak)
```

Core conventions (constitution v2.0.0): UUIDv7 identifiers, mandatory audit
fields on every record, EF Core-only PostgreSQL access, OIDC JWT authN/Z with
resource-ownership enforcement, Polly-based resilience, Serilog → CloudWatch,
Scalar docs, health checks, and rate limiting per API. **No automated tests** —
correctness is verified through the manual smoke checks in `quickstart.md`.

## Prerequisites

- .NET 10 SDK (SDK version pinned in `global.json`)
- Docker (for the backend stack and local verification)

## Start the backend stack

```powershell
Copy-Item .env.example .env   # first run only, fill in AWS credentials
docker compose up -d --build
```

| Container | Port | Purpose |
|-----------|------|---------|
| `postgres` | 5432 | Both services' transactional stores (schemas `events`, `notifications`) |
| `redis` | 6379 | ElastiCache-compatible cache (dev stand-in) |
| `mailhog` | 8025 / 1025 | SMTP sink for email (UI at http://localhost:8025) |
| `keycloak` | 8080 | OIDC IdP for dev tokens |

The SNS topic, SQS queue, and SNS→SQS subscription are auto-provisioned against
**real AWS** at startup. Credentials are injected from the
git-ignored `.env` via `Sns__*`/`CloudWatch__*` environment variables — see
`specs/002-aws-sns-migration/contracts/aws-environment-variables.md`.

## Run the services

```powershell
dotnet run --project services/event-service/src/EventService.Api          # http://localhost:5000
dotnet run --project services/notification-service/src/NotificationService.Api  # http://localhost:5100
```

On startup both services apply EF Core migrations; the BrokerProvisioner
creates the SNS topic/queue/subscription and the outbox relay ships `Pending`
outbox rows to SNS. Dev OIDC tokens always come from the Keycloak realm started
by `docker compose`.

- API docs (Scalar): http://localhost:5000/scalar/v1 and
  http://localhost:5100/scalar/v1
- Health checks: `http://localhost:5000/health/live` + `/health/ready`
  (readiness validates Postgres, Redis, SNS).

## Verify the loop (smoke)

1. Create an event:

   ```powershell
   $body = @{
     name  = "Summer Fest 2026"; date = "2026-07-25T22:00:00Z"
     venue = "Parque Norte, Madrid"; status = "published"
     zones = @(@{name="General"; price=25.00; capacity=500}, @{name="VIP"; price=90.00; capacity=100})
   } | ConvertTo-Json -Depth 4
   Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/v1/events `
     -Headers @{Authorization="Bearer $token"} -ContentType application/json -Body $body
   ```

2. Poll the notification record: `GET http://localhost:5100/api/v1/notifications`
   → one `Pending` record, then `Sent` within ~60 s.
3. Email visible in the MailHog UI at http://localhost:8025.
4. Duplicate/failure/broker-down and cache behavior checks are in
   `quickstart.md` (§5–6).

## Build

```powershell
dotnet build EventPlatform.slnx   # 0 warnings / 0 errors expected
dotnet format EventPlatform.slnx  # enforced formatting
```

## Development config quick reference

| Setting | Default (dev) | Purpose |
|---------|---------------|---------|
| `ConnectionStrings:Default` | `Host=localhost;Port=5432;Database=eventplatform;Username=eventplatform;Password=eventplatform` | PostgreSQL |
| `Redis:Connection` | `localhost:6379` | Cache |
| `Sns:TopicName` | `event-created` | SNS topic auto-created at startup |
| `Sns:QueueName` | `notification-service-event-created` | SQS queue auto-created + subscribed (NotificationService) |
| `Aws:Region` | `us-east-1` | AWS region (SNS/SQS/CloudWatch) |
| `Aws:AccessKey` / `Aws:SecretKey` | empty | AWS static credentials (from env, never committed) |
| `CloudWatch:Enabled` | `false` (appsettings) → `true` (compose) | Ship structured logs to AWS CloudWatch (30-day retention) |
| `Smtp:Host/Port` | `localhost:1025` (MailHog) | SMTP (no auth in dev; StartTLS in prod) |
| `Oidc:Authority` | `http://localhost:8080/realms/event-platform` | JWT issuer (dev) |
| `Oidc:Audience` | `event-platform` | JWT audience |

Production secrets come from environment variables or user-secrets, never from
committed files (constitution security rules).