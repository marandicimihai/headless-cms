/** @vitest-environment jsdom */

import { act, cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

const push = vi.fn()

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}))

import { WorkspaceSearch } from "./workspace-search"

const results = {
  projects: {
    total: 1,
    items: [{ id: "project-1", name: "Article catalogue" }],
  },
  contentTypes: {
    total: 1,
    items: [
      {
        id: "content-type-1",
        key: "article",
        projectId: "project-1",
        projectName: "Article catalogue",
      },
    ],
  },
  entries: {
    total: 2,
    items: [
      {
        id: "entry-1",
        status: "published",
        contentTypeKey: "article",
        projectId: "project-1",
        projectName: "Article catalogue",
        matchedFieldKey: "title",
        snippet: "A matching article title",
      },
    ],
  },
}

describe("WorkspaceSearch", () => {
  beforeEach(() => {
    vi.useFakeTimers()
    push.mockReset()
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify(results), { status: 200 }),
      ),
    )
  })

  afterEach(() => {
    cleanup()
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it("waits for a two-character query, groups results, and opens the selected entry", async () => {
    render(<WorkspaceSearch workspaceId="workspace-1" />)
    const input = screen.getByRole("combobox", { name: "Search workspace content" })

    fireEvent.change(input, { target: { value: "a" } })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300)
    })
    expect(fetch).not.toHaveBeenCalled()

    fireEvent.change(input, { target: { value: "article" } })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300)
    })

    expect(fetch).toHaveBeenCalledWith(
      "/api/search?workspaceId=workspace-1&query=article&limit=5",
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    )
    expect(screen.getByRole("listbox", { name: "Search results" })).toBeTruthy()
    expect(screen.getByText("Projects")).toBeTruthy()
    expect(screen.getByText("Content types")).toBeTruthy()
    expect(screen.getByText("Entries")).toBeTruthy()
    expect(screen.getByText("Showing 1 of 2")).toBeTruthy()
    expect(screen.getByRole("option", { name: /entry-1/ })).toBeTruthy()

    fireEvent.keyDown(input, { key: "ArrowDown" })
    fireEvent.keyDown(input, { key: "ArrowDown" })
    fireEvent.keyDown(input, { key: "ArrowDown" })
    fireEvent.keyDown(input, { key: "Enter" })

    expect(push).toHaveBeenCalledWith(
      "/workspaces/workspace-1/projects/project-1/content-types/article?entry=entry-1",
    )
    expect((input as HTMLInputElement).value).toBe("")
  })

  it("shows a recoverable error and retries the current query", async () => {
    const fetchMock = vi.mocked(fetch)
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ detail: "The service is temporarily unavailable." }), {
        status: 503,
      }),
    )
    render(<WorkspaceSearch workspaceId="workspace-1" />)
    const input = screen.getByRole("combobox", { name: "Search workspace content" })

    fireEvent.change(input, { target: { value: "article" } })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300)
    })
    expect(screen.getByRole("alert").textContent).toContain("temporarily unavailable")

    fireEvent.click(screen.getByRole("button", { name: "Retry" }))
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300)
    })

    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(screen.getByText("A matching article title")).toBeTruthy()
  })
})
