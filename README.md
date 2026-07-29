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

Suggested location:

```text
postman/headless-cms.postman_collection.json
```

## Database

This CMS uses PostgreSQL. Typical configuration will include:

- host
- port
- database name
- username
- password

Connection details should be provided through environment variables or local configuration (for example, `appsettings.Development.json`).

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

The development admin configured with `Auth:AdminUsername` is assigned
`PlatformAdmin`. Login and refresh access tokens include the platform role as a
standard `role` claim. Tenant permissions must be resolved from the
authenticated user's membership for the requested `TenantId`; platform roles
must not be used as tenant permissions.

The tenant invitation model stores an email, tenant role, hashed single-use
token, expiration, inviter, and acceptance details. Registration and email
delivery endpoints are not implemented yet.

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

The authentication tests use the FastEndpoints-recommended xUnit,
`FastEndpoints.Testing`, `AppFixture`, route-less HTTP helpers, and Shouldly
setup. They boot the complete API pipeline with an isolated in-memory database.

## Status

Initial project setup in progress.

## License

TBD
