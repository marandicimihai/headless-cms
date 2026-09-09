/** @vitest-environment jsdom */

import { cleanup, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

const { pathname } = vi.hoisted(() => ({
  pathname:
    "/workspaces/workspace-1/projects/project-1/content-types/articles/entries/new",
}))

vi.mock("next/navigation", () => ({
  usePathname: () => pathname,
}))

import { SidebarProvider } from "@/components/ui/sidebar"

import { ContentSidebar } from "./content-sidebar"

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
  vi.restoreAllMocks()
})

describe("ContentSidebar", () => {
  it("keeps the selected content type active across entry routes", () => {
    render(
      <SidebarProvider>
        <ContentSidebar
          workspaceId="workspace-1"
          projectId="project-1"
          contentTypes={[
            {
              id: "type-1",
              projectId: "project-1",
              key: "articles",
              createdAt: "2026-09-03T00:00:00Z",
              updatedAt: "2026-09-03T00:00:00Z",
              fields: [],
            },
            {
              id: "type-2",
              projectId: "project-1",
              key: "pages",
              createdAt: "2026-09-03T00:00:00Z",
              updatedAt: "2026-09-03T00:00:00Z",
              fields: [],
            },
          ]}
          canWrite
        />
      </SidebarProvider>,
    )

    const articlesLink = screen.getByRole("link", { name: "articles" })
    const pagesLink = screen.getByRole("link", { name: "pages" })

    expect(articlesLink.hasAttribute("data-active")).toBe(true)
    expect(pagesLink.hasAttribute("data-active")).toBe(false)
    expect(
      screen
        .getByRole("link", { name: "Create content type" })
        .getAttribute("href"),
    ).toBe("/workspaces/workspace-1/projects/project-1/content-types/new")
  })

  it("does not expose content-type creation controls to read-only members", () => {
    render(
      <SidebarProvider>
        <ContentSidebar
          workspaceId="workspace-1"
          projectId="project-1"
          contentTypes={[]}
          canWrite={false}
        />
      </SidebarProvider>,
    )

    expect(screen.queryByRole("link", { name: "Create content type" })).toBeNull()
    expect(screen.getByText("No content types yet.")).toBeTruthy()
  })
})
