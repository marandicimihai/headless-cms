"use client"

import Link from "next/link"
import { useActionState, useState } from "react"

import type { ContentActionState } from "../../content-actions"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useActionToast } from "@/hooks/use-action-toast"
import type { ContentEntry, ContentField } from "@/lib/types/content"

const initialState: ContentActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

export function EntryEditor({
  workspaceId,
  projectId,
  contentTypeKey,
  fields,
  entry,
  action,
}: {
  workspaceId: string
  projectId: string
  contentTypeKey: string
  fields: ContentField[]
  entry?: ContentEntry
  action: (
    state: ContentActionState,
    formData: FormData,
  ) => Promise<ContentActionState>
}) {
  const [state, formAction, pending] = useActionState(action, initialState)
  useActionToast(state)
  const contentTypeHref =
    `/workspaces/${workspaceId}/projects/${projectId}/content-types/${contentTypeKey}`

  return (
    <form action={formAction} className="max-w-2xl">
      <FieldGroup>
        {fields.map((field) => (
          <EntryField
            key={field.key}
            field={field}
            value={entry?.data[field.key]}
            error={
              state.fieldErrors[`data.${field.key}`] ??
              state.fieldErrors[field.key] ??
              []
            }
          />
        ))}

        <Field>
          <FieldLabel>Status</FieldLabel>
          <Select name="status" defaultValue={entry?.status ?? "draft"} required>
          <SelectTrigger className="w-full">
            <SelectValue>
              {(value) => (value === "published" ? "Published" : "Draft")}
            </SelectValue>
            </SelectTrigger>
            <SelectContent align="start" alignItemWithTrigger={false}>
              <SelectItem value="draft">Draft</SelectItem>
              <SelectItem value="published">Published</SelectItem>
            </SelectContent>
          </Select>
        </Field>

        {state.message ? (
          <p className="text-sm text-destructive" role="alert">
            {state.message}
          </p>
        ) : null}

        <div className="flex items-center gap-2">
          <Button type="submit" disabled={pending}>
            {pending ? "Saving..." : entry ? "Save entry" : "Create entry"}
          </Button>
          <Button
            nativeButton={false}
            variant="ghost"
            render={<Link href={contentTypeHref} />}
          >
            Cancel
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

function EntryField({
  field,
  value,
  error,
}: {
  field: ContentField
  value: unknown
  error: string[]
}) {
  const id = `entry-field-${field.key}`
  const required = field.required
  const name = `field:${field.key}`
  const [isNull, setIsNull] = useState(value === null)

  if (field.type === "boolean") {
    const selected = typeof value === "boolean" ? String(value) : undefined

    return (
      <Field data-invalid={error.length > 0}>
        <EntryFieldLabel field={field} htmlFor={id} />
        <Select name={name} defaultValue={selected} required={required}>
          <SelectTrigger id={id} className="w-full">
            <SelectValue placeholder={field.required ? "Choose a value" : "Use default"}>
              {(value) =>
                value === "true" ? "True" : value === "false" ? "False" : "Use default"
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent align="start" alignItemWithTrigger={false}>
            <SelectItem value="true">True</SelectItem>
            <SelectItem value="false">False</SelectItem>
          </SelectContent>
        </Select>
        <FieldError errors={error.map((message) => ({ message }))} />
      </Field>
    )
  }

  const inputValue =
    typeof value === "string" || typeof value === "number" ? String(value) : ""

  return (
    <Field data-invalid={error.length > 0}>
      <EntryFieldLabel field={field} htmlFor={id} />
      <div className="relative">
        <Input
          id={id}
          name={name}
          type={field.type === "number" ? "number" : "text"}
          defaultValue={inputValue}
          disabled={field.type === "text" && !field.required && isNull}
          required={required}
          step={field.type === "number" ? "any" : undefined}
          className={field.type === "text" && !field.required ? "pr-16" : undefined}
        />
        {field.type === "text" && !field.required ? (
          <label
            htmlFor={`${id}-null`}
            className="absolute right-2 bottom-1 flex items-center gap-1 text-xs text-muted-foreground"
          >
            <Checkbox
              id={`${id}-null`}
              name={`null:${field.key}`}
              checked={isNull}
              onCheckedChange={setIsNull}
            />
            Null
          </label>
        ) : null}
      </div>
      <FieldError errors={error.map((message) => ({ message }))} />
    </Field>
  )
}

function EntryFieldLabel({
  field,
  htmlFor,
}: {
  field: ContentField
  htmlFor: string
}) {
  return (
    <FieldLabel htmlFor={htmlFor}>
      {field.key}
      {field.required ? (
        <>
          <span className="text-destructive" aria-hidden="true"> *</span>
          <span className="sr-only"> required</span>
        </>
      ) : null}
    </FieldLabel>
  )
}
