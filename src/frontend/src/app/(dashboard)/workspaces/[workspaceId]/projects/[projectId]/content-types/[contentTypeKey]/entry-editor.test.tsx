/** @vitest-environment jsdom */

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("@/hooks/use-action-toast", () => ({
  useActionToast: vi.fn(),
}))

import { EntryEditor } from "./entry-editor"

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe("EntryEditor", () => {
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
    expect(title.getAttribute("required")).not.toBeNull()
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
})
