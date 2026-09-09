import { beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))
vi.mock("./fetch-utils", () => ({ apiFetch: vi.fn() }))

import { searchWorkspace, workspaceSearchPath } from "./search"
import { apiFetch } from "./fetch-utils"

describe("workspace search API helper", () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset()
  })

  it("uses the workspace-scoped search contract", () => {
    expect(workspaceSearchPath("workspace/id", "two words")).toBe(
      "/api/workspaces/workspace%2Fid/search?query=two+words&limit=5",
    )

    void searchWorkspace("workspace-1", "article", 3)
    expect(apiFetch).toHaveBeenCalledWith(
      "/api/workspaces/workspace-1/search?query=article&limit=3",
      { cache: "no-store" },
    )
  })
})
