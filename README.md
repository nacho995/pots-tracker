# POTS Self-Management PWA

Progressive Web App for patient self-management of POTS (Postural Orthostatic
Tachycardia Syndrome). Installable from the browser, works offline, encrypts
health data locally.

> This is a tracking and education tool, **not** a diagnostic tool. It does
> not replace clinical care.

## What it does (planned scope)

- Daily Green / Orange / Red status with sub-60-second entry.
- Symptom tracking across cardiovascular, neurological, GI, autonomic, and
  functional domains.
- Vital signs (HR lying/sitting/standing, BP, sleep, steps, weather).
- Preventive actions checklist (hydration, compression, pacing, heat
  avoidance, sleep, medication adherence; salt only if clinician-prescribed).
- Episode log triggered automatically on Red status.
- Rescue actions menu paired with emergency triage.
- Patient-facing trends ("associated with," never "caused by").
- Exportable doctor report (PDF + CSV).

## Stack

- **Frontend:** Blazor WebAssembly PWA on .NET 10 LTS.
- **Backend:** ASP.NET Core 10 Minimal API.
- **DB:** PostgreSQL 16 with Row-Level Security (multi-tenant from day 1).
- **Auth:** Magic-link email (no passwords). HS256 JWT.
- **Hosting target:** Fly.io + Neon Postgres (when deployed).

## Project layout

```
pots-pwa/
├─ Pots.sln
├─ docker-compose.yml            # Postgres 16 on localhost:55432
├─ docker-init/                  # one-shot role bootstrap
├─ src/
│  ├─ Pots.Domain/               # entities, value objects, invariants — pure
│  ├─ Pots.Infrastructure/       # EF Core 10, RLS interceptor, migrations
│  ├─ Pots.Shared/               # DTOs shared between API and Client
│  ├─ Pots.Api/                  # ASP.NET Core 10 Minimal API
│  └─ Pots.Client/               # Blazor WASM PWA
└─ tests/
   ├─ Pots.Domain.Tests/         # 41 tests — invariants, factories, pinning
   ├─ Pots.Infrastructure.Tests/ # 7 tests — RLS isolation, audit append-only
   └─ Pots.Api.Tests/            # (placeholder)
```

## Security model

Two Postgres roles:

- `pots_dev` — owns the schema, runs migrations. **Superuser in dev** (Docker
  default); in production must be created **without SUPERUSER** for FORCE
  RLS to bind it.
- `pots_app` — what the API connects as. Subject to RLS on every query.

The `RlsCommandInterceptor` pins `app.current_user_id` on every command via
`set_config`, and policies in migration `EnableRowLevelSecurity` enforce:

- Patient rows visible only to owner + active grantees.
- Mutations visible only to owner + active *editor* grantees.
- Audit log is append-only: REVOKE UPDATE/DELETE on `pots_app` + a BEFORE
  UPDATE/DELETE trigger that raises on every mutation attempt (even for the
  table owner).
- Sign-in bootstrap uses two `SECURITY DEFINER` functions
  (`auth_find_user_by_email`, `auth_provision_user`) that bypass RLS for the
  specific anonymous-caller paths.

See `CLAUDE.md` for the full safety rule set.

## Local development

Prerequisites: .NET 10 SDK, Docker, optionally Node only if you want to use
Lighthouse against a running build.

```bash
# 1. Start Postgres (port 55432 to avoid clashes with a host Postgres).
docker compose up -d

# 2. Export the migrations connection string. EF tooling fails fast if unset.
export POTS_CONNECTION_STRING="Host=localhost;Port=55432;Database=pots;Username=pots_dev;Password=pots_dev_only_change_me"

# 3. Apply migrations.
dotnet ef database update --project src/Pots.Infrastructure

# 4. Run the API (terminal 1).
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Pots.Api --urls http://localhost:5050

# 5. Run the client (terminal 2).
dotnet run --project src/Pots.Client
# Then open the URL it prints (http://localhost:5185 by default).
```

The magic-link "email" in development is logged to the API console
(`[DEV EMAIL] To: ... Subject: ...`). Copy the link into the browser to
complete sign-in.

## Tests

```bash
dotnet test
```

Domain tests run instantly. Infrastructure tests spin up Postgres via
Testcontainers — first run is slow because of the image pull.

## Development workflow

This repo uses Claude Code with an **adversarial dual-reviewer workflow**
defined in `CLAUDE.md`. After any meaningful code change, two agents review
the diff in parallel:

- `code-defender` — argues the code is fit for purpose; pushes back against
  unnecessary churn (YAGNI, KISS).
- `code-critic` — argues the code violates Clean Code, SOLID, scalability,
  accessibility, or POTS safety rules; demands refactors.

The loop continues until both agents agree the code is clean, SOLID,
scalable, production-grade, and compliant with the project's safety and PWA
constraints. Hard cap at 5 iterations to surface impasses to the human.

## Hard safety rules (excerpt — see `CLAUDE.md` for full list)

- No diagnosis, no treatment recommendations.
- Salt features clinician-gated, never on by default.
- Emergency triage always surfaces for red-flag symptoms.
- Insights use "associated with," never "caused by."
- Health data encrypted at rest and in transit.
- No streak shaming, no overexertion gamification.
- Patient owns their data; clinician access is read-only on patient-entered
  fields.

## Status (v1)

Full functional spec from `CLAUDE.md` implemented.

- ✅ Solution scaffolded (Clean Architecture: Domain/Infrastructure/Shared/Api/Client).
- ✅ Postgres + EF Core 10 + migrations + dual-role model.
- ✅ Row-Level Security with FORCE RLS, owner+grantee policies, audit triggers
  across **10 user-data tables**.
- ✅ Magic-link auth: 32-byte token, SHA-256 hash, single-use, 15-min TTL,
  sibling-token invalidation on verify, rate-limited. Invite flow auto-provisions
  the grantee account and sends a magic-link.
- ✅ Patient profile + name editing (`/profile`).
- ✅ Permission management (`/grants`): invite Viewer/Editor, revoke.
- ✅ Daily Status Verde/Naranja/Rojo (`/today`) — sub-60-second entry.
- ✅ Episode log (`/episode/new`) with emergency-triage banner.
- ✅ Rescue actions menu (`/rescue`) — salt action clinician-gated.
- ✅ Symptoms entry (`/log/symptoms`) — 28+ 0-10 scales grouped by system.
- ✅ Vital signs entry (`/log/vitals`) — HR lying/2'/5'/10', BP triple, sleep, steps.
- ✅ Preventive actions checklist (`/log/actions`) — daily upsert with
  salt-gated section.
- ✅ Settings (`/settings`) — hydration, compression, sleep, exercise plan,
  salt-gate toggle with explicit clinician attestation modal.
- ✅ Trends (`/trends`) — G/O/R distribution, episodes per week, top triggers,
  average symptom burden. Language: "asociado con", never "causa".
- ✅ Doctor Report (`/report`) — print-to-PDF view via browser + CSV download.
- ✅ `/shared` — perfiles compartidos contigo (SECURITY DEFINER function for
  the cross-RLS join).
- ✅ Blazor WASM PWA: editorial design system (Fraunces + General Sans),
  warm paper + ocre accent, dark/light mode, `prefers-reduced-motion`,
  touch targets ≥48px, all in Spanish.
- ✅ **63 regression tests** (56 Domain + 7 Infrastructure integration with
  Testcontainers + Postgres), covering salt-gate enforcement, RLS isolation,
  audit append-only, magic-link single-use, email validation.

### Hardening pending for production deploy

- Self-host fonts (avoid CDN dependency for offline-first).
- Forwarded-headers middleware so rate-limiter buckets per real client IP.
- Per-email rate limit on `/auth/request-link` (in addition to per-IP).
- Refresh-token flow with revocation (current JWT lifetime 24h).
- Move `Jwt:SigningKey` to env var / secret store; remove dev placeholder.
- Self-host email provider (replace `ConsoleEmailSender`).
- Postgres `pots_dev` role created **without** SUPERUSER so `FORCE ROW LEVEL
  SECURITY` actually binds the migrations role too.
- One-shot signed-URL endpoint for the CSV download (currently the link uses
  the bearer in a way that only works from authed contexts).

### Previously legacy notes (kept for context)

- Magic-link auth: 32-byte token, SHA-256 hash, single-use, 15-min TTL,
  sibling-token invalidation on verify, rate-limited.
- Patient profile feature: GET/POST/PUT `/me/patient`.
- Permission management: `/me/patient/grants` (list, invite Viewer/Editor,
  revoke).
- Blazor WASM PWA: login, magic-link verify (URL fragment, not query
  string), profile, grants pages. Dark/light mode, prefers-reduced-motion,
  ≥44px touch targets, Spanish copy.
- ✅ Regression test suite (48 tests, 0 failures).

Pending for v1:
- Day-tracking UI (Green/Orange/Red, symptom entry, episode log).
- Vital signs entry + trends.
- Doctor report export (PDF/CSV).
- Production deployment: pots_dev as non-superuser, secrets from env, HTTPS
  termination, Forwarded Headers for rate limiter, real email provider.
- v2: smartwatch integration (Garmin/Fitbit/Apple via cloud API),
  predictive alerts (regulatory work required).
