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

## Status

Initial project setup in progress.

## License

TBD

