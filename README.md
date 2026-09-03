# Innowise.D3S1T1.Gateway

The GraphQL read API for the D3S1T1 metrics pipeline. It exposes the readings
that `DataProcessor` persists — filtered, sorted, paged and aggregated — to the
dashboard frontend, and it does nothing else: no writes, no migrations, no
message-broker connection.

* Endpoint: `POST /graphql` (Nitro IDE at the same URL in Development)
* Readiness: `GET /health` — runs the metrics-database check
* Liveness: `GET /health/live` — no checks; answers while the database is down

---

## Architecture

```
WeakApp  ──HTTP──▶  DataIngestor  ──RabbitMQ──▶  DataProcessor  ──▶  MS SQL
 (flaky                (Quartz,                    (MassTransit         │
  external              Polly)                      consumer,           │  reads,
  API)                                              owns the           │  never
                                                    migrations)        │  writes
                                                                        ▼
                                            ┌───────────────────────────────────┐
                                            │  Gateway  (this repo)             │
                                            │                                   │
                                            │  Presentation  HotChocolate 16    │
                                            │  Application   query service,     │
                                            │                FluentValidation   │
                                            │  Infrastructure  EF Core 10,      │
                                            │                  read-only ctx    │
                                            │  Domain        MetricReading TPH  │
                                            └───────────────────────────────────┘
                                                          │
                                                     GraphQL over HTTP
                                                          ▼
                                                      Frontend
```

Four layers plus a host under `src/`, mirroring the sibling services' layering.
Each layer owns one `DependencyInjectionRegistration` (`AddApplication` /
`AddInfrastructure` / `AddPresentation`); `Gateway.AppHost` composes them and
adds Serilog and OpenTelemetry. The GraphQL server is wired inside
`AddPresentation`, not in `Program.cs`, so the layering stays honest.

| Project | Holds |
|---|---|
| `Gateway.Domain` | `MetricReading` and its three subtypes, `MetricReadingType` |
| `Gateway.Infrastructure` | `MetricsReadDbContext`, the EF configuration, `MetricReadingQueryService` |
| `Gateway.Application` | Query/result models, `IMetricReadingQueryService`, the aggregation validator |
| `Gateway.Presentation` | Schema types, the root query, the error filter, the health check |
| `Gateway.AppHost` | Host, Serilog, OpenTelemetry, endpoint mapping |

---

## Why the Gateway owns its own read model

The Gateway declares its own `MetricReading` hierarchy, EF configuration and
`DbContext`, pointed at the **same** database `DataProcessor` writes to. It never
creates, owns or applies a migration — `DataProcessor` stays the sole schema
owner, and the Gateway assumes the schema already exists when it starts.

The alternatives were a shared entity package or calling a REST endpoint on
`DataProcessor`. Both were rejected, for these reasons:

* **Full `IQueryable` pushdown.** Resolvers return `IQueryable<MetricReading>`;
  HotChocolate's filtering, sorting and offset-paging middlewares rewrite the
  expression tree and EF translates the whole thing into one parameterised SQL
  query. Aggregations are `GroupBy` LINQ in the Application layer, also executed
  in SQL Server. A REST hop would move all of that into the process.
* **No build-time coupling.** No shared package, no version lockstep, no
  cross-repo release ordering. This service ships independently.
* **Read-optimised on its own terms.** `NoTrackingWithIdentityResolution`, a
  pooled `DbContextFactory` instead of a scoped context (GraphQL resolves sibling
  root fields in parallel, and a read-only context has no shared change tracker
  to protect), and its own index proposals — none of which the write side wants.
* **Cost:** roughly 75 lines of duplicated entity and EF-configuration code —
  four entities, one enum, one `IEntityTypeConfiguration`. Trivial against the
  above.

Read-only is enforced structurally, not by comment: `MetricsReadDbContext`
overrides `SaveChanges`/`SaveChangesAsync` to throw, and there is no
`Migrations/` folder and no `Database.Migrate()` call anywhere in the repo.

### The liability, and the guard against it

If `DataProcessor` renames or retypes a column, this service still compiles and
fails only at runtime. Exactly one thing stands between that and production:
`tests/Gateway.Integration.Tests` starts a SQL Server container, applies
**DataProcessor's own schema** from `Schema/DataProcessorSchema.sql`, seeds rows,
and runs every Gateway query against it. That test *is* the contract between the
two services.

`DataProcessorSchema.sql` is a checked-in copy, which makes it a thing that can
itself drift. Regenerate it from the writer's repository with:

```bash
dotnet ef migrations script --idempotent \
  --project src/DataProcessor.Infrastructure \
  --startup-project src/DataProcessor.AppHost \
  --output DataProcessorSchema.sql
```

A scheduled CI job that regenerates and diffs it is the next step; until it
exists, refresh the file by hand whenever `DataProcessor` gains a migration.

---

## Running it

### With the whole stack (recommended)

The Gateway is a service in the Infrastructure repo's compose file. From
`Innowise.D3S1T1.Infrastructure/`:

```bash
cp .env.example .env      # set MSSQL_SA_PASSWORD and GITHUB_TOKEN
docker compose up -d --build
```

Then open <http://localhost:5003/graphql> for Nitro. Host port comes from
`GRAPHQL_PORT` (default `5003`); the container always listens on 8080.

### Standalone, against a database you already have

```bash
dotnet user-secrets --project src/Gateway.AppHost \
  set "ConnectionStrings:MetricsDb" \
  "Server=localhost,1433;Database=DataProcessor;User Id=sa;Password=…;Encrypt=True;TrustServerCertificate=True"

dotnet run --project src/Gateway.AppHost
```

`appsettings.json` ships `ConnectionStrings:MetricsDb` as `""` on purpose — real
values come from user secrets or environment variables, and startup fails fast
with a readable message rather than an opaque `SqlClient` error if it is missing.

The Gateway does not create the schema, so point it at a database `DataProcessor`
has already migrated. If the table is absent the service still starts and
`/health` reports **Unhealthy** — deliberately, so a missing schema is a
degraded service rather than a crash loop.

### Tests

```bash
dotnet test Innowise.D3S1T1.Gateway.slnx
```

The integration project needs a Docker daemon (it pulls a pinned SQL Server
image, ~2 GB RAM, amd64). The other three projects are pure unit tests and run
anywhere.

Two suites are snapshot-based and **fail by design until their baseline exists**,
rather than writing one silently — a snapshot nobody read is not a test:

* `SchemaSnapshotTests` pins the SDL against `tests/Gateway.Presentation.Tests/schema.graphql`.
* `ResponseSnapshotTests` pins the JSON each dashboard query returns, one file
  per query under `tests/Gateway.Integration.Tests/Snapshots/`.

On a first run each writes a `.json.actual` (or `.graphql.actual`) beside the
expected path in the **output** directory and fails with the path in the
message. Review it, copy it to the source tree under the name the message
gives, and re-run. After that any change to the schema or to a response shape
shows up as a diff in the pull request, next to the change that caused it.

### Exporting the SDL

```bash
dotnet run --project src/Gateway.AppHost -- schema export --output schema.graphql
```

This is what CI's `export-schema` job publishes as an artifact, and it is what
the frontend's Apollo/URQL codegen consumes. It builds the schema through the
app's own DI graph rather than introspecting a running server, so it needs no
database — but `AddInfrastructure` still demands a connection string, so set
`ConnectionStrings__MetricsDb` to any syntactically valid value first.

`tests/Gateway.Presentation.Tests/schema.graphql` is a committed SDL baseline
that `SchemaSnapshotTests` asserts against, so an accidental schema change fails
the build. Regenerate it deliberately when a change is intended.

---

## The schema

One `interface MetricReading` with three implementations, rather than a union —
unions are still unsupported by HotChocolate's projections, and an interface lets
a client select `room`/`receivedAt` without a fragment per type. Storage is
table-per-hierarchy, so `type` is the mapped discriminator column and is
filterable and sortable like any other field.

```graphql
interface MetricReading {
  id: ID!
  room: String!
  type: MetricReadingType!    # ENERGY | AIR_QUALITY | MOTION
  ingestedAt: DateTime!       # when the ingestor pulled the batch
  receivedAt: DateTime!       # when the processor persisted it
}

type EnergyReading     implements MetricReading { energyAmount: Float! }
type AirQualityReading implements MetricReading { co2: Float! pm25: Float! humidity: Float! }
type MotionReading     implements MetricReading { isMotionDetected: Boolean! }
```

**There is no `timestamp` field, on purpose.** WeakApp returns only a name and a
payload — there is no sensor timestamp anywhere in the pipeline. `ingestedAt` and
`receivedAt` are pipeline times, and they are named so that no client mistakes
either for observation time. Time aggregation buckets on `receivedAt`.

Both timestamps are `datetime2` in SQL Server, which carries no offset. An
identity provider-side conversion pins `DateTimeKind.Utc` on the way out, so the
`DateTime` scalar cannot stamp the server's local offset onto values that are
already UTC.

### Sample queries

Paged, filtered and sorted raw readings — the whole `where`/`order`/`skip`/`take`
shape is pushed into SQL. Asking for `totalCount` adds a second command, a
`COUNT` over the same predicate; omit it and the page is a single query:

```graphql
query Readings {
  metricReadings(
    skip: 0
    take: 20
    where: { room: { eq: "Kitchen" }, receivedAt: { gte: "2026-09-01T00:00:00Z" } }
    order: [{ receivedAt: DESC }]
  ) {
    totalCount
    pageInfo { hasNextPage hasPreviousPage }
    items {
      id
      room
      type
      receivedAt
      ... on EnergyReading { energyAmount }
      ... on AirQualityReading { co2 pm25 humidity }
      ... on MotionReading { isMotionDetected }
    }
  }
}
```

Hourly average CO₂ per room over a day:

```graphql
query Co2ByHour {
  metricAggregation(
    input: {
      field: CO2
      interval: HOUR
      groupByRoom: true
      from: "2026-09-02T00:00:00Z"
      to: "2026-09-03T00:00:00Z"
    }
  ) {
    room
    bucketStart
    stats { count min max average sum }
  }
}
```

The latest reading of each type, for the live tiles:

```graphql
query Latest {
  latestReadings(types: [ENERGY, AIR_QUALITY]) {
    room
    type
    receivedAt
    ... on EnergyReading { energyAmount }
    ... on AirQualityReading { co2 }
  }
}
```

Room cards, plus the values for a filter dropdown:

```graphql
query Dashboard {
  rooms {
    room
    totalReadings
    latestReading { type receivedAt }
    latestByType { type receivedAt }
  }
  availableRooms
}
```

### Notes on the query surface

* **Filtering and sorting are bound explicitly**, not auto-generated. `room`,
  `type`, `receivedAt` and `ingestedAt` are whitelisted; auto-binding every
  field would produce a sprawling filter surface and let a client build
  unindexed predicates at will.
* **Offset paging**, not Relay cursors, because the dashboard table wants page
  numbers and a total count. Default page size 20, maximum 100. Switching to
  cursor paging is a one-attribute change if the frontend prefers `fetchMore`.
* **`metricAggregation` takes a `field`, not a type.** The field determines the
  reading type, so grouping by type as well would always yield a single group.
  Type narrowing uses `OfType<T>()` rather than a cast, which puts an indexed
  `WHERE` on the discriminator and makes cross-type contamination impossible —
  the failure mode here is plausible-looking wrong averages, not an exception.
* **`MOTION_DETECTED` maps the boolean to 1/0**, so `sum` is the detection count
  and `average` is the detection rate. One code path, no special-case query.
* **An ungrouped aggregate over zero rows returns one bucket with `count: 0`**,
  not an empty list — `GROUP BY` yields no group for zero rows, and a client
  reads a zero more easily than an absence.
* **Aggregation windows are validated.** `MINUTE` and `HOUR` require both `from`
  and `to`; `MINUTE` allows at most 24 hours, `HOUR` at most 90 days; at most 50
  rooms per query. A five-year window at minute resolution is 2.6 million
  buckets, which the database would happily compute and the response would take
  the process down with. Failures come back as one GraphQL error per broken
  rule, each with `extensions.code = VALIDATION_FAILED` and the offending field.

### Limits

| Knob | Value | Where |
|---|---|---|
| Default / max page size | 20 / 100 | `AddPresentation` |
| Max field & type cost | 5 000 | `ModifyCostOptions` |
| Max execution depth | 8 | `AddMaxExecutionDepthRule` |
| Execution timeout | 30 s | `ModifyRequestOptions` |

---

## Configuration

Environment-variable form uses `__` for nesting, as in compose.

| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:MetricsDb` | *(empty — required)* | The database `DataProcessor` writes to. Startup fails if unset. |
| `Cors:AllowedOrigins` | `[]` (dev: localhost 3000 / 5173) | Frontend origins. Empty means no cross-origin caller is allowed — a wide-open default is not something to inherit by accident. |
| `GraphQL:AllowIntrospection` | `false` | Opt-in override. HotChocolate 16 disables introspection outside Development by default; set this instead of pretending a deployment is Development just to get codegen. |
| `Serilog:MinimumLevel:*` | `Information` | Standard Serilog configuration section. |
| `Serilog:UseJsonConsole` | *(unset)* | Compact JSON console. Defaults to on outside Development, off inside; set it explicitly either way. |
| `OpenTelemetry:OtlpEndpoint` | *(empty)* | No endpoint, no exporter — registering one unconditionally would retry a collector that is not there on every interval. |

### Observability

Serilog replaces the default logger; a bootstrap logger is installed before the
host is built so a bad connection string is written *somewhere* instead of the
container exiting silently. `UseSerilogRequestLogging()` is first in the
pipeline, so its one-line summary times everything below it.

`AddInstrumentation()` from `HotChocolate.Diagnostics` creates activities; when
`OpenTelemetry:OtlpEndpoint` is set, traces and metrics leave over OTLP for a
collector to expose. There is no Prometheus scrape endpoint in-process: the EF
Core instrumentation and the Prometheus ASP.NET Core exporter have never shipped
a stable release, so traces come from the EF `ActivitySource` directly and
metrics go to a collector.

`GraphQlErrorFilter` logs the full exception and returns a stable
`extensions.code` — `VALIDATION_FAILED` or `INTERNAL_SERVER_ERROR`.
Messages and stack traces are masked outside Development, so no SQL and no
connection string can leak. Validation errors are the exception: their messages
describe the caller's own mistake, so they pass through unmasked.

---

## Non-goals

* **No GraphQL subscriptions.** The spec assigns real-time push to a separate
  Notification Service over SignalR, and that is where it stays. HotChocolate
  could do it, which is not a reason to. If it ever moves here, the Gateway would
  bind its own MassTransit queue to the existing `metric-readings` fanout
  exchange — the fanout means an extra consumer is free — and republish through
  `ITopicEventSender`.
* **No writes and no migrations.** See above; enforced by a throwing
  `SaveChanges`.
* **No HTTPS redirection.** In a container there is no dev certificate and, in
  compose, no HTTPS port, so `UseHttpsRedirection()` would either no-op with a
  warning or 307 clients to a port nothing listens on. TLS belongs at the edge.
* **No RabbitMQ dependency.** Unlike the other two services, this one touches
  only the database.

---

## CI

`.github/workflows/build-and-push.yml`: `codestyle-check` → (`test`,
`export-schema`) → `smoke` → `build-and-push`. PRs are gated; only pushes
publish an image, tagged both `:latest` and `:${{ github.sha }}` so a deploy can
name a commit. Unlike the sibling repos, restore needs no `GITHUB_TOKEN` and the
Dockerfile no build secret — the Gateway consumes no private packages.

The `smoke` job is what the unit and integration suites cannot cover. It builds
the image, starts a real SQL Server beside it, applies the writer's schema from
the same checked-in copy the integration tests use, and then asserts that the
container comes up, `/health` goes green, `/health/live` answers separately, and
a GraphQL query returns without errors. It also runs **the exact healthcheck
command `docker-compose.yml` declares** inside the image — that probe uses bash's
`/dev/tcp` because the runtime image ships no curl or wget, and it must be `CMD`
rather than `CMD-SHELL` since `/bin/sh` there is dash and has no `/dev/tcp`. A
broken probe leaves a perfectly healthy service permanently unhealthy, which is
invisible without running it, so it is checked on every pull request.

Image: `ghcr.io/sailtor/innowise.d3s1t1.gateway`.

---

## TODO

Nothing here is required for the service to do its job; these are the next
things worth building, and the first is the one this design actually invites.

### A scheduled job that fails when the read model drifts

The whole read-model decision above rests on one integration test: it applies
`DataProcessorSchema.sql` — a **checked-in copy** of the writer's schema — and
runs every Gateway query against it. That copy is refreshed by hand, so it can
drift from the writer, and if it does, the tests keep passing while production
breaks. The guard has a gap in exactly the shape of the risk it guards.

The fix is a scheduled workflow that regenerates the script from
`DataProcessor` and fails on a diff:

```yaml
on:
  schedule:
    - cron: '0 6 * * 1'      # Monday morning, before anyone needs the answer
  workflow_dispatch:

jobs:
  schema-drift:
    runs-on: ubuntu-latest
    steps:
      # Check out both repos, regenerate the script from the writer's migrations
      # with `dotnet ef migrations script --idempotent`, then:
      #   git diff --exit-code -- tests/.../Schema/DataProcessorSchema.sql
      # A non-zero exit is the alarm: the writer moved and this repo has not.
```

Two details decide whether it is worth having. It needs read access to the
`DataProcessor` repository, which for a private repo means a token rather than
the default `GITHUB_TOKEN`. And it should open an issue or fail loudly rather
than auto-committing the regenerated script — silently accepting the new schema
would defeat the purpose, since the point is that a human reads the diff and
decides whether the Gateway's entities need to change with it.

### Smaller things

* **Indexes tuned to these query shapes.** Approved in principle and deferred
  until the real SQL existed; it now does. Lands as a migration in
  `DataProcessor`, which owns the schema — see that repo's README.
* **The bonus `deploy` job.** CI now smoke-tests the image it is about to
  publish (see below), but nothing deploys it anywhere.
