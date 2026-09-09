/** @vitest-environment jsdom */

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

const { toastError } = vi.hoisted(() => ({ toastError: vi.fn() }))

vi.mock("sonner", () => ({
  toast: { error: toastError, success: vi.fn() },
}))

import { EntryEditor } from "./entry-editor"

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  toastError.mockClear()
})

describe("EntryEditor", () => {
  it("uses a compact bounded textarea for text fields", () => {
    render(
      <EntryEditor
        workspaceId="workspace-1"
        projectId="project-1"
        contentTypeKey="articles"
        fields={[
          { key: "body", type: "text", required: true, position: 0, settings: {} },
          { key: "rank", type: "number", required: true, position: 1, settings: {} },
        ]}
        action={async () => ({ status: "idle", message: null, fieldErrors: {} })}
      />,
    )

    const body = screen.getByLabelText(/body/) as HTMLTextAreaElement
    const rank = screen.getByLabelText(/rank/) as HTMLInputElement

    expect(body.tagName).toBe("TEXTAREA")
    expect(body.rows).toBe(1)
    expect(body.className).toContain("min-h-8")
    expect(body.className).toContain("max-h-40")
    expect(body.className).toContain("overflow-y-auto")
    expect(rank.tagName).toBe("INPUT")
    expect(rank.type).toBe("number")
  })

  it("marks required fields and lets optional text fields submit null", () => {
    const { container } = render(
      <EntryEditor
        workspaceId="workspace-1"
        projectId="project-1"
        contentTypeKey="articles"
        fields={[
          {
            key: "title",
            type: "text",
            required: true,
            position: 0,
            settings: {},
          },
          {
            key: "summary",
            type: "text",
            required: false,
            position: 1,
            settings: { default: "Untitled" },
          },
        ]}
        action={async () => ({ status: "idle", message: null, fieldErrors: {} })}
      />,
    )

    const title = screen.getByLabelText(/title/)
    const summary = screen.getByLabelText("summary")
    expect(screen.getByText("$status")).toBeTruthy()
    expect(container.querySelector("form")?.noValidate).toBe(true)
    expect(title.getAttribute("required")).toBeNull()
    expect(container.querySelector(".text-destructive")?.textContent?.trim()).toBe("*")

    fireEvent.click(screen.getByRole("checkbox", { name: "Null" }))
    expect(summary.getAttribute("disabled")).not.toBeNull()
  })

  it("includes the null marker in submitted form data", async () => {
    let submitted: FormData | undefined
    const action = vi.fn(async (_state: unknown, formData: FormData) => {
      submitted = formData
      return { status: "idle" as const, message: null, fieldErrors: {} }
    })

    const { container } = render(
      <EntryEditor
        workspaceId="workspace-1"
        projectId="project-1"
        contentTypeKey="articles"
        fields={[
          {
            key: "summary",
            type: "text",
            required: false,
            position: 0,
            settings: { default: "Untitled" },
          },
        ]}
        action={action}
      />,
    )

    fireEvent.click(screen.getByRole("checkbox", { name: "Null" }))
    fireEvent.submit(container.querySelector("form") as HTMLFormElement)

    await waitFor(() => expect(action).toHaveBeenCalled())
    expect(submitted?.get("null:summary")).toBe("on")
    expect(submitted?.get("field:summary")).toBeNull()
  })

  it("uses a grouped toast instead of native validation for empty required fields", () => {
    const action = vi.fn(async () => ({
      status: "idle" as const,
      message: null,
      fieldErrors: {},
    }))
    const { container } = render(
      <EntryEditor
        workspaceId="workspace-1"
        projectId="project-1"
        contentTypeKey="articles"
        fields={[
          { key: "title", type: "text", required: true, position: 0, settings: {} },
          {
            key: "published",
            type: "boolean",
            required: true,
            position: 1,
            settings: {},
          },
        ]}
        action={action}
      />,
    )

    fireEvent.submit(container.querySelector("form") as HTMLFormElement)

    expect(action).not.toHaveBeenCalled()
    expect(toastError).toHaveBeenCalledWith("Required fields: title, published")
  })
})
