/** @vitest-environment jsdom */

import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("../actions", () => ({
  deleteProjectAction: vi.fn(),
  renameProjectAction: vi.fn(),
}))

import { ProjectManagement } from "./project-management"

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe("ProjectManagement", () => {
  it("updates the controlled project name when server data changes", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {})
    const view = render(
      <ProjectManagement
        workspaceId="workspace-1"
        projectId="project-1"
        projectName="Original project"
      />,
    )

    view.rerender(
      <ProjectManagement
        workspaceId="workspace-1"
        projectId="project-1"
        projectName="Renamed project"
      />,
    )

    const nameInput = view.container.querySelector<HTMLInputElement>(
      'input[name="name"]',
    )

    expect(nameInput?.value).toBe("Renamed project")
    expect(
      consoleError.mock.calls.some((call) =>
        call.some((value) =>
          String(value).includes("changing the default value state"),
        ),
      ),
    ).toBe(false)
  })

  it("requires the exact project name before enabling deletion", () => {
    render(
      <ProjectManagement
        workspaceId="workspace-1"
        projectId="project-1"
        projectName="Original project"
      />,
    )

    fireEvent.click(screen.getByRole("button", { name: "Delete project" }))

    const confirmation = document.querySelector<HTMLInputElement>(
      "#delete-project-confirmation",
    )
    const deleteButton = screen
      .getAllByRole("button", { name: "Delete project" })
      .find((button) => button.getAttribute("form") === "delete-project-form")

    expect(confirmation).not.toBeNull()
    expect(deleteButton?.hasAttribute("disabled")).toBe(true)

    fireEvent.change(confirmation!, { target: { value: "Original project" } })

    expect(deleteButton?.hasAttribute("disabled")).toBe(false)
  })
})
