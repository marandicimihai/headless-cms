/** @vitest-environment jsdom */

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

const { generateSchemaProposalAction, createSchemaProposalAction } = vi.hoisted(() => ({
  generateSchemaProposalAction: vi.fn(),
  createSchemaProposalAction: vi.fn(),
}))

vi.mock("./schema-assistant-actions", () => ({
  generateSchemaProposalAction,
  createSchemaProposalAction,
}))

import { SchemaAssistantChat } from "./schema-assistant-chat"

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe("SchemaAssistantChat", () => {
  it("lets users review a generated draft and confirms creation", async () => {
    generateSchemaProposalAction.mockResolvedValue({
      ok: true,
      data: {
        reply: "I drafted the article schema.",
        contentTypes: [
          {
            key: "articles",
            fields: [
              { key: "title", type: "text", required: true, defaultValue: null },
            ],
          },
        ],
        existingKeyConflicts: [],
      },
    })
    createSchemaProposalAction.mockResolvedValue({
      ok: true,
      data: {
        contentTypes: [
          {
            id: "type-1",
            projectId: "project-1",
            key: "articles",
            createdAt: "2026-09-29T00:00:00Z",
            updatedAt: "2026-09-29T00:00:00Z",
            fields: [],
          },
        ],
      },
    })

    render(<SchemaAssistantChat workspaceId="workspace-1" projectId="project-1" />)

    fireEvent.change(screen.getByLabelText("Describe or refine your schema"), {
      target: { value: "Create articles with a required title." },
    })
    fireEvent.click(screen.getByRole("button", { name: "Send message" }))

    expect(await screen.findByText("Proposed content types")).toBeTruthy()
    expect(screen.getByText("articles", { selector: "h3" })).toBeTruthy()
    expect(createSchemaProposalAction).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole("button", { name: "Create 1 content type" }))
    fireEvent.click(await screen.findByRole("button", { name: "Confirm creation" }))

    await waitFor(() => expect(createSchemaProposalAction).toHaveBeenCalledWith(
      "workspace-1",
      "project-1",
      [{
        key: "articles",
        fields: [{ key: "title", type: "text", required: true, defaultValue: null }],
      }],
    ))
    expect(await screen.findByText("Content types created")).toBeTruthy()
  })
})
