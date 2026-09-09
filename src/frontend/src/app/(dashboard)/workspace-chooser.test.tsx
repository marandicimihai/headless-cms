/** @vitest-environment jsdom */

import { cleanup, fireEvent, render } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

const push = vi.fn()

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}))

import { WorkspaceChooser } from "./workspace-chooser"
import { clearCurrentWorkspaceCookie } from "@/lib/current-workspace-client"

afterEach(() => {
  cleanup()
  clearCurrentWorkspaceCookie()
  push.mockReset()
})

describe("WorkspaceChooser", () => {
  it("remembers the selected workspace before entering it", () => {
    const view = render(
      <WorkspaceChooser
        workspaces={[
          {
            id: "workspace-1",
            name: "Acme content",
            createdAt: "2026-09-06T00:00:00Z",
            currentRole: "owner",
          },
        ]}
      />,
    )

    fireEvent.click(view.getByRole("button", { name: /Acme content/i }))

    expect(document.cookie).toContain("cms_current_workspace=workspace-1")
    expect(push).toHaveBeenCalledWith("/workspaces/workspace-1")
  })
})
