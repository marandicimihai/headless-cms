# AGENTS.md

## Current Repository Reality
- This repo is at scaffold stage: `src/` is currently empty and `headless-cms.slnx` contains no projects.
- Treat `README.md` as the primary source of intent; treat implementation details as **not yet defined** until code lands.
- If you add assumptions (framework setup, folder layout, tooling), label them explicitly as proposed.

## Big Picture (from discoverable sources)
- Product intent: a simplicity-first, API-first headless CMS (`README.md`).
- Planned backend boundary: `.NET` API using **FastEndpoints** (`README.md`).
- Planned persistence boundary: **PostgreSQL** as primary store (`README.md`).
- Planned external client boundary: Postman collection for API exploration (`README.md`, suggested path `postman/headless-cms.postman_collection.json`).

## Practical Working Conventions for Agents
- Keep new implementation under `src/` unless a different structure is explicitly introduced.
- Keep docs and code aligned: if you add runnable components, update `README.md` with exact run/test commands.
- Prefer minimal, explicit architecture matching stated goals ("simple and maintainable" in `README.md`).
- When introducing major structure (projects, layers, modules), document the rationale in the PR/commit message.

## Developer Workflows (what is known vs unknown)
- Known: repository has no runnable API project yet, so build/test/debug commands are currently undefined.
- Unknown until code exists: package manager setup, migration workflow, test framework, local bootstrap scripts.
- First contributor adding executable code should establish and document:
  - build command(s),
  - test command(s),
  - local run command(s),
  - required environment variables for PostgreSQL.

## Integration Points to Preserve
- Database config should come from environment or local config (example noted in `README.md`: `appsettings.Development.json`).
- Keep API contract discoverable for non-.NET clients; maintain/update Postman artifacts when endpoints are introduced.

## High-Value Next Structural Milestones
- Add at least one API project under `src/` and include it in `headless-cms.slnx`.
- Add a concrete configuration example for PostgreSQL connection settings.
- Add initial endpoint + persistence slice that demonstrates the intended FastEndpoints + PostgreSQL flow.

