"use client"

import Link from "next/link"
import { useActionState } from "react"

import type { ContentActionState } from "../../content-actions"
import { Button } from "@/components/ui/button"
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
              <SelectValue />
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
  const required = field.required && !field.nullable
  const name = `field:${field.key}`
  const description = `${field.type}${field.required ? " · required" : ""}${field.nullable ? " · allows null" : ""}`

  if (field.type === "boolean") {
    const selected =
      typeof value === "boolean" ? String(value) : field.nullable ? "" : undefined

    return (
      <Field data-invalid={error.length > 0}>
        <FieldLabel htmlFor={id}>{field.key}</FieldLabel>
        <Select name={name} defaultValue={selected} required={required}>
          <SelectTrigger id={id} className="w-full">
            <SelectValue
              placeholder={field.nullable ? "No value" : "Choose a value"}
            />
          </SelectTrigger>
          <SelectContent align="start" alignItemWithTrigger={false}>
            {field.nullable ? (
              <SelectItem value="">No value</SelectItem>
            ) : null}
            <SelectItem value="true">True</SelectItem>
            <SelectItem value="false">False</SelectItem>
          </SelectContent>
        </Select>
        <p className="text-xs text-muted-foreground">{description}</p>
        <FieldError errors={error.map((message) => ({ message }))} />
      </Field>
    )
  }

  const inputValue =
    typeof value === "string" || typeof value === "number" ? String(value) : ""

  return (
    <Field data-invalid={error.length > 0}>
      <FieldLabel htmlFor={id}>{field.key}</FieldLabel>
      <Input
        id={id}
        name={name}
        type={field.type === "number" ? "number" : "text"}
        defaultValue={inputValue}
        required={required}
        step={field.type === "number" ? "any" : undefined}
      />
      <p className="text-xs text-muted-foreground">
        {description}
        {field.nullable ? "; leave empty to save null" : ""}
      </p>
      <FieldError errors={error.map((message) => ({ message }))} />
    </Field>
  )
}
