import { beforeEach, describe, expect, it, vi } from "vitest"

const cookieStore = {
  get: vi.fn(),
  set: vi.fn(),
  delete: vi.fn(),
}

vi.mock("server-only", () => ({}))
vi.mock("next/headers", () => ({
  cookies: vi.fn(async () => cookieStore),
}))

import {
  getSession,
  mirrorBackendSessionCookie,
} from "./session"

describe("backend session cookie", () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.unstubAllGlobals()
    process.env.BACKEND_URL = "https://backend.example"
  })

  it("mirrors the opaque backend cookie with its security attributes", async () => {
    await mirrorBackendSessionCookie(
      "cms_session=opaque-secret; Path=/; Expires=Mon, 24 Aug 2027 12:00:00 GMT; Secure; HttpOnly; SameSite=Lax",
      "2027-08-24T12:00:00Z",
    )

    expect(cookieStore.set).toHaveBeenCalledWith(
      "cms_session",
      "opaque-secret",
      {
        httpOnly: true,
        secure: true,
        sameSite: "lax",
        path: "/",
        expires: new Date("2027-08-24T12:00:00Z"),
      },
    )
  })

  it("verifies dashboard sessions by forwarding only the opaque cookie", async () => {
    cookieStore.get.mockReturnValue({ value: "opaque-secret" })
    const fetchMock = vi.fn(async (
      input: RequestInfo | URL,
      init?: RequestInit,
    ) => {
      expect(input).toBe("https://backend.example/api/auth/session")
      expect(init?.headers).toEqual({ Cookie: "cms_session=opaque-secret" })
      return new Response(JSON.stringify({
        userId: "user-1",
        email: "user@example.test",
        platformRole: "User",
        idleExpiresAt: "2026-09-23T12:00:00Z",
        absoluteExpiresAt: "2027-08-24T12:00:00Z",
      }), { status: 200 })
    })
    vi.stubGlobal("fetch", fetchMock)

    const session = await getSession()

    expect(session?.userId).toBe("user-1")
    expect(fetchMock).toHaveBeenCalledOnce()
  })
})
