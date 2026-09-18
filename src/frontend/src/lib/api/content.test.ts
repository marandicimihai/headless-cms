import { beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))
vi.mock("./fetch-utils", () => ({
  apiFetch: vi.fn(),
}))

vi.mock("./cached-read", () => ({ cachedWorkspaceRead: vi.fn() }))
import { cachedWorkspaceRead } from "./cached-read"

import { listContentEntries, updateContentType } from "./content"
import { apiFetch } from "./fetch-utils"

const apiFetchMock = vi.mocked(apiFetch)

describe("content API helpers", () => {
  beforeEach(() => {
    apiFetchMock.mockReset()
    vi.mocked(cachedWorkspaceRead).mockReset()
  })

  it("updates fields through the workspace-scoped content-type contract", async () => {
    const input = {
      fields: [
        {
          key: "title",
          type: "text" as const,
          required: false,
          settings: { default: "Untitled" },
        },
      ],
    }

    await updateContentType("workspace-1", "project-1", "articles", input)

    expect(apiFetchMock).toHaveBeenCalledWith(
      "/api/workspaces/workspace-1/projects/project-1/content-types/articles",
      {
        method: "PUT",
        body: JSON.stringify(input),
        cache: "no-store",
      },
    )
  })

  it("serializes entry sorting and repeated filters for the list endpoint", async () => {
    await listContentEntries("workspace-1", "project-1", "articles", {
      sort: "-$updatedAt",
      filters: [
        { field: "title", operator: "contains", value: "first article" },
        { field: "$status", operator: "eq", value: "published" },
      ],
    })

    expect(cachedWorkspaceRead).toHaveBeenCalledWith(
      "workspace-1",
      "/api/workspaces/workspace-1/projects/project-1/content-types/articles/entries?page=1&pageSize=25&sort=-%24updatedAt&filter%5B%24status%5D%5Beq%5D=published&filter%5Btitle%5D%5Bcontains%5D=first+article",
    )
  })
})
