import { beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))
vi.mock("./fetch-utils", () => ({
  apiFetch: vi.fn(),
}))

import {
  leaveWorkspace,
  removeWorkspaceMember,
  transferWorkspaceOwnership,
} from "./workspaces"
import { apiFetch } from "./fetch-utils"

const apiFetchMock = vi.mocked(apiFetch)

describe("workspace membership API helpers", () => {
  beforeEach(() => {
    apiFetchMock.mockReset()
  })

  it("uses the ownership, removal, and leaving contracts", async () => {
    await transferWorkspaceOwnership("workspace/id", "new owner")
    await removeWorkspaceMember("workspace/id", "member/id")
    await leaveWorkspace("workspace/id")

    expect(apiFetchMock.mock.calls).toEqual([
      [
        "/api/workspaces/workspace%2Fid/ownership-transfer",
        {
          method: "POST",
          body: JSON.stringify({ newOwnerUserId: "new owner" }),
          cache: "no-store",
        },
      ],
      [
        "/api/workspaces/workspace%2Fid/members/member%2Fid",
        { method: "DELETE", cache: "no-store" },
      ],
      [
        "/api/me/workspaces/workspace%2Fid",
        { method: "DELETE", cache: "no-store" },
      ],
    ])
  })
})
