/** @vitest-environment jsdom */

import { cleanup, renderHook } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

const { toast } = vi.hoisted(() => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}))

vi.mock("sonner", () => ({ toast }))

import { useActionToast, useErrorToast } from "./use-action-toast"

type TestActionState = {
  status: "idle" | "success" | "error"
  message: string | null
}

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe("form toast hooks", () => {
  it("does not notify for an idle action", () => {
    renderHook(() =>
      useActionToast({ status: "idle", message: null }),
    )

    expect(toast.success).not.toHaveBeenCalled()
    expect(toast.error).not.toHaveBeenCalled()
  })

  it("notifies successful and failed actions", () => {
    const { rerender } = renderHook(
      ({ state }: { state: TestActionState }) => useActionToast(state),
      { initialProps: { state: { status: "success", message: "Saved." } } },
    )

    expect(toast.success).toHaveBeenCalledWith("Saved.")

    rerender({ state: { status: "error", message: "Unable to save." } })

    expect(toast.error).toHaveBeenCalledWith("Unable to save.")
  })

  it("notifies API failures without rendering a form-level alert", () => {
    renderHook(() => useErrorToast({ detail: "The service is unavailable." }))

    expect(toast.error).toHaveBeenCalledWith("The service is unavailable.")
  })
})
