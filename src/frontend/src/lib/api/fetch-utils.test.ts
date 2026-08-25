import { beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))
vi.mock("./session", () => ({
  getSessionCookie: vi.fn(async () => "opaque-secret"),
  SESSION_COOKIE_NAME: "cms_session",
}))

import { apiFetch } from "./fetch-utils"

describe("authenticated API forwarding", () => {
  beforeEach(() => {
    vi.unstubAllGlobals()
    process.env.BACKEND_URL = "https://backend.example"
  })

  it("adds the session cookie without bearer authorization", async () => {
    const fetchMock = vi.fn(async (_url: string, init?: RequestInit) => {
      const headers = new Headers(init?.headers)
      expect(headers.get("Cookie")).toBe("cms_session=opaque-secret")
      expect(headers.has("Authorization")).toBe(false)
      return new Response(JSON.stringify({ id: "workspace-1" }), { status: 200 })
    })
    vi.stubGlobal("fetch", fetchMock)

    const result = await apiFetch<{ id: string }>("/api/workspaces/1")

    expect(result.ok).toBe(true)
    expect(fetchMock).toHaveBeenCalledOnce()
  })

  it("does not refresh or retry an unauthorized response", async () => {
    const fetchMock = vi.fn(async () => new Response(
      JSON.stringify({ title: "Unauthorized", status: 401 }),
      { status: 401, headers: { "Content-Type": "application/problem+json" } },
    ))
    vi.stubGlobal("fetch", fetchMock)

    const result = await apiFetch("/api/workspaces/1")

    expect(result.ok).toBe(false)
    expect(fetchMock).toHaveBeenCalledOnce()
  })
})
