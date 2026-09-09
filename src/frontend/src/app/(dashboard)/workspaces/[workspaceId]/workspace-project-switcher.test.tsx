/** @vitest-environment jsdom */

import { cleanup, fireEvent, render } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

let pathname = "/workspaces/workspace-1/projects/project-1"
const push = vi.fn()

vi.mock("next/navigation", () => ({
  usePathname: () => pathname,
  useRouter: () => ({ push }),
}))

import { SidebarProvider } from "@/components/ui/sidebar"
import { WorkspaceProjectSwitcher } from "./workspace-project-switcher"

Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: vi.fn().mockImplementation(() => ({
    addEventListener: vi.fn(),
    matches: false,
    removeEventListener: vi.fn(),
  })),
})

afterEach(() => {
  cleanup()
  pathname = "/workspaces/workspace-1/projects/project-1"
  push.mockReset()
})

describe("WorkspaceProjectSwitcher", () => {
  it("shows the active workspace and project, then opens the selected project overview", () => {
    const view = render(
      <SidebarProvider>
        <WorkspaceProjectSwitcher
          projects={[
            {
              id: "project-1",
              workspaceId: "workspace-1",
              name: "Marketing site",
              createdAt: "2026-09-06T00:00:00Z",
              updatedAt: "2026-09-06T00:00:00Z",
            },
          ]}
          workspace={{
            id: "workspace-1",
            name: "Acme",
            createdAt: "2026-09-06T00:00:00Z",
            currentRole: "owner",
          }}
          workspaces={[
            {
              id: "workspace-1",
              name: "Acme",
              createdAt: "2026-09-06T00:00:00Z",
              currentRole: "owner",
            },
          ]}
        />
      </SidebarProvider>,
    )

    expect(
      view.getByRole("button", { name: "Switch workspace or project" }).textContent,
    ).toContain("Acme / Marketing site")

    fireEvent.click(view.getByRole("button", { name: "Switch workspace or project" }))
    fireEvent.click(view.getByRole("button", { name: /Marketing site/i }))

    expect(push).toHaveBeenCalledWith(
      "/workspaces/workspace-1/projects/project-1",
    )
  })
})
