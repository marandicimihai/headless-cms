This is a [Next.js](https://nextjs.org) project bootstrapped with [`create-next-app`](https://nextjs.org/docs/app/api-reference/cli/create-next-app).

## Getting Started

Set the backend URL; no frontend signing or encryption secret is required:

```text
BACKEND_URL=http://localhost:5123
```

The Next.js app acts as the browser-facing BFF. Login mirrors the backend's
opaque HttpOnly `cms_session` cookie, server-side API helpers forward it
automatically, dashboard access is verified with `GET /api/auth/session`, and
logout revokes the backend session before clearing the browser cookie.

Invitation links open `/auth/invitations/accept?token=...`. A new invitee sets
a password there and is signed in automatically. An existing user can sign in
from that page and is returned to the invitation to join the workspace.

First, run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

You can start editing the page by modifying `app/page.tsx`. The page auto-updates as you edit the file.

## Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

You can check out [the Next.js GitHub repository](https://github.com/vercel/next.js) - your feedback and contributions are welcome!

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.

## Render request deduplication

Session, workspace membership, and workspace project reads share results within
a server render using React `cache()`. Session and membership checks use
`no-store` on every new server request; mutations are not memoized.

Project lists/details, schemas, entry-list pages, and previews additionally use
Next.js's fetch Data Cache with `force-cache` and a 30-second revalidation
interval. Cache keys retain the forwarded HttpOnly session cookie, isolating
sessions. Fresh session and membership checks run before each cached read and
fail closed; cached previews with an outdated role are fetched uncached.
Individual entry reads, search, member/invitation lists, and mutation
prerequisites remain uncached. UUIDs and filter order are normalized for reuse.

Successful frontend Server Actions expire the workspace tag using `updateTag`
before existing page revalidation/redirects. This invalidates all its cached
reads across sessions. Direct backend changes appear through background
revalidation: the first expired read may return stale data, and refresh failures
can retain it longer, so 30 seconds is not a hard maximum age. Authentication
and membership checks remain fresh even when resource data is stale.

No TanStack Query or Cache Components configuration is required. Shared
cache/invalidation coordination must be configured when deploying multiple
Next.js instances.

Run from `src/frontend`:

```bash
pnpm test
pnpm lint
pnpm test:render-dedup
```

The integration command builds the production app with webpack and uses Playwright HTTP
requests against Next.js with a local mock backend. No browser download or real
backend is required. Ports 3210 and 3211 must be available. Tests cover request
counts, UUID normalization, separate requests, concurrent sessions, failures,
redirects, warm resource reads, canonical sorting/filtering, expiry, role changes,
and Server Action invalidation. On the mock-backed production server, a cold
workspace load makes 3 backend calls and a warm load makes 2 (session and
membership). New table sorting variants fetch entries; warm variants skip that
request. These are backend API counts, not PostgreSQL command measurements.

To run the production build and integration tests separately, use:

```bash
pnpm exec next build --webpack
pnpm exec playwright test
```

Local listening ports must still be permitted to run integration tests.
