import { beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))
vi.mock("next/cache", () => ({ updateTag: vi.fn() }))
vi.mock("./fetch-utils", () => ({ apiFetch: vi.fn() }))
vi.mock("./session", () => ({ getSession: vi.fn() }))
vi.mock("./workspaces", () => ({ listMyWorkspaces: vi.fn() }))

import { updateTag } from "next/cache"
import { cachedWorkspaceRead, invalidateWorkspaceCache } from "./cached-read"
import { apiFetch } from "./fetch-utils"
import { getSession } from "./session"
import { listMyWorkspaces } from "./workspaces"

const id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
const error = { status: 503, detail: "Unavailable", fieldErrors: {} }

describe("cached workspace reads", () => {
  beforeEach(() => {
    vi.resetAllMocks()
    vi.mocked(getSession).mockResolvedValue({ userId: "alice", email: "alice@example.com", platformRole: "User", idleExpiresAt: "", absoluteExpiresAt: "" })
    vi.mocked(listMyWorkspaces).mockResolvedValue({ ok: true, data: [{ id, name: "Workspace", createdAt: "", currentRole: "member" }] })
    vi.mocked(apiFetch).mockResolvedValue({ ok: true, data: [] })
  })

  it("checks access before opting into tagged time-based caching", async () => {
    await cachedWorkspaceRead(id.toUpperCase(), "/resource")
    expect(apiFetch).toHaveBeenCalledWith("/resource", { cache: "force-cache", next: { revalidate: 30, tags: [`workspace:${id}`] } })
  })

  it("blocks revoked sessions without consulting the cache", async () => {
    vi.mocked(getSession).mockResolvedValue(null)
    expect(await cachedWorkspaceRead(id, "/resource")).toMatchObject({ ok: false, error: { status: 401 } })
    expect(apiFetch).not.toHaveBeenCalled()
  })

  it("requires membership even for platform administrators", async () => {
    vi.mocked(getSession).mockResolvedValue({ userId: "admin", email: "admin@example.com", platformRole: "PlatformAdmin", idleExpiresAt: "", absoluteExpiresAt: "" })
    vi.mocked(listMyWorkspaces).mockResolvedValue({ ok: true, data: [] })
    expect(await cachedWorkspaceRead(id, "/resource")).toMatchObject({ ok: false, error: { status: 403 } })
    expect(apiFetch).not.toHaveBeenCalled()
  })

  it("fails closed when membership checks fail", async () => {
    vi.mocked(listMyWorkspaces).mockResolvedValue({ ok: false, error })
    expect(await cachedWorkspaceRead(id, "/resource")).toEqual({ ok: false, error })
    expect(apiFetch).not.toHaveBeenCalled()
  })

  it("bypasses role-dependent previews after a role change", async () => {
    vi.mocked(apiFetch).mockResolvedValueOnce({ ok: true, data: { currentRole: "owner" } }).mockResolvedValueOnce({ ok: true, data: { currentRole: "member" } })
    const result = await cachedWorkspaceRead<{ currentRole: "owner" | "member" }>(id, "/preview", { roleOf: (data) => data.currentRole })
    expect(result).toEqual({ ok: true, data: { currentRole: "member" } })
    expect(apiFetch).toHaveBeenLastCalledWith("/preview", { cache: "no-store" })
  })

  it("keeps mutation prerequisites uncached", async () => {
    await cachedWorkspaceRead(id, "/project", { fresh: true })
    expect(apiFetch).toHaveBeenCalledExactlyOnceWith("/project", { cache: "no-store" })
  })

  it("normalizes workspace invalidation tags", () => {
    invalidateWorkspaceCache(id.toUpperCase())
    expect(updateTag).toHaveBeenCalledWith(`workspace:${id}`)
  })
})
