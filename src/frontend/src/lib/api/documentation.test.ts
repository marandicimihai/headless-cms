import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))

import { getDocumentation } from "./documentation"

const contract = {
  openapi: "3.0.3",
  paths: {
    "/api/auth/session": {
      get: {
        operationId: "session",
        tags: ["Authentication"],
        summary: "Get session",
        responses: { "200": { description: "Success." } },
      },
    },
  },
  components: { schemas: {} },
}

afterEach(() => vi.unstubAllGlobals())

describe("getDocumentation", () => {
  it("reads the internal contract without forwarding the session", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(contract)))
    vi.stubGlobal("fetch", fetch)

    expect((await getDocumentation())?.operations).toHaveLength(1)
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/openapi/v1.json"),
      expect.objectContaining({ cache: "no-store" }),
    )
    expect(fetch.mock.calls[0][1]).not.toHaveProperty("headers")
  })

  it.each([
    new Response("{}", { status: 503 }),
    new Response("{}"),
    new Response("not json"),
  ])("returns null for unavailable or invalid API docs", async (response) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response))
    expect(await getDocumentation()).toBeNull()
  })
})
