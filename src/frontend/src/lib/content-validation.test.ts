import { beforeEach, describe, expect, it, vi } from "vitest"

const { error } = vi.hoisted(() => ({ error: vi.fn() }))

vi.mock("sonner", () => ({
  toast: { error, success: vi.fn() },
}))

import {
  showContentValidationToasts,
  validateContentEntry,
  validateContentType,
} from "./content-validation"

describe("content validation", () => {
  beforeEach(() => error.mockClear())

  it("groups missing entry fields into one toast", () => {
    const issues = validateContentEntry(
      [
        { key: "title", type: "text", required: true, position: 0, settings: {} },
        { key: "published", type: "boolean", required: true, position: 1, settings: {} },
      ],
      new FormData(),
    )

    showContentValidationToasts(issues)

    expect(error).toHaveBeenCalledTimes(1)
    expect(error).toHaveBeenCalledWith("Required fields: title, published")
  })

  it("groups content type validation errors by category", () => {
    const issues = validateContentType("", [
      { key: "", type: "text", required: true, settings: {} },
      { key: "title", type: "boolean", required: false, settings: { default: "yes" } },
      { key: "title", type: "text", required: false, settings: {} },
    ])

    showContentValidationToasts(issues)

    expect(error).toHaveBeenCalledWith(
      "Required fields: content type key, field 1 key",
    )
    expect(error).toHaveBeenCalledWith("Invalid default values: title")
    expect(error).toHaveBeenCalledWith("Duplicate field keys: title")
  })
})
