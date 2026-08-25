import { beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))
vi.mock("./fetch-utils", () => ({
  apiFetch: vi.fn(),
}))

import {
  createProject,
  deleteProject,
  getProject,
  listProjects,
  updateProject,
} from "./projects"
import { apiFetch } from "./fetch-utils"

const apiFetchMock = vi.mocked(apiFetch)

describe("project API helpers", () => {
  beforeEach(() => {
    apiFetchMock.mockReset()
  })

  it("uses the workspace-scoped project contract", async () => {
    await listProjects("workspace-1")
    await getProject("workspace-1", "project-1")
    await createProject("workspace-1", { name: "Website" })
    await updateProject("workspace-1", "project-1", { name: "Updated website" })
    await deleteProject("workspace-1", "project-1")

    expect(apiFetchMock.mock.calls).toEqual([
      ["/api/workspaces/workspace-1/projects", { cache: "no-store" }],
      [
        "/api/workspaces/workspace-1/projects/project-1",
        { cache: "no-store" },
      ],
      [
        "/api/workspaces/workspace-1/projects",
        {
          method: "POST",
          body: JSON.stringify({ name: "Website" }),
          cache: "no-store",
        },
      ],
      [
        "/api/workspaces/workspace-1/projects/project-1",
        {
          method: "PUT",
          body: JSON.stringify({ name: "Updated website" }),
          cache: "no-store",
        },
      ],
      [
        "/api/workspaces/workspace-1/projects/project-1",
        { method: "DELETE", cache: "no-store" },
      ],
    ])
  })
})
