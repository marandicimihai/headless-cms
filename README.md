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

### Private project API

Projects are private and owned by the user identified by the access token's
`sub` claim. Every project route requires a bearer token. Collection and
resource queries are scoped to that user, and attempts to read, update, or
delete another user's project return `404 Not Found`.

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/api/projects` | Create a project for the current user |
| `GET` | `/api/projects` | List only the current user's projects |
| `GET` | `/api/projects/{id}` | Get one owned project |
| `PUT` | `/api/projects/{id}` | Rename one owned project |
| `DELETE` | `/api/projects/{id}` | Delete one owned project |

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
