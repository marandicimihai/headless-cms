# Headless CMS

A simplicity-first headless CMS built with **FastEndpoints** and **PostgreSQL**.

This project focuses on a clean API-first approach so content can be managed once and delivered anywhere (web, mobile, or other clients).

## Goals

- Keep the architecture simple and easy to maintain.
- Build fast, focused endpoints with FastEndpoints.
- Use PostgreSQL as the primary data store.
- Provide a Postman collection for quick API exploration and testing.

## Tech Stack

- **Backend:** .NET + FastEndpoints
- **Database:** PostgreSQL (pgsql)
- **API Testing:** Hosted Postman collection

## Project Structure

```text
headless-cms/
└── src/
    ├── backend/
    │   ├── headless-cms.slnx
    │   ├── HeadlessCms.Api/
    │   │   ├── Auth/
    │   │   ├── Content/
    │   │   │   └── Models/
    │   │   ├── Endpoints/
    │   │   │   ├── Auth/
    │   │   │   ├── Content/
    │   │   │   ├── Projects/
    │   │   │   └── Workspaces/
    │   │   └── Workspaces/
    │   └── HeadlessCms.Api.Tests/
    └── frontend/
```

Endpoints use vertical slices: each endpoint has its own file containing its
request, response, validation, mapping, and endpoint-specific helpers. An
endpoint file never contains a second endpoint. Shared domain models and
services remain in their owning feature modules.

## API and Postman

The hosted Postman collection makes it easy to:

- test endpoint behavior,
- understand request/response shapes,
- speed up local development and collaboration.

[Open the Headless CMS collection in Postman](https://go.postman.co/collection/30832597-e019a46d-66de-4a93-90a3-53a14453bd92).

### Authentication and workspace API

Authentication is email-based and normal user registration is invitation-only.
Authenticated requests use an opaque PostgreSQL-backed `cms_session` cookie.
The cookie is HttpOnly, host-only, `SameSite=Lax`, secure outside development,
and is never returned in a JSON response. API clients such as Postman should
enable their cookie jar and reuse the cookie set by login or invitation
registration.

| Method | Route | Access |
| --- | --- | --- |
| `POST` | `/api/auth/login` | Anonymous |
| `GET` | `/api/auth/session` | Authenticated, current session metadata |
| `POST` | `/api/auth/logout` | Anonymous/idempotent, revokes the presented session |
| `POST` | `/api/auth/invitations/preview` | Anonymous, masked invitation details |
| `POST` | `/api/auth/invitations/register` | Anonymous, valid invitation |
| `POST` | `/api/auth/invitations/accept` | Authenticated invited user |
| `POST` | `/api/workspaces` | Authenticated user, creates an `Owner` membership |
| `GET` | `/api/workspaces` | `PlatformAdmin` |
| `GET` | `/api/workspaces/{workspaceId}` | `PlatformAdmin` or workspace member |
| `PATCH` | `/api/workspaces/{workspaceId}` | `PlatformAdmin` or workspace owner |
| `DELETE` | `/api/workspaces/{workspaceId}` | Workspace owner |
| `GET` | `/api/me/workspaces` | Authenticated user |
| `DELETE` | `/api/me/workspaces/{workspaceId}` | Workspace editor/member |
| `POST` | `/api/workspaces/{workspaceId}/invitations` | Workspace owner |
| `GET` | `/api/workspaces/{workspaceId}/invitations` | Workspace owner |
| `POST` | `/api/workspaces/{workspaceId}/invitations/{invitationId}/resend` | Workspace owner |
| `DELETE` | `/api/workspaces/{workspaceId}/invitations/{invitationId}` | Workspace owner |
| `GET` | `/api/workspaces/{workspaceId}/members` | Workspace owner |
| `PATCH` | `/api/workspaces/{workspaceId}/members/{userId}` | Workspace owner |
| `DELETE` | `/api/workspaces/{workspaceId}/members/{userId}` | Workspace owner |
| `POST` | `/api/workspaces/{workspaceId}/ownership-transfer` | Workspace owner |

Workspace deletion takes no request body. The manage page requires an exact
workspace name match in its confirmation popup; this check is client-side only.
The API enforces owner access and returns `204 No Content` after permanently
deleting the workspace, projects, content types, fields, entries, memberships,
and invitations. User accounts remain. Non-owners (including platform
administrators without an owner membership) and missing workspaces return `404`.

### Private project API

Projects are private and owned by workspaces. Every project route requires a
valid session cookie and resolves the current user's membership for the
workspace in the route:

- workspace `Owner` and `Editor` roles can create, read, update, and delete
  projects;
- workspace `Member` roles can list and read projects, but write attempts return
  `403 Forbidden`;
- users without a workspace membership receive `404 Not Found`, so workspace and
  project existence is not disclosed.

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/api/workspaces/{workspaceId}/projects` | Create a workspace project |
| `GET` | `/api/workspaces/{workspaceId}/projects` | List the workspace's projects |
| `GET` | `/api/workspaces/{workspaceId}/projects/{id}` | Get one workspace project |
| `PUT` | `/api/workspaces/{workspaceId}/projects/{id}` | Rename one workspace project |
| `DELETE` | `/api/workspaces/{workspaceId}/projects/{id}` | Delete one workspace project |

Create and update requests use this shape:

```json
{
  "name": "My project"
}
```

Project names are trimmed and must contain between 3 and 100 characters.

## Database

This CMS uses PostgreSQL. Typical configuration will include:

- host
- port
- database name
- username
- password

Connection details should be provided through environment variables or local configuration (for example, `appsettings.Development.json`).

Apply the EF Core migrations:

```bash
dotnet ef database update \
  --project src/backend/HeadlessCms.Api/HeadlessCms.Api.csproj \
  --startup-project src/backend/HeadlessCms.Api/HeadlessCms.Api.csproj
```

Build and test the API:

```bash
dotnet build src/backend/headless-cms.slnx
dotnet test src/backend/headless-cms.slnx
```

Run the API locally:

```bash
dotnet run --project src/backend/HeadlessCms.Api/HeadlessCms.Api.csproj
```

Run and verify the frontend:

```bash
cd src/frontend
pnpm dev
pnpm test
pnpm lint
pnpm build
pnpm test:render-dedup
```

`pnpm test:render-dedup` builds with webpack and verifies render request counts,
session isolation, warm Next.js data-cache reads, and mutation freshness against
a local mock backend. Resource reads revalidate every 30 seconds; session and
workspace access checks stay uncached. Successful Server Actions expire the
workspace cache immediately. Ports
3210 and 3211 must be available; no browser installation is required.

## Development Notes

Planned core capabilities:

- content type management,
- content item CRUD,
- publish/unpublish workflow,
- filtering and pagination.

## Authorization model

Authorization has separate platform and workspace scopes:

- A user has one platform role: `PlatformAdmin` or `User`.
- A user can belong to many workspaces.
- Each workspace membership has one workspace role: `Owner`, `Editor`, or `Member`.
- Any authenticated user can create a workspace and immediately becomes its
  owner. A user can own at most `Workspaces:MaximumOwnedWorkspaces` workspaces.

The development admin configured with `Auth:AdminEmail` is assigned
`PlatformAdmin`. Each authenticated request resolves the user's current platform
role from the database and exposes it as a standard `role` claim. Workspace
permissions must be resolved from the authenticated user's membership for the
requested `WorkspaceId`; platform roles must not be used as workspace
permissions.

Sessions remain valid for 30 days of inactivity and have a one-year absolute
limit. Activity updates `LastSeenAt` and rolls the idle expiry at most once every
24 hours. A user can have at most ten active sessions; a new login prunes expired
sessions and evicts the least recently used session when necessary. Logout
revokes only the current browser session. Deleting a user cascades to all of that
user's sessions.

The workspace invitation model stores only a hash of each single-use token.
Resending rotates the token, and accepted, expired, or revoked invitations
cannot be reused. Development logs invitation URLs; production must register an
`IInvitationEmailSender` implementation. Invitation URLs use
`Frontend:BaseUrl` and point to
`/auth/invitations/accept?token={single-use-token}`. New invitees create their
password on that page and are signed in automatically. New passwords must be
15–64 characters long and may use any characters; existing credentials remain
accepted at login for backwards compatibility. Invitees with an existing
account sign in before accepting the invitation.

The workspace ownership limit defaults to `10` in `appsettings.json`. Override
`Workspaces:MaximumOwnedWorkspaces` in local configuration or with the
`Workspaces__MaximumOwnedWorkspaces` environment variable.

Required production configuration:

```text
Auth__AdminEmail
Auth__AdminPassword
ConnectionStrings__DefaultConnection
Frontend__BaseUrl
```

## Dynamic content

Content types belong to projects, and projects belong to workspaces. Entry data
uses relational field definitions with PostgreSQL `jsonb` values. Creating a
content type does not create a .NET class, PostgreSQL table, or migration.

The workflow is:

1. An `Owner` or `Editor` creates a project inside a workspace.
2. They define content types inside that project.
3. Entries are created and queried through the project-scoped content type.

The initial field types are:

- `text`
- `number`
- `boolean`

Definitions and entries are available below the workspace boundary:

```text
GET  /api/workspaces/{workspaceId}/projects/{projectId}/content-types
POST /api/workspaces/{workspaceId}/projects/{projectId}/content-types
GET  /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}
PUT  /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}
DELETE /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}

GET    /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries
POST   /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries
GET    /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries/{entryId}
PUT    /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries/{entryId}
DELETE /api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries/{entryId}
```

`Owner` and `Editor` memberships can manage definitions and entries. `Member`
memberships have read-only access. Platform administrators do not bypass
workspace membership checks.

Example content type:

```json
{
  "key": "article",
  "fields": [
    {
      "key": "title",
      "type": "text",
      "required": true
    },
    {
      "key": "views",
      "type": "number",
      "settings": { "default": 0 }
    },
    {
      "key": "published",
      "type": "boolean",
      "required": true
    }
  ]
}
```

Entry bodies use a generic JSON object:

```json
{
  "data": {
    "title": "PostgreSQL for CMS",
    "views": 125,
    "published": true
  },
  "status": "published"
}
```

Entry lists support pagination, an optional `draft` or `published` status,
typed sorting, and declared-field filters:

```text
?page=1&pageSize=25&status=published
?sort=-views
?filter[views][gte]=100&sort=-views
```

Text fields support `eq` and `contains`; number fields support `eq`, `gt`,
`gte`, `lt`, and `lte`; boolean fields support `eq`.

Each content type has one current schema, and newly created or updated entries
are validated against it.
Updating a definition changes only the content type and field definitions.
Existing entries are never rewritten: their stored JSON and timestamps remain
unchanged, removed fields remain in their data, and newly configured defaults
are not backfilled. The current schema is applied when an entry is created or
updated.
Keys identify and label content types and fields. Existing field keys cannot
change type. Deleting a content type also deletes all of its fields and entries.
Content type keys are unique within a project, so separate projects can
independently define a content type with the same key.

The `ResetDynamicContentToSingleSchema` development migration intentionally
discards existing content types, fields, and entries while preserving projects,
workspaces, memberships, and authentication data.

### Workspace search

Authenticated workspace members can search the projects, content types, and
text-field entry values they can access:

```text
GET /api/workspaces/{workspaceId}/search?query={text}&limit=5
```

Queries are trimmed and must contain 2–100 characters; `limit` defaults to 5
and accepts 1–10. Results are grouped into `projects`, `contentTypes`, and
`entries`, each with `items` and a total match count. Entry matches contain the
project and content-type context, matching field key, status, and a bounded
text snippet. Search is case-insensitive substring matching and considers only
declared `text` fields; it never searches raw JSON, numeric, or boolean values.
Results are workspace-scoped and no result is returned to users without a
workspace membership.

## Build and Test

Restore and build the solution:

```bash
dotnet restore src/backend/headless-cms.slnx
dotnet build src/backend/headless-cms.slnx --no-restore
```

Run the integration tests:

```bash
dotnet test src/backend/HeadlessCms.Api.Tests/HeadlessCms.Api.Tests.csproj
```

Docker must be running because the standard test command includes PostgreSQL
18 integration tests powered by Testcontainers. Override the default
`postgres:18-alpine` image when matching another deployed PostgreSQL version:

```bash
TEST_POSTGRES_IMAGE=postgres:17-alpine \
  dotnet test src/backend/HeadlessCms.Api.Tests/HeadlessCms.Api.Tests.csproj
```

The test suite uses xUnit, `FastEndpoints.Testing`, and Shouldly to boot the
complete API pipeline. Endpoint behavior tests run in parallel against EF Core
InMemory using one application host per collection, test authentication, a
unique database per test, and teardown cleanup. Authentication, concurrency,
migration, relational-constraint, and PostgreSQL-specific tests share a
serialized disposable PostgreSQL Testcontainer; its schema is reset and all
migrations are reapplied before each test.

Apply PostgreSQL migrations:

```bash
dotnet tool restore
dotnet ef database update --project src/backend/HeadlessCms.Api/HeadlessCms.Api.csproj
```

Configure PostgreSQL with `ConnectionStrings__DefaultConnection` or the
equivalent `DefaultConnection` value in local configuration.

## Status

Initial project setup in progress.

## License

TBD

### Preview summaries

The workspace and project preview pages use dedicated read-only endpoints:

| Method | Route | Summary |
| --- | --- | --- |
| `GET` | `/api/workspaces/{workspaceId}/preview` | Project, entry, publishing-status, and member counts; per-project content type and entry counts and last entry update |
| `GET` | `/api/workspaces/{workspaceId}/projects/{id}/preview` | Content type and publishing-status counts, plus the ten most recently updated entries |

Both require workspace membership, including for platform administrators. The
workspace response includes `pendingInvitationCount` only for owners (null for
other roles); expired, accepted, and revoked invitations are excluded. Counts
cover all matching records. Last content update means the latest `UpdatedAt`
among a project's existing entries, or null when it has no entries. Recent
entries sort by update time descending with entry ID as a stable tie-breaker.
The frontend displays timestamps in UTC and hides summary cards for workspaces
without projects and projects without content types.

Verify these contracts against PostgreSQL with Docker running:

```bash
dotnet test src/backend/HeadlessCms.Api.Tests/HeadlessCms.Api.Tests.csproj --filter FullyQualifiedName~PreviewEndpointTests
```

### Extending backend field types

Field-specific value validation, filtering, sorting, and text-search eligibility
live in `src/backend/HeadlessCms.Api/Content/FieldTypes/`. The coordinating services
retain document rules, defaults, system fields, pagination, and search ranking.

To add a field type, add a `ContentFieldType` enum member, implement
`IContentFieldTypeHandler`, and register it explicitly in `Program.cs`. Keep query
expressions translatable by EF Core and PostgreSQL. Set `SupportsTextSearch` only
for types storing JSON strings that should participate in workspace search.

Removing a handler registration disables that type without deleting stored fields
or entry values. Definition validation, entry writes, and filtering or sorting
that require the disabled handler fail with a validation error; workspace search
excludes it. Duplicate handler registrations fail at application startup. Existing
enum values and their serialized/database names must remain stable.
