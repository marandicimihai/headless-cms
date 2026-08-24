---
name: build-headless-cms-frontend
description: Build and refine the Headless CMS Next.js control-panel frontend using this project's architecture, shadcn/Base UI conventions, flat visual style, real backend contracts, and accessibility requirements. Use for frontend routes, layouts, navigation, dashboards, workspace/project/content/member screens, forms, empty states, API integration, or UI refactors in src/frontend.
---

# Build the Headless CMS frontend

Apply these project-specific preferences whenever changing `src/frontend`. Read the root and frontend `AGENTS.md` files first; the user's current request overrides this skill.

## Ground the change

1. Inspect the existing route, nearby components, API helpers, and types before designing.
2. Read the relevant installed Next.js 16 documentation under `src/frontend/node_modules/next/dist/docs/` before using framework conventions.
3. Inspect backend endpoints, request/response records, authorization checks, and tests when the UI represents backend data or actions.
4. Treat repository behavior as authoritative. Do not invent fields, counts, permissions, actions, or routes.

## Keep routing and layout direct

- Put signed-in control-panel routes under `src/frontend/src/app/(dashboard)/`. The route group must not alter public URLs.
- Keep `src/frontend/src/app/auth/` outside the dashboard route-group layout.
- Put the actual shared sidebar, header, content inset, and breadcrumb composition directly in `(dashboard)/layout.tsx`.
- Do not add a thin shell component whose only purpose is to return layout markup. Extract a component only when it owns meaningful reusable behavior or substantial independent UI.
- Make pages render feature content only; let the route-group layout own shared chrome.
- Derive active navigation and route labels from the current pathname. Do not hardcode one item as permanently active.

## Use a flat, quiet visual language

- Build shadcn-first. Add an official primitive through the shadcn CLI before creating a custom equivalent, and keep generated primitives in `src/frontend/src/components/ui/`.
- Prefer typography, whitespace, semantic grouping, `border-y`, and `divide-y` over boxed surfaces.
- Do not default management, list, or empty-state pages to Card components, colored panel backgrounds, rings, shadows, or repeated rounded containers.
- Use cards only when the content genuinely needs discrete visual enclosure, such as an established analytical dashboard pattern.
- Start pages with a concise `h1` and muted supporting description. Keep page-level Tailwind classes focused on layout, spacing, and responsive placement.
- Keep typography compact: reserve larger text sizes for page titles and primary entity names; use semibold or bold weight for secondary emphasis instead of increasing type size, and avoid unnecessary size contrast between a section heading and its supporting copy.
- Use existing theme tokens; do not introduce one-off colors or decorative styling.
- Render collections as semantic lists, tables, or definition lists according to the data rather than as a grid of decorative cards.

## Make states purposeful

- When a collection is empty, hide zero-value summary or information blocks.
- Show one focused empty state directly on the page without a card background.
- Give the empty state concise guidance and a real call to action that matches an implemented backend or navigation workflow.
- Do not add dead links, inert buttons, or controls that imply unsupported behavior.
- Show backend failures with the shared Alert primitive and safe, actionable copy.

## Preserve real data and permissions

- Keep authenticated API access server-side. Read the encrypted session on the server, pass bearer tokens only from server-only helpers, and use `cache: "no-store"` for user-specific data.
- Model frontend types from the exact backend JSON contract, including nullable fields and string-enum serialization.
- Derive displayed summaries from returned data only.
- Respect platform and workspace roles exactly as implemented. Do not assume platform administrators are workspace members.
- For the Workspaces index, preserve membership-only listing through `GET /api/me/workspaces`; do not silently switch administrators to the global workspace endpoint.
- Keep invitation messaging consistent with the backend's invitation-based membership flow.

## Preserve native semantics

- Use native elements for their intended behavior and keep keyboard, form, and screen-reader behavior intact.
- When a Base UI `Button` renders a non-button element such as a Next.js `Link`, set `nativeButton={false}`.
- Use a real native button for button actions; do not suppress Base UI semantic warnings instead of fixing the rendered element contract.
- Give icon-only controls accessible names and use semantic headings and landmark elements.

## Validate the result

1. Keep the change narrowly scoped and remove obsolete wrappers or imports created by the refactor.
2. Run `pnpm lint` from `src/frontend`.
3. Run the production build. If Turbopack is blocked only by the execution sandbox, verify with `pnpm exec next build --webpack` and report the limitation accurately.
4. Run `git diff --check` and inspect the final route tree when changing App Router structure.
