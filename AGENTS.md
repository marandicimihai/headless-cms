# AGENTS.md

## Current Repository Reality
- This repo is at scaffold stage: `src/` is currently empty and `headless-cms.slnx` contains no projects.
- Treat `README.md` as the primary source of intent; treat implementation details as **not yet defined** until code lands.
- If you add assumptions (framework setup, folder layout, tooling), label them explicitly as proposed.

## Big Picture (from discoverable sources)
- Product intent: a simplicity-first, API-first headless CMS (`README.md`).
- Planned backend boundary: `.NET` API using **FastEndpoints** (`README.md`).
- Planned persistence boundary: **PostgreSQL** as primary store (`README.md`).
- External client boundary: hosted Postman collection for API exploration (linked from `README.md`).

## Practical Working Conventions for Agents
- Keep new implementation under `src/` unless a different structure is explicitly introduced.
- Keep docs and code aligned: if you add runnable components, update `README.md` with exact run/test commands.
- Prefer minimal, explicit architecture matching stated goals ("simple and maintainable" in `README.md`).
- When introducing major structure (projects, layers, modules), document the rationale in the PR/commit message.

## Frontend UI Conventions
- Build frontend UI **shadcn-first**: use the official shadcn component or composition pattern whenever one exists before writing a custom equivalent.
- Add missing primitives through the shadcn CLI and keep generated components under `src/frontend/src/components/ui/`.
- Compose pages from shadcn primitives with as little custom styling as possible. Limit page-level Tailwind classes to layout, spacing, and responsive placement; use the shared theme tokens and component variants for visual styling.
- Do not recreate shadcn behavior such as sidebars, drawers, breadcrumbs, menus, dialogs, form controls, cards, or tables with bespoke markup and state unless the available shadcn component cannot satisfy a documented requirement.
- Keep route-specific UI in its `page.tsx` or `layout.tsx`. Do not create tiny pass-through files or extract a component used by only one page merely to shorten that page.
- Prefer locality over artificial file separation: a longer page file is easier to follow than a chain of single-use wrappers. Extract only meaningful reuse, independently complex behavior, or a framework-required boundary such as a Server Action file.

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
- Keep API contract discoverable for non-.NET clients; maintain the hosted Postman collection when endpoints are introduced.

## High-Value Next Structural Milestones
- Add at least one API project under `src/` and include it in `headless-cms.slnx`.
- Add a concrete configuration example for PostgreSQL connection settings.
- Add initial endpoint + persistence slice that demonstrates the intended FastEndpoints + PostgreSQL flow.
