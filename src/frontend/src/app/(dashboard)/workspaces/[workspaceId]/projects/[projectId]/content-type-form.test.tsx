/** @vitest-environment jsdom */

import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("./content-actions", () => ({
  createContentTypeAction: vi.fn(),
  updateContentTypeAction: vi.fn(),
}))

import { ContentTypeForm } from "./content-type-form"

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe("ContentTypeForm", () => {
  it("collects only keys for content types and fields", () => {
    render(<ContentTypeForm workspaceId="workspace-1" projectId="project-1" />)

    expect(screen.getByLabelText("Content type key")).toBeTruthy()
    expect(screen.getByLabelText("Key")).toBeTruthy()
    expect(screen.queryByLabelText("Display name")).toBeNull()
    expect(screen.queryByLabelText("Name")).toBeNull()
  })

  it("keeps the field key focused while editing", () => {
    render(<ContentTypeForm workspaceId="workspace-1" projectId="project-1" />)

    const keyInput = screen.getByLabelText("Key")
    keyInput.focus()
    fireEvent.change(keyInput, { target: { value: "body" } })

    expect(document.activeElement).toBe(keyInput)
  })

  it("uses an in-page confirmation for destructive schema changes", () => {
    const browserConfirm = vi.spyOn(window, "confirm")

    render(
      <ContentTypeForm
        workspaceId="workspace-1"
        projectId="project-1"
        mode="edit"
        initialContentType={{
          id: "type-1",
          projectId: "project-1",
          key: "articles",
          createdAt: "2026-09-02T00:00:00Z",
          updatedAt: "2026-09-02T00:00:00Z",
          fields: [
            {
              key: "title",
              type: "text",
              required: true,
              nullable: false,
              position: 0,
              settings: {},
            },
          ],
        }}
      />,
    )

    fireEvent.click(screen.getByRole("button", { name: "Add field" }))
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }))

    expect(browserConfirm).not.toHaveBeenCalled()
    expect(screen.getByRole("alertdialog")).toBeTruthy()
    expect(screen.getByText("Review schema changes?")).toBeTruthy()
  })

  it("initializes edit mode from the current schema and preserves defaults", () => {
    const { container } = render(
      <ContentTypeForm
        workspaceId="workspace-1"
        projectId="project-1"
        mode="edit"
        initialContentType={{
          id: "type-1",
          projectId: "project-1",
          key: "articles",
          createdAt: "2026-09-02T00:00:00Z",
          updatedAt: "2026-09-02T00:00:00Z",
          fields: [
            {
              key: "title",
              type: "text",
              required: true,
              nullable: false,
              position: 0,
              settings: { default: "Untitled" },
            },
            {
              key: "featured",
              type: "boolean",
              required: false,
              nullable: true,
              position: 1,
              settings: { default: null },
            },
          ],
        }}
      />,
    )

    expect(screen.getByText("articles", { selector: "output" }).tagName).toBe("OUTPUT")
    expect(screen.getByText("title", { selector: "output" }).tagName).toBe("OUTPUT")
    expect(screen.queryByLabelText("Default value")).toBeNull()
    expect(screen.getByLabelText("Move featured down").getAttribute("disabled")).not.toBeNull()

    const definition = container.querySelector('input[name="definition"]')
    expect(definition).not.toBeNull()
    expect(JSON.parse((definition as HTMLInputElement)?.value ?? "{}")).toMatchObject({
      key: "articles",
      fields: [
        { key: "title", settings: { default: "Untitled" } },
        { key: "featured", settings: { default: null } },
      ],
    })

    fireEvent.click(screen.getByLabelText("Move featured up"))
    expect(JSON.parse((definition as HTMLInputElement)?.value ?? "{}").fields[0].key).toBe(
      "featured",
    )
  })
})
