/** @vitest-environment jsdom */

import { cleanup, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

let pathname = "/workspaces/workspace-1/projects/project-1"

vi.mock("next/navigation", () => ({
  usePathname: () => pathname,
}))

import { SidebarProvider } from "@/components/ui/sidebar"
import { WorkspaceSidebar } from "./workspace-sidebar"

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
})

describe("WorkspaceSidebar", () => {
  const props = {
    workspaceId: "workspace-1",
    projects: [{
      id: "project-1",
      workspaceId: "workspace-1",
      name: "Marketing website",
      createdAt: "2026-09-06T00:00:00Z",
      updatedAt: "2026-09-06T00:00:00Z",
    }],
  }

  it("shows Schema assistant in the main project navigation and marks it active", () => {
    pathname = "/workspaces/workspace-1/projects/project-1/schema-assistant"

    render(
      <SidebarProvider>
        <WorkspaceSidebar {...props} canWrite />
      </SidebarProvider>,
    )

    const link = screen.getByRole("link", { name: "Schema assistant" })
    expect(link.getAttribute("href")).toBe(
      "/workspaces/workspace-1/projects/project-1/schema-assistant",
    )
    expect(link.hasAttribute("data-active")).toBe(true)
  })

  it("hides Schema assistant from read-only members", () => {
    render(
      <SidebarProvider>
        <WorkspaceSidebar {...props} canWrite={false} />
      </SidebarProvider>,
    )

    expect(screen.queryByRole("link", { name: "Schema assistant" })).toBeNull()
  })

  it("shows project navigation when the freshly loaded project is supplied", () => {
    render(
      <SidebarProvider>
        <WorkspaceSidebar
          {...props}
          projects={[]}
          activeProject={props.projects[0]}
          canWrite
        />
      </SidebarProvider>,
    )

    expect(screen.getByRole("link", { name: "Overview" }).getAttribute("href")).toBe(
      "/workspaces/workspace-1/projects/project-1",
    )
  })
})
