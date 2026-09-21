# Self-hosting with Docker Compose

Production runs one frontend, one API, Caddy, and an initializer. PostgreSQL can
run in the same Compose project or be supplied externally. Development
continues to use the root `compose.yaml`; production uses only `compose.prod.yaml`.
The project names and volumes are separate. Do not combine these two files with
multiple `-f` flags.

## First deployment

Install Docker Engine/Desktop with Compose v2, allocate enough memory for the
configured limits (defaults total roughly 3 GiB plus Docker/build overhead), and
point your hostname's DNS records at the server. Inbound TCP 80/443 must reach
Caddy for automatic HTTPS. Allow UDP 443 if using HTTP/3. Build-time access to
NuGet, npm, Docker registries, and Google Fonts is required.

From the repository root:

```bash
cp .env.production.example .env.production
chmod 600 .env.production
# Edit .env.production before continuing.
docker compose --env-file .env.production -f compose.prod.yaml --profile bundled config --quiet
docker compose --env-file .env.production -f compose.prod.yaml --profile bundled up -d --build
docker compose --env-file .env.production -f compose.prod.yaml --profile bundled ps -a
```

Set `APP_DOMAIN` to a hostname only, e.g. `cms.example.com`. Choose a unique
`RELEASE_TAG` per release (never `latest`) and an administrator email/password.
The admin password must contain 15–64 characters. Use a strong random database
password and keep the connection string consistent with the bundled service credentials:

```text
DATABASE_CONNECTION_STRING='Host=postgres;Port=5432;Database=headless_cms;Username=headless_cms;Password=YOUR_PASSWORD;Maximum Pool Size=20'
```

Single-quote environment-file values containing `$`. Connection-string special
characters need provider-specific escaping; generated hexadecimal passwords
avoid that complexity. Real credentials are not committed or sent to image
build contexts. Compose environment values can still be read by host/Docker
administrators. Use `config --quiet` rather than printing resolved secrets.

Initialization retries transient database connection failures, applies migrations,
and seeds/promotes the configured administrator under a PostgreSQL advisory lock.
The API starts only after initialization exits successfully. A later initialization
preserves the admin password, but the configured admin email remains authoritative.
Changing `ADMIN_PASSWORD` is not a password-reset mechanism. No workspaces,
projects, or sample content are seeded. Normal API containers receive no admin
credentials. `initialize` showing `Exited (0)` is expected.

Visit `https://cms.example.com`. The frontend and backend use the same secure,
HttpOnly, host-only `cms_session` cookie with path `/`:

- `/api` and `/api/*` go to the API without stripping the prefix.
- `/bff/search` and all other paths go to Next.js.
- Frontend server requests use `http://api:8080` over Docker networking.
- No database, frontend, or API port is published on the host.

Caddy stores certificates in its named volume. API HTTPS redirects are disabled
because the edge enforces HTTPS; internal HTTP health probes remain functional.
Forwarded headers are trusted only from Caddy's fixed private address. If
`172.30.20.0/24` conflicts with your network, change `PROXY_SUBNET`,
`PROXY_DYNAMIC_RANGE`, and `PROXY_ADDRESS` together. The dynamic range must be
inside the subnet and exclude Caddy's address so other containers cannot claim it.

## Shareable invitations

There is no email service to configure. Workspace owners create invitations in
the manage screen, then copy and share the displayed link with the invited person.
The create and regenerate endpoints return an `invitationUrl` with `Cache-Control:
no-store`. Invitation listings never return it; the database stores only its token
hash. Generate a new link from the invitation menu if the original is lost. This
invalidates the old link and refreshes expiry. Links use the public `APP_DOMAIN`.
The existing `/resend` API route is retained for compatibility but now means
regenerate a shareable link. The legacy `lastSentAt` field records last generation,
not email delivery. No invitation tokens are logged by the configured sender;
Caddy also removes the token query parameter and Referer header from access logs.

## External PostgreSQL

Omit `--profile bundled` from all application commands. Set the database connection
string to the external service and configure its TLS requirements
(`SSL Mode=VerifyFull` with trusted CAs).
Use PostgreSQL 18 for compatibility with the bundled backup client; a newer
server requires upgrading that client. The initializer requires migration/schema
permissions. No automatic migration of existing development data takes place.

For backup tools, independently configure `BACKUP_PGHOST`, `BACKUP_PGPORT`,
`BACKUP_PGSSLMODE`, `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD` to match
the external database. A Docker container's `localhost` is not the host machine.

## Health, logs, resources, and shutdown

```bash
docker compose --env-file .env.production -f compose.prod.yaml logs --tail 100 api initialize
docker compose --env-file .env.production -f compose.prod.yaml logs --follow frontend caddy
docker compose --env-file .env.production -f compose.prod.yaml exec api curl -fsS http://127.0.0.1:8080/health/ready
docker compose --env-file .env.production -f compose.prod.yaml --profile bundled down
```

`/health/live` and `/health/ready` are internal API paths and are not routed to the
API publicly. Readiness checks PostgreSQL. Docker marks unhealthy processes, but
does not restart them solely because a health check fails. Process exits restart
automatically. Investigate unhealthy services through logs.

Application roots are read-only. Frontend cache and temporary files use disposable
tmpfs mounts; database data and TLS state use named volumes. Logs rotate at
10 MB with three files per container. Tune resource limits in the example env
file for your host; build-time memory can exceed runtime limits. API shutdown has
30 seconds to drain within Docker's 40-second grace period.

`down` preserves volumes. **`down --volumes` permanently deletes this deployment's
database and certificates.** Never use it for an ordinary upgrade.

## Production load testing

Production Compose includes an opt-in k6 service on the private data network. It
calls the API container directly, so measurements focus on API and PostgreSQL
performance rather than public TLS or Caddy overhead. Set a GET path
that already exists and is accessible to the load-test account:

```bash
LOAD_TEST_PATH=/api/workspaces/WORKSPACE_ID/projects \
docker compose --env-file .env.production -f compose.prod.yaml \
  --profile load-test run --rm load-test
```

`LOAD_TEST_EMAIL` and `LOAD_TEST_PASSWORD` should identify a dedicated account;
they fall back to the configured administrator when omitted. Defaults are 10
virtual users for 30 seconds with one warmup request. Configure the load through
the `LOAD_TEST_*` variables in `.env.production`.

Do not run a capacity test against a user-facing deployment without confirming
that its resource limits and traffic window can tolerate the requested load.

## Upgrades and rollback

Single-instance upgrades briefly interrupt service. Review migrations and take
an off-host backup first. Check out the release, set a new `RELEASE_TAG`, then:

```bash
# Build while the existing app is still serving traffic.
docker compose --env-file .env.production -f compose.prod.yaml build
bash src/deploy/backup.sh /srv/cms-backups
# Stop public traffic and application processes before schema changes.
docker compose --env-file .env.production -f compose.prod.yaml stop caddy frontend api
docker compose --env-file .env.production -f compose.prod.yaml run --rm --no-deps initialize
# Only continue if initialization succeeded. Do not bypass a failed migration.
docker compose --env-file .env.production -f compose.prod.yaml --profile bundled up -d --no-build
docker compose --env-file .env.production -f compose.prod.yaml --profile bundled ps -a
```

The final `up` may rerun the idempotent initializer; this is safe. Do not use
`restart` to deploy an image or environment change. For application rollback, stop
the app, restore the previous source/config and `RELEASE_TAG`, and run `up -d
--no-build` with retained images **only if the database schema is backward
compatible**. Database rollback is a separate, explicit restore operation.
Changing a PostgreSQL major-version image tag does not upgrade its data: use
dump/restore or a planned `pg_upgrade`. Review pinned image updates regularly.

## Backups and restore drills

```bash
bash src/deploy/backup.sh /srv/cms-backups
# Create a SEPARATE empty database first, then test restoration.
bash src/deploy/restore.sh /srv/cms-backups/cms-TIMESTAMP.dump cms_restore_test
```

Backup uses a consistent custom-format `pg_dump` snapshot, written with private
permissions. Failed backups retain a `.partial` suffix and must not be used.
Restore requires an
existing target database, an absolute dump path, and typing the target name to
confirm overwriting its objects. It restores in one transaction and fails on
errors. Stop application writes before restoring the real production database.
Dumps do not contain cluster roles, external secrets, or TLS volumes;
store deployment secrets separately and securely. Restore credentials need
permission to recreate the dumped objects.

Use `CMS_ENV_FILE=/absolute/path/to/env` to select a different environment file.
Example host cron entry (adapt the repository path):

```cron
0 2 * * * cd /srv/headless-cms && bash src/deploy/backup.sh /srv/cms-backups >> /srv/cms-backup.log 2>&1
```

Copy completed dumps off-host using your backup service; retain at least seven
daily and four weekly snapshots and verify a restore monthly. Retention cleanup
is operator-managed: these scripts never delete old backups automatically.
