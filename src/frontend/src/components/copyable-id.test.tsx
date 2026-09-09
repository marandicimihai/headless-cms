/** @vitest-environment jsdom */

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

const { toast } = vi.hoisted(() => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}))

vi.mock("sonner", () => ({ toast }))

import { CopyableId } from "./copyable-id"

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe("CopyableId", () => {
  it("copies the ID and shows a success toast", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    })

    render(<CopyableId id="project-1" />)

    fireEvent.click(screen.getByRole("button", { name: "Copy ID project-1" }))

    await waitFor(() => expect(writeText).toHaveBeenCalledWith("project-1"))
    expect(toast.success).toHaveBeenCalledWith("ID copied to clipboard")
  })
})
