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
- **API Testing:** Postman collection (planned in this repo)

## Project Structure

```text
headless-cms/
├── headless-cms.slnx
└── src/
```

> The `src/` folder is where the API project and related code will live.

## API and Postman

A Postman collection will be included to make it easy to:

- test endpoint behavior,
- understand request/response shapes,
- speed up local development and collaboration.

Collection location:

```text
postman/headless-cms.postman_collection.json
```

### Authentication and tenant API

Authentication is email-based and normal user registration is invitation-only.
All authenticated routes require `Authorization: Bearer <access-token>`.

| Method | Route | Access |
| --- | --- | --- |
| `POST` | `/api/auth/login` | Anonymous |
| `POST` | `/api/auth/refresh` | Anonymous, valid refresh token |
| `POST` | `/api/auth/invitations/preview` | Anonymous, masked invitation details |
| `POST` | `/api/auth/invitations/register` | Anonymous, valid invitation |
| `POST` | `/api/auth/invitations/accept` | Authenticated invited user |
| `POST` | `/api/tenants` | `PlatformAdmin` |
| `GET` | `/api/tenants` | `PlatformAdmin` |
| `GET` | `/api/tenants/{tenantId}` | `PlatformAdmin` or tenant member |
| `PATCH` | `/api/tenants/{tenantId}` | `PlatformAdmin` or tenant owner |
| `GET` | `/api/me/tenants` | Authenticated user |
| `DELETE` | `/api/me/tenants/{tenantId}` | Tenant editor/member |
| `POST` | `/api/tenants/{tenantId}/invitations` | Tenant owner |
| `GET` | `/api/tenants/{tenantId}/invitations` | Tenant owner |
| `POST` | `/api/tenants/{tenantId}/invitations/{invitationId}/resend` | Tenant owner |
| `DELETE` | `/api/tenants/{tenantId}/invitations/{invitationId}` | Tenant owner |
| `GET` | `/api/tenants/{tenantId}/members` | Tenant owner |
| `PATCH` | `/api/tenants/{tenantId}/members/{userId}` | Tenant owner |
| `DELETE` | `/api/tenants/{tenantId}/members/{userId}` | Tenant owner |
| `POST` | `/api/tenants/{tenantId}/ownership-transfer` | Tenant owner |

### Private project API

Projects are private and owned by tenants. Every project route requires a
bearer token and resolves the current user's membership for the tenant in the
route:

- tenant `Owner` and `Editor` roles can create, read, update, and delete
  projects;
- tenant `Member` roles can list and read projects, but write attempts return
  `403 Forbidden`;
- users without a tenant membership receive `404 Not Found`, so tenant and
  project existence is not disclosed.

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/api/tenants/{tenantId}/projects` | Create a tenant project |
| `GET` | `/api/tenants/{tenantId}/projects` | List the tenant's projects |
| `GET` | `/api/tenants/{tenantId}/projects/{id}` | Get one tenant project |
| `PUT` | `/api/tenants/{tenantId}/projects/{id}` | Rename one tenant project |
| `DELETE` | `/api/tenants/{tenantId}/projects/{id}` | Delete one tenant project |

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
  --project src/HeadlessCms.Api/HeadlessCms.Api.csproj \
  --startup-project src/HeadlessCms.Api/HeadlessCms.Api.csproj
```

## Development Notes

Planned core capabilities:

- content type management,
- content item CRUD,
- publish/unpublish workflow,
- filtering and pagination.

## Authorization model

Authorization has separate platform and tenant scopes:

- A user has one platform role: `PlatformAdmin` or `User`.
- A user can belong to many tenants.
- Each tenant membership has one tenant role: `Owner`, `Editor`, or `Member`.

The development admin configured with `Auth:AdminEmail` is assigned
`PlatformAdmin`. Login and refresh access tokens include the platform role as a
standard `role` claim. Tenant permissions must be resolved from the
authenticated user's membership for the requested `TenantId`; platform roles
must not be used as tenant permissions.

The tenant invitation model stores only a hash of each single-use token.
Resending rotates the token, and accepted, expired, or revoked invitations
cannot be reused. Development logs invitation URLs; production must register an
`IInvitationEmailSender` implementation.

Required production configuration:

```text
Auth__SigningKey
Auth__AdminEmail
Auth__AdminPassword
ConnectionStrings__DefaultConnection
Tenancy__InvitationUrl
```

Deployments upgrading from the previous username-based admin may temporarily
set `Auth__LegacyAdminUsername` so startup can promote and re-key that account
to `Auth__AdminEmail`.

## Build and Test

Restore and build the solution:

```bash
dotnet restore headless-cms.slnx
dotnet build headless-cms.slnx --no-restore
```

Run the integration tests:

```bash
dotnet test src/HeadlessCms.Api.Tests/HeadlessCms.Api.Tests.csproj
```

Docker must be running because the standard test command includes PostgreSQL
18 integration tests powered by Testcontainers. Override the default
`postgres:18-alpine` image when matching another deployed PostgreSQL version:

```bash
TEST_POSTGRES_IMAGE=postgres:17-alpine \
  dotnet test src/HeadlessCms.Api.Tests/HeadlessCms.Api.Tests.csproj
```

The test suite uses xUnit, `FastEndpoints.Testing`, `AppFixture`, and Shouldly
to boot the complete API pipeline. Fast endpoint coverage uses an isolated
in-memory database, while provider behavior and ownership migrations run
against a disposable PostgreSQL container.

## Status

Initial project setup in progress.

## License

TBD
