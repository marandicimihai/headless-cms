import { useEffect } from "react"
import { toast } from "sonner"

import type { ContentField, ContentFieldInput } from "@/lib/types/content"

type ValidationIssue = {
  category: string
  subject: string
}

type ContentActionResult = {
  status: "idle" | "success" | "error"
  message: string | null
  fieldErrors: Record<string, string[]>
}

const fieldKeyPattern = /^[a-z][a-z0-9_]*$/

function unique(values: string[]) {
  return [...new Set(values)]
}

function displayFieldName(value: string) {
  return value.replace(/^data\./, "") || "field"
}

function showValidationIssues(issues: ValidationIssue[]) {
  const grouped = new Map<string, string[]>()

  for (const issue of issues) {
    grouped.set(issue.category, [
      ...(grouped.get(issue.category) ?? []),
      issue.subject,
    ])
  }

  for (const [category, subjects] of grouped) {
    toast.error(`${category}: ${unique(subjects).join(", ")}`)
  }
}

export function validateContentType(
  key: string,
  fields: ContentFieldInput[],
): ValidationIssue[] {
  const issues: ValidationIssue[] = []

  if (key === "") {
    issues.push({ category: "Required fields", subject: "content type key" })
  } else if (!fieldKeyPattern.test(key)) {
    issues.push({ category: "Invalid keys", subject: "content type key" })
  }

  const keys = new Map<string, number[]>()
  fields.forEach((field, index) => {
    const subject = field.key === "" ? `field ${index + 1} key` : field.key

    if (field.key === "") {
      issues.push({ category: "Required fields", subject })
    } else if (!fieldKeyPattern.test(field.key)) {
      issues.push({ category: "Invalid keys", subject })
    }

    if (field.key !== "") {
      keys.set(field.key, [...(keys.get(field.key) ?? []), index])
    }

    if (
      !field.required &&
      Object.prototype.hasOwnProperty.call(field.settings, "default")
    ) {
      const value = field.settings.default
      const valid =
        field.type === "text"
          ? typeof value === "string"
          : field.type === "number"
            ? typeof value === "number" && Number.isFinite(value)
            : typeof value === "boolean"

      if (!valid) {
        issues.push({ category: "Invalid default values", subject })
      }
    }
  })

  for (const [fieldKey, indexes] of keys) {
    if (indexes.length > 1) {
      issues.push({ category: "Duplicate field keys", subject: fieldKey })
    }
  }

  return issues
}

export function validateContentEntry(
  fields: ContentField[],
  formData: FormData,
): ValidationIssue[] {
  const issues: ValidationIssue[] = []

  for (const field of fields) {
    if (!field.required) continue

    const value = formData.get(`field:${field.key}`)
    if (value === null || String(value) === "") {
      issues.push({ category: "Required fields", subject: field.key })
    }
  }

  return issues
}

function toValidationIssue(
  field: string,
  message: string,
): ValidationIssue {
  const required = message.match(/^Field '(.+)' is required\.$/)
  if (required) {
    return { category: "Required fields", subject: required[1] }
  }

  const typed = message.match(/^Field '(.+)' must contain a (.+) value\.$/)
  if (typed) {
    return { category: `Invalid ${typed[2]} fields`, subject: typed[1] }
  }

  if (/not empty|required/i.test(message)) {
    return { category: "Required fields", subject: displayFieldName(field) }
  }

  if (/match|pattern|key/i.test(message)) {
    return { category: "Invalid keys", subject: displayFieldName(field) }
  }

  if (/duplicate/i.test(message)) {
    return { category: "Duplicate fields", subject: displayFieldName(field) }
  }

  if (/default|settings/i.test(message)) {
    return { category: "Invalid default values", subject: displayFieldName(field) }
  }

  return { category: "Validation errors", subject: message }
}

export function useContentActionToast(state: ContentActionResult) {
  useEffect(() => {
    if (!state.message || state.status === "idle") return

    if (state.status === "success") {
      toast.success(state.message)
      return
    }

    const issues = Object.entries(state.fieldErrors).flatMap(([field, messages]) =>
      messages.map((message) => toValidationIssue(field, message)),
    )

    if (issues.length > 0) {
      showValidationIssues(issues)
      return
    }

    toast.error(state.message)
  }, [state])
}

export function showContentValidationToasts(issues: ValidationIssue[]) {
  showValidationIssues(issues)
}
