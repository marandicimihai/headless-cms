# Headless CMS

Headless CMS is a simplicity-first content management system with a web admin panel and an API for managing workspaces, projects, content types, and content entries. It is built with a .NET API, FastEndpoints, PostgreSQL, and a Next.js frontend.

[Watch the admin panel demo](https://youtu.be/mgmrfMevGoI).

## Run locally

Requirements: Git and Docker with Docker Compose. The default development configuration is included, so the commands below run without creating a `.env` file:

```bash
git clone https://github.com/marandicimihai/headless-cms.git
cd headless-cms
docker compose up --build
```

The stack requires a PostgreSQL connection and an admin email and password. Compose supplies these using development defaults (`HeadlessCms`, `postgres`, `postgres`, `admin@example.com`, and `password`). To configure them before starting, create a root `.env` file and set:

```dotenv
POSTGRES_DB=HeadlessCms
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=password
```

These credentials are for local development only. See [`.env.example`](.env.example) for optional port and integration settings.

When the services are ready, open [http://localhost:3000](http://localhost:3000). Sign in with the development account:

- Email: `admin@example.com`
- Password: `password`

Compose starts the frontend, API, and PostgreSQL database, then initializes the database and development admin. Source files are mounted for live development reloads. Stop the stack with `Ctrl+C`; run `docker compose down` to stop it later. To remove the database and start fresh, run `docker compose down --volumes`.

The project’s API, data model, and detailed development notes are in [Project reference](docs/project-reference.md).
