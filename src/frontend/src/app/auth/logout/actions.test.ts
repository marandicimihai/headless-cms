import { describe, expect, it, vi } from "vitest"

const mocks = vi.hoisted(() => ({
  logout: vi.fn(async () => {
    throw new TypeError("backend unavailable")
  }),
  deleteSession: vi.fn(async () => {}),
}))

vi.mock("server-only", () => ({}))
vi.mock("@/lib/api/auth", () => ({ logout: mocks.logout }))
vi.mock("@/lib/api/session", () => ({ deleteSession: mocks.deleteSession }))
vi.mock("next/navigation", () => ({
  redirect: vi.fn(() => {
    throw new Error("NEXT_REDIRECT")
  }),
}))

import { logoutAction } from "./actions"

describe("logoutAction", () => {
  it("always clears the browser cookie when backend revocation fails", async () => {
    await expect(logoutAction()).rejects.toThrow("NEXT_REDIRECT")
    expect(mocks.logout).toHaveBeenCalledOnce()
    expect(mocks.deleteSession).toHaveBeenCalledOnce()
  })
})
