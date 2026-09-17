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

function project() {
  return { id: projectId, workspaceId, name: projectName, createdAt: date, updatedAt: date }
}

function count(path: string, cookie = "cms_session=alice") {
  return calls.filter((call) => call.path === path && call.cookie === cookie && call.method === "GET").length
}

function shellCounts(cookie = "cms_session=alice") {
  return [count("/api/auth/session", cookie), count("/api/me/workspaces", cookie), count(projectsEndpoint, cookie)]
}

async function load(request: import("@playwright/test").APIRequestContext, path = workspacePath, user = "alice") {
  return request.get(path, { headers: { Cookie: `cms_session=${user}` }, maxRedirects: 0 })
}

test.beforeAll(async () => {
  server = createServer(async (req, res) => {
    const path = new URL(req.url!, "http://localhost").pathname.toLowerCase()
    const cookie = req.headers.cookie ?? ""
    calls.push({ path, cookie, method: req.method! })
    res.setHeader("Content-Type", "application/json")
    const user = cookie.split("=")[1] ?? ""
    let body: unknown
    if (path === "/api/auth/session") {
      if (!user || user === "invalid") { res.writeHead(401); res.end("{}"); return }
      body = { userId: user, email: `${user}@example.com`, platformRole: "User", idleExpiresAt: date, absoluteExpiresAt: date }
    } else if (path === "/api/me/workspaces") {
      if (failWorkspaces) { res.writeHead(503); res.end("{}"); return }
      body = [{ id: workspaceId, name: `${user} workspace`, createdAt: date, currentRole: "owner" }]
    } else if (path === projectsEndpoint) {
      body = [project()]
    } else if (path === `${projectsEndpoint}/${projectId}`) {
      if (req.method === "PUT") {
        let raw = ""
        for await (const chunk of req) raw += chunk
        projectName = JSON.parse(raw).name
      }
      body = project()
    } else if (path === `${projectsEndpoint}/${projectId}/content-types`) {
      body = []
    } else { res.writeHead(404); res.end("{}"); return }
    res.end(JSON.stringify(body))
  })
  await new Promise<void>((resolve) => server.listen(3211, "127.0.0.1", resolve))
})
test.afterAll(async () => { await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve())) })
test.beforeEach(() => { calls = []; projectName = "Website"; failWorkspaces = false })

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
  expect(shellCounts()).toEqual([1, 1, 1])
})

test("new requests fetch changed data", async ({ request }) => {
  expect(await (await load(request)).text()).toContain("Website")
  projectName = "Updated website"
  expect(await (await load(request)).text()).toContain("Updated website")
  expect(shellCounts()).toEqual([2, 2, 2])
})

test("concurrent sessions remain isolated", async ({ request }) => {
  const responses = await Promise.all([load(request), load(request, workspacePath, "bob")])
  const bodies = await Promise.all(responses.map((response) => response.text()))
  expect(bodies[0]).toContain("alice workspace")
  expect(bodies[0]).not.toContain("bob workspace")
  expect(bodies[1]).toContain("bob workspace")
  expect(bodies[1]).not.toContain("alice workspace")
  expect(shellCounts()).toEqual([1, 1, 1])
  expect(shellCounts("cms_session=bob")).toEqual([1, 1, 1])
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
  expect(count("/api/auth/session", "cms_session=invalid")).toBe(1)
})

test("project rename action writes once and renders fresh data", async ({ request }) => {
  const path = `${workspacePath}/projects/${projectId}/manage`
  const html = await (await load(request, path)).text()
  const form = html.match(/<form\b[^>]*>[\s\S]*?name="name"[\s\S]*?<\/form>/)?.[0]
  expect(form).toBeTruthy()
  const fields: Record<string, string> = { name: "Renamed website" }
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
  multipart.append("_1_name", "Renamed website")
  multipart.append("0", JSON.stringify([...bound, "$K1"]))
  const response = await request.post(path, {
    headers: { Cookie: "cms_session=alice", Origin: "http://127.0.0.1:3210", "Next-Action": metadata.id },
    multipart,
    timeout: 10_000,
  })
  expect(response.status()).toBe(200)
  expect(await response.text()).toContain("Renamed website")
  expect(calls.filter((call) => call.method === "PUT")).toHaveLength(1)
  expect(await (await load(request)).text()).toContain("Renamed website")
})
