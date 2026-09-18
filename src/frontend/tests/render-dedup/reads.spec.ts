import { randomUUID } from "node:crypto"
import { readFile } from "node:fs/promises"
import { createServer, type Server } from "node:http"
import { test, expect } from "@playwright/test"

const workspaceId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
const projectId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
const workspacePath = `/workspaces/${workspaceId}`
const projectsEndpoint = `/api/workspaces/${workspaceId}/projects`
const date = "2026-09-17T00:00:00Z"
let server: Server
let calls: { path: string; cookie: string; method: string }[] = []
let projectName = "Website"
let failWorkspaces = false
let failProjects = false
let failWrites = false
let removed = false
let revoked = false
let role = "owner"
let token = ""
const contentPath = `${workspacePath}/projects/${projectId}/content-types/articles`
const typesEndpoint = `${projectsEndpoint}/${projectId}/content-types`
const entriesEndpoint = `${typesEndpoint}/articles/entries`
function sessionCookie(user = "alice") { return `cms_session=${user}-${token}` }
function contentType() { return { id: projectId, projectId, key: "articles", createdAt: date, updatedAt: date, fields: [] } }


function project() {
  return { id: projectId, workspaceId, name: projectName, createdAt: date, updatedAt: date }
}

function count(path: string, cookie = sessionCookie()) {
  return calls.filter((call) => call.path === path && call.cookie === cookie && call.method === "GET").length
}

function shellCounts(cookie = sessionCookie()) {
  return [count("/api/auth/session", cookie), count("/api/me/workspaces", cookie), count(projectsEndpoint, cookie)]
}

async function load(request: import("@playwright/test").APIRequestContext, path = workspacePath, user = "alice") {
  return request.get(path, { headers: { Cookie: sessionCookie(user) }, maxRedirects: 0 })
}

test.beforeAll(async () => {
  server = createServer(async (req, res) => {
    const url = new URL(req.url!, "http://localhost")
    const path = url.pathname.toLowerCase()
    const cookie = req.headers.cookie ?? ""
    calls.push({ path: path === entriesEndpoint ? path + url.search : path, cookie, method: req.method! })
    res.setHeader("Content-Type", "application/json")
    const user = (cookie.split("=")[1] ?? "").split("-")[0]
    let body: unknown
    if (path === "/api/auth/session") {
      if (!user || user === "invalid" || revoked) { res.writeHead(401); res.end("{}"); return }
      body = { userId: user, email: `${user}@example.com`, platformRole: "User", idleExpiresAt: date, absoluteExpiresAt: date }
    } else if (path === "/api/me/workspaces") {
      if (failWorkspaces) { res.writeHead(503); res.end("{}"); return }
      body = removed ? [] : [{ id: workspaceId, name: `${user} workspace`, createdAt: date, currentRole: role }]
    } else if (path === projectsEndpoint) {
      if (failProjects) { res.writeHead(503); res.end("{}"); return }
      body = [project()]
    } else if (path === `${projectsEndpoint}/${projectId}`) {
      if (req.method === "PUT") {
        if (failWrites) { res.writeHead(400); res.end(JSON.stringify({ detail: "Rejected update" })); return }
        let raw = ""
        for await (const chunk of req) raw += chunk
        projectName = JSON.parse(raw).name
      }
      body = project()
    } else if (path === `${projectsEndpoint}/${projectId}/content-types`) {
      body = [contentType()]
    } else if (path === `${typesEndpoint}/articles`) {
      body = contentType()
    } else if (path === entriesEndpoint) {
      body = { items: [], total: 0, page: 1, pageSize: 25 }
    } else if (path === `${projectsEndpoint}/${projectId}/preview`) {
      body = { name: projectName, currentRole: role, contentTypeCount: 1, publishedEntryCount: 0, draftEntryCount: 0, recentEntries: [] }
    } else { res.writeHead(404); res.end("{}"); return }
    res.end(JSON.stringify(body))
  })
  await new Promise<void>((resolve) => server.listen(3211, "127.0.0.1", resolve))
})
test.afterAll(async () => { await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve())) })
test.beforeEach(() => { calls = []; projectName = "Website"; failWorkspaces = false; failProjects = false; failWrites = false; removed = false; revoked = false; role = "owner"; token = randomUUID() })

test("workspace reads are deduplicated", async ({ request }) => {
  expect((await load(request)).status()).toBe(200)
  const counts = shellCounts()
  console.log(`Workspace backend reads: ${counts.join(" + ")} = ${counts.reduce((a, b) => a + b, 0)}`)
  expect(counts).toEqual([1, 1, 1])
})

test("nested content retains distinct reads", async ({ request }) => {
  expect((await load(request, `${workspacePath}/projects/${projectId}/content`)).status()).toBe(200)
  expect(shellCounts()).toEqual([1, 1, 1])
  expect(count(`${projectsEndpoint}/${projectId}`)).toBe(1)
  expect(count(`${projectsEndpoint}/${projectId}/content-types`)).toBe(1)
})

test("uppercase route IDs share the project read", async ({ request }) => {
  expect((await load(request, `/workspaces/${workspaceId.toUpperCase()}`)).status()).toBe(200)
  await load(request)
  expect(shellCounts()).toEqual([2, 2, 1])
})

test("warm requests check access and reuse cached projects", async ({ request }) => {
  expect(await (await load(request)).text()).toContain("Website")
  projectName = "Updated website"
  expect(await (await load(request)).text()).toContain("Website")
  expect(shellCounts()).toEqual([2, 2, 1])
  console.log("Workspace reads: cold 3, warm 2 (session + membership)")
})

test("concurrent sessions remain isolated", async ({ request }) => {
  const responses = await Promise.all([load(request), load(request, workspacePath, "bob")])
  const bodies = await Promise.all(responses.map((response) => response.text()))
  expect(bodies[0]).toContain("alice workspace")
  expect(bodies[0]).not.toContain("bob workspace")
  expect(bodies[1]).toContain("bob workspace")
  expect(bodies[1]).not.toContain("alice workspace")
  expect(shellCounts()).toEqual([1, 1, 1])
  expect(shellCounts(sessionCookie("bob"))).toEqual([1, 1, 1])
})

test("failed reads are shared and retried next request", async ({ request }) => {
  failWorkspaces = true
  expect(await (await load(request)).text()).toContain("Unable to load workspace")
  expect(count("/api/me/workspaces")).toBe(1)
  failWorkspaces = false
  expect(await (await load(request)).text()).toContain("Website")
  expect(count("/api/me/workspaces")).toBe(2)
})

test("invalid sessions still redirect", async ({ request }) => {
  const response = await load(request, workspacePath, "invalid")
  expect(response.status()).toBe(307)
  expect(response.headers().location).toBe("/auth/login")
  expect(count("/api/auth/session", sessionCookie("invalid"))).toBe(1)
})

async function submitAction(request: import("@playwright/test").APIRequestContext, path: string, field: string, value: string) {
  const html = await (await load(request, path)).text()
  const form = [...html.matchAll(/<form\b[^>]*>[\s\S]*?<\/form>/g)].map((match) => match[0]).find((form) => form.includes(`name="${field}"`))
  expect(form).toBeTruthy()
  const fields: Record<string, string> = {}
  for (const input of form!.matchAll(/<input\b[^>]*>/g)) {
    const name = input[0].match(/name="([^"]+)"/)?.[1]
    const value = input[0].match(/value="([^"]*)"/)?.[1] ?? ""
    if (name?.startsWith("$ACTION")) fields[name] = value.replaceAll("&quot;", '"').replaceAll("&amp;", "&").replaceAll("&#x27;", "'")
  }
  expect(Object.keys(fields).some((key) => key.startsWith("$ACTION"))).toBe(true)
  const reference = Object.keys(fields).find((key) => key.startsWith("$ACTION_REF_"))!
  const prefix = reference.slice("$ACTION_REF_".length)
  const metadata = JSON.parse(fields[`$ACTION_${prefix}:0`]) as { id: string }
  const bound = JSON.parse(fields[`$ACTION_${prefix}:1`]) as unknown[]
  // Submit the same action arguments as the hydrated client, encoding FormData
  // as React's $K reference rather than posting progressive-enhancement state.
  const multipart = new FormData()
  multipart.append(`_1_${field}`, value)
  multipart.append("0", JSON.stringify([...bound, "$K1"]))
  return request.post(path, {
    headers: { Cookie: sessionCookie(), Origin: "http://127.0.0.1:3210", "Next-Action": metadata.id },
    multipart,
    timeout: 10_000,
  })
}

test("project rename action writes once and renders fresh data", async ({ request }) => {
  const path = `${workspacePath}/projects/${projectId}/manage`
  await load(request, workspacePath, "bob")
  const response = await submitAction(request, path, "name", "Renamed website")
  expect(response.status()).toBe(200)
  expect(await response.text()).toContain("Renamed website")
  expect(calls.filter((call) => call.method === "PUT")).toHaveLength(1)
  expect(await (await load(request)).text()).toContain("Renamed website")
  expect(await (await load(request, workspacePath, "bob")).text()).toContain("Renamed website")
})


test("sort and canonical filters reuse cached content reads", async ({ request }) => {
  await load(request, contentPath)
  await load(request, `${contentPath}?sort=$id`)
  await load(request, contentPath)
  expect(count(`${typesEndpoint}/articles`)).toBe(1)
  expect(count(projectsEndpoint)).toBe(1)
  expect(calls.filter((call) => call.path.startsWith(entriesEndpoint))).toHaveLength(2)
  await load(request, `${contentPath}?filter[title][eq]=one&filter[$status][eq]=draft`)
  await load(request, `${contentPath}?filter[$status][eq]=draft&filter[title][eq]=one`)
  expect(calls.filter((call) => call.path.startsWith(entriesEndpoint))).toHaveLength(3)
})

test("removed membership cannot return warm projects", async ({ request }) => {
  await load(request)
  removed = true
  const response = await load(request)
  expect(response.status()).toBe(307)
  expect(response.headers().location).toBe("/")
  expect(count(projectsEndpoint)).toBe(1)
})

test("revoked sessions cannot return warm projects", async ({ request }) => {
  await load(request)
  revoked = true
  const response = await load(request)
  expect(response.status()).toBe(307)
  expect(response.headers().location).toBe("/auth/login")
  expect(count(projectsEndpoint)).toBe(1)
})

test("authorization failure blocks warm resource reads", async ({ request }) => {
  await load(request)
  failWorkspaces = true
  const html = await (await load(request)).text()
  expect(html).toContain("Unable to load workspace")
  expect(count(projectsEndpoint)).toBe(1)
})

test("preview role changes bypass cached responses", async ({ request }) => {
  const path = `${workspacePath}/projects/${projectId}`
  expect(await (await load(request, path)).text()).toContain(">Project settings<")
  role = "member"
  const html = await (await load(request, path)).text()
  expect(html).not.toContain(">Project settings<")
  expect(count(`${projectsEndpoint}/${projectId}/preview`)).toBe(2)
})

test("failed cold resource reads are not cached", async ({ request }) => {
  failProjects = true
  expect(await (await load(request)).text()).toContain("Unable to load projects")
  failProjects = false
  expect(await (await load(request)).text()).toContain("Website")
  expect(count(projectsEndpoint)).toBeGreaterThanOrEqual(2)
})

test("expired reads refresh in the background", async ({ request }) => {
  test.setTimeout(60_000)
  await load(request)
  projectName = "External update"
  await new Promise((resolve) => setTimeout(resolve, 31_000))
  await load(request)
  await expect.poll(async () => (await (await load(request)).text()), { timeout: 10_000 }).toContain("External update")
  expect(count(projectsEndpoint)).toBe(2)
})


test("project deletion confirms the uncached current name", async ({ request }) => {
  const path = `${workspacePath}/projects/${projectId}/manage`
  await load(request, path)
  projectName = "Externally renamed"
  // The delete dialog is not rendered while closed. Resolve its action from
  // the production manifest instead of hardcoding a build-dependent ID.
  const manifest = JSON.parse(await readFile(".next/server/server-reference-manifest.json", "utf8")) as { node: Record<string, { exportedName: string }> }
  const actionId = Object.entries(manifest.node).find(([, action]) => action.exportedName === "deleteProjectAction")![0]
  const multipart = new FormData()
  multipart.append("_1_confirmation", "Website")
  multipart.append("0", JSON.stringify([workspaceId, projectId, { status: "idle", message: null, fieldErrors: {} }, "$K1"]))
  const response = await request.post(path, { headers: { Cookie: sessionCookie(), Origin: "http://127.0.0.1:3210", "Next-Action": actionId }, multipart })
  expect(await response.text()).toContain("Enter the current project name")
  expect(calls.filter((call) => call.method === "DELETE")).toHaveLength(0)
  expect(count(`${projectsEndpoint}/${projectId}`)).toBeGreaterThanOrEqual(2)
})

test("failed mutations do not invalidate warm resources", async ({ request }) => {
  const path = `${workspacePath}/projects/${projectId}/manage`
  await load(request)
  failWrites = true
  const response = await submitAction(request, path, "name", "Rejected rename")
  expect(await response.text()).toContain("Rejected update")
  await load(request)
  expect(count(projectsEndpoint)).toBe(1)
})
