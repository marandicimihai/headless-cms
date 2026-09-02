/** @vitest-environment jsdom */

import { cleanup, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("./content-actions", () => ({
  createContentTypeAction: vi.fn(),
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
})
