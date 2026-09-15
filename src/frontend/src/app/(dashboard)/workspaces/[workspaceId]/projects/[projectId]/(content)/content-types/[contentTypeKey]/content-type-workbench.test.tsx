/** @vitest-environment jsdom */

import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

const push = vi.fn()
let query = "entry=entry-1"

vi.mock("next/navigation", () => ({
  usePathname: () => "/workspaces/workspace-1/projects/project-1/content-types/articles",
  useRouter: () => ({ push }),
  useSearchParams: () => new URLSearchParams(query),
}))

vi.mock("../../../content-actions", () => ({
  createContentEntryAction: vi.fn(),
  updateContentEntryAction: vi.fn(),
}))

vi.mock("../../../content-type-form", () => ({
  ContentTypeForm: () => <div>Schema editor</div>,
}))

vi.mock("./content-type-settings", () => ({
  ContentTypeSettings: () => <div>Content type settings</div>,
}))

vi.mock("./entry-editor", () => ({
  EntryEditor: ({ entry }: { entry?: { id: string } }) => (
    <div>{entry ? `Entry editor: ${entry.id}` : "New entry editor"}</div>
  ),
}))

vi.mock("./entry-actions", () => ({
  EntryActions: () => <button type="button">Entry actions</button>,
}))

import { ContentTypeWorkbench } from "./content-type-workbench"

afterEach(() => {
  cleanup()
  push.mockReset()
  query = "entry=entry-1"
  vi.restoreAllMocks()
})

describe("ContentTypeWorkbench", () => {
  it("switches sections and edits a clicked entry in a right-hand sheet", () => {
    render(
      <ContentTypeWorkbench
        workspaceId="workspace-1"
        projectId="project-1"
        canWrite
        entriesError={null}
        filters={[]}
        contentType={{
          id: "type-1",
          projectId: "project-1",
          key: "articles",
          createdAt: "2026-09-03T00:00:00Z",
          updatedAt: "2026-09-03T00:00:00Z",
          fields: [
            {
              key: "title",
              type: "text",
              required: true,
              position: 0,
              settings: {},
            },
          ],
        }}
        entries={[
          {
            id: "entry-1",
            status: "draft",
            data: { title: "First article" },
            createdAt: "2026-09-03T12:34:56Z",
            updatedAt: "2026-09-03T01:02:03Z",
          },
        ]}
      />,
    )

    expect(screen.getByText("First article")).toBeTruthy()
    expect(screen.getByText("#")).toBeTruthy()
    expect(screen.getByText("$id")).toBeTruthy()
    expect(screen.getByText("1")).toBeTruthy()
    expect(screen.getByText("entry-1")).toBeTruthy()
    expect(screen.getByText("$status")).toBeTruthy()
    expect(screen.getByText("$createdAt")).toBeTruthy()
    expect(screen.getByText("$updatedAt")).toBeTruthy()
    expect(screen.getByRole("columnheader", { name: "$updatedAt" }).getAttribute("aria-sort"))
      .toBe("descending")
    fireEvent.click(screen.getByRole("button", { name: /Sort by \$updatedAt, currently descending/ }))
    expect(push).toHaveBeenCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/articles?entry=entry-1&sort=%24updatedAt",
      { scroll: false },
    )
    expect(screen.getByRole("button", { name: "Copy ID type-1" })).toBeTruthy()
    expect(screen.getByRole("button", { name: "Copy ID entry-1" })).toBeTruthy()
    const entriesTable = screen.getByText("$id").closest("table")
    expect(entriesTable?.className).toContain("table-fixed")
    expect(entriesTable?.getAttribute("style")).toContain("width: 928px")
    expect(entriesTable?.querySelector("thead tr")?.className).toContain("h-11")
    expect(entriesTable?.querySelector("thead th")?.className).toContain("h-11")
    expect(entriesTable?.querySelector("col:nth-child(1)")?.className).toContain(
      "w-8",
    )
    expect(entriesTable?.querySelector("col:nth-child(2)")?.className).toContain(
      "w-52",
    )
    expect(entriesTable?.querySelector("col:nth-child(3)")?.className).toContain(
      "w-44",
    )
    expect(screen.getByText(/12:34/)).toBeTruthy()
    expect(screen.getByText(/1:02/)).toBeTruthy()
    expect(screen.queryByText("2026-09-03T12:34:56Z")).toBeNull()
    expect(screen.queryByText("2026-09-03T01:02:03Z")).toBeNull()
    const actionsCell = screen.getByRole("button", { name: "Entry actions" }).closest("td")
    expect(actionsCell?.className).toContain("sticky")
    expect(actionsCell?.className).toContain("right-0")
    expect(actionsCell?.className).toContain("w-8")
    expect(actionsCell?.className).toContain("min-w-8")
    expect(document.querySelector("th[aria-hidden=true]")?.className).toContain(
      "invisible",
    )
    fireEvent.click(screen.getByText("First article"))

    expect(screen.getByText("Edit articles entry")).toBeTruthy()
    expect(screen.getByText("Entry editor: entry-1")).toBeTruthy()
    expect(document.querySelector("[data-slot=sheet-content]")?.className).toContain(
      "sm:data-[side=right]:!w-2/5",
    )

    fireEvent.click(document.querySelector("[data-slot=sheet-overlay]") as Element)
    expect(screen.queryByText("Edit articles entry")).toBeNull()

    fireEvent.click(screen.getByRole("tab", { name: "Schema" }))
    const schemaTable = screen.getByText("Key").closest("table")
    expect(schemaTable?.querySelector("thead tr")?.className).toContain("h-11")
    expect(schemaTable?.querySelector("tbody tr")?.className).toContain("h-10")
    expect(schemaTable?.querySelector("tbody td")?.className).toContain("py-2")

    fireEvent.click(screen.getByRole("tab", { name: "Settings" }))
    expect(screen.getByText("Content type settings")).toBeTruthy()
  })

  it("cycles sort state and clears committed filters through URL navigation", () => {
    const props = {
      workspaceId: "workspace-1",
      projectId: "project-1",
      canWrite: false,
      entriesError: null,
      contentType: {
        id: "type-1",
        projectId: "project-1",
        key: "articles",
        createdAt: "2026-09-03T00:00:00Z",
        updatedAt: "2026-09-03T00:00:00Z",
        fields: [
          {
            key: "title",
            type: "text" as const,
            required: true,
            position: 0,
            settings: {},
          },
        ],
      },
      entries: [
        {
          id: "entry-1",
          status: "draft" as const,
          data: { title: "First article" },
          createdAt: "2026-09-03T12:34:56Z",
          updatedAt: "2026-09-03T01:02:03Z",
        },
      ],
      selectedEntry: undefined,
    }
    const { rerender } = render(
      <ContentTypeWorkbench {...props} filters={[]} sort="title" />,
    )

    expect(screen.getByRole("columnheader", { name: "$updatedAt" }).getAttribute("aria-sort"))
      .toBe("none")
    fireEvent.click(screen.getByRole("button", { name: /Sort by \$updatedAt$/ }))
    expect(push).toHaveBeenLastCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/articles?entry=entry-1&sort=%24updatedAt",
      { scroll: false },
    )

    rerender(<ContentTypeWorkbench {...props} filters={[]} sort="$updatedAt" />)
    fireEvent.click(screen.getByRole("button", { name: /Sort by \$updatedAt, currently ascending/ }))
    expect(push).toHaveBeenLastCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/articles?entry=entry-1&sort=-%24updatedAt",
      { scroll: false },
    )

    rerender(
      <ContentTypeWorkbench
        {...props}
        filters={[{ field: "title", operator: "contains", value: "first" }]}
      />,
    )
    expect(screen.getByRole("button", { name: "Filter entries" }).textContent).toContain("1")
    fireEvent.click(screen.getByRole("button", { name: "Filter entries" }))
    fireEvent.click(screen.getByRole("button", { name: "Clear all" }))
    expect(push).toHaveBeenLastCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/articles?entry=entry-1",
      { scroll: false },
    )

    rerender(<ContentTypeWorkbench {...props} filters={[]} />)
    fireEvent.click(screen.getByRole("button", { name: "Add filter" }))
    fireEvent.click(screen.getByRole("button", { name: "Done" }))
    expect(push).toHaveBeenLastCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/articles?entry=entry-1",
      { scroll: false },
    )

    rerender(
      <ContentTypeWorkbench
        {...props}
        entries={[]}
        filters={[{ field: "title", operator: "contains", value: "first" }]}
      />,
    )
    expect(screen.getByText("No matching entries")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }))
    expect(push).toHaveBeenLastCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/articles?entry=entry-1",
      { scroll: false },
    )
  })

  it("opens filters to the right and edits timestamp filters with a date-time input", () => {
    render(
      <ContentTypeWorkbench
        workspaceId="workspace-1"
        projectId="project-1"
        canWrite={false}
        entries={[]}
        entriesError={null}
        filters={[{ field: "$createdAt", operator: "gte", value: "2026-09-09T12:00:00Z" }]}
        contentType={{
          id: "type-1",
          projectId: "project-1",
          key: "articles",
          createdAt: "2026-09-03T00:00:00Z",
          updatedAt: "2026-09-03T00:00:00Z",
          fields: [],
        }}
      />,
    )

    fireEvent.click(screen.getByRole("button", { name: "Filter entries" }))

    expect(document.querySelector("[data-slot=popover-content]")?.getAttribute("data-align"))
      .toBe("start")
    const timestampInput = document.querySelector("input[type=datetime-local]") as HTMLInputElement
    expect(timestampInput.value).toBe("2026-09-09T12:00")
    expect(timestampInput.step).toBe("60")

    fireEvent.change(timestampInput, { target: { value: "2026-09-10T13:45" } })
    fireEvent.click(screen.getByRole("button", { name: "Done" }))

    expect(push).toHaveBeenLastCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/articles?entry=entry-1&filter%5B%24createdAt%5D%5Bgte%5D=2026-09-10T13%3A45%3A00.000Z",
      { scroll: false },
    )
  })
})
