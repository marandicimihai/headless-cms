import { beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("server-only", () => ({}))
vi.mock("./fetch-utils", () => ({
  apiFetch: vi.fn(),
}))

import { updateContentType } from "./content"
import { apiFetch } from "./fetch-utils"

const apiFetchMock = vi.mocked(apiFetch)

describe("content API helpers", () => {
  beforeEach(() => {
    apiFetchMock.mockReset()
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
})
