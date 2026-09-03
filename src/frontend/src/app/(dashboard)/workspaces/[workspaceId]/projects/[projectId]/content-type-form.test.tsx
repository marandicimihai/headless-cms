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
              position: 0,
              settings: {},
            },
            {
              key: "featured",
              type: "boolean",
              required: false,
              position: 1,
              settings: { default: false },
            },
          ],
        }}
      />,
    )

    expect(screen.getByText("articles", { selector: "output" }).tagName).toBe("OUTPUT")
    expect(screen.getByText("title", { selector: "output" }).tagName).toBe("OUTPUT")
    expect(screen.getByLabelText("Default value")).toBeTruthy()
    expect(screen.getByLabelText("Move featured down").getAttribute("disabled")).not.toBeNull()

    const definition = container.querySelector('input[name="definition"]')
    expect(definition).not.toBeNull()
    expect(JSON.parse((definition as HTMLInputElement)?.value ?? "{}")).toMatchObject({
      key: "articles",
      fields: [
        { key: "title", settings: {} },
        { key: "featured", settings: { default: false } },
      ],
    })

    fireEvent.click(screen.getByLabelText("Move featured up"))
    expect(JSON.parse((definition as HTMLInputElement)?.value ?? "{}").fields[0].key).toBe(
      "featured",
    )
  })

  it("removes a default when an optional field becomes required", () => {
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
              key: "summary",
              type: "text",
              required: false,
              position: 0,
              settings: { default: "Untitled" },
            },
          ],
        }}
      />,
    )

    fireEvent.click(screen.getByRole("button", { name: "Required" }))

    const definition = container.querySelector('input[name="definition"]')
    expect(JSON.parse((definition as HTMLInputElement).value).fields[0]).toMatchObject({
      required: true,
      settings: {},
    })
  })

  it("removes a default when its input is empty or whitespace", () => {
    const { container } = render(
      <ContentTypeForm workspaceId="workspace-1" projectId="project-1" />,
    )

    fireEvent.click(screen.getByRole("button", { name: "Add field" }))
    const defaultInput = screen.getByLabelText("Default value")
    fireEvent.change(defaultInput, { target: { value: "   " } })

    const definition = container.querySelector('input[name="definition"]')
    expect(JSON.parse((definition as HTMLInputElement).value).fields[1].settings).toEqual({})
  })
})
