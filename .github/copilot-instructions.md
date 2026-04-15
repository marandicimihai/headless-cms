# Copilot Instructions

## Repository Context
- Treat `README.md` as the source of product intent.
- The repository is currently in scaffold stage: `src/` is empty and `headless-cms.slnx` has no projects yet.
- Planned direction: API-first headless CMS using `.NET` + FastEndpoints with PostgreSQL.
- Keep implementation assumptions explicitly marked as **Proposed** until concrete code structure exists.

## Working Conventions
- Keep new implementation under `src/` unless a different structure is explicitly introduced.
- Prefer simple, explicit architecture aligned with maintainability goals.
- If runnable components are added, update `README.md` with exact build, test, and run commands.
- Keep database configuration sourced from environment variables or local config.
- When endpoints are introduced, keep API contracts discoverable and maintain Postman artifacts.

## Code Generation Style
- Generate comment-less code by default.
- Write code that is self-explanatory through clear naming, straightforward control flow, and sensible structure.
- Do not add comments unless explicitly requested by the user.

## Function Design
- Keep functions focused on one clear responsibility.
- Avoid overly complex functions.
- Avoid excessive micro-functions that fragment logic.
- Aim for balanced granularity: cohesive units that are easy to read, test, and evolve.

## Uncertainty Handling
- Do not present unverified build/runtime workflows as established facts.
- If setup details are not yet defined in repo files, state what is known and mark proposals clearly.

