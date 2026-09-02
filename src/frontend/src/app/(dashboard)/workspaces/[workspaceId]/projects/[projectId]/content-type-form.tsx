"use client"

import Link from "next/link"
import { Plus, Trash2 } from "lucide-react"
import { useActionState, useState } from "react"

import {
  createContentTypeAction,
  type ContentActionState,
} from "./content-actions"
import { Button } from "@/components/ui/button"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useActionToast } from "@/hooks/use-action-toast"
import type { ContentFieldInput, ContentFieldType } from "@/lib/types/content"

const initialState: ContentActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

function makeField(index: number): ContentFieldInput {
  return {
    key: index === 0 ? "title" : `field_${index + 1}`,
    type: "text",
    required: index === 0,
    nullable: false,
    settings: {},
  }
}

export function ContentTypeForm({
  workspaceId,
  projectId,
}: {
  workspaceId: string
  projectId: string
}) {
  const [fields, setFields] = useState<ContentFieldInput[]>([makeField(0)])
  const [key, setKey] = useState("")
  const [state, formAction, pending] = useActionState(
    createContentTypeAction.bind(null, workspaceId, projectId),
    initialState,
  )
  useActionToast(state)
  const definition = JSON.stringify({ key, fields })

  function updateField(index: number, update: Partial<ContentFieldInput>) {
    setFields((current) =>
      current.map((field, fieldIndex) =>
        fieldIndex === index ? { ...field, ...update } : field,
      ),
    )
  }

  return (
    <form action={formAction} className="max-w-4xl space-y-8">
      <input type="hidden" name="definition" value={definition} />

      <Field data-invalid={(state.fieldErrors.key ?? []).length > 0}>
        <FieldLabel htmlFor="content-type-key">Content type key</FieldLabel>
        <Input
          id="content-type-key"
          value={key}
          onChange={(event) => setKey(event.target.value)}
          placeholder="articles"
          pattern="[a-z][a-z0-9_]*"
          maxLength={64}
          required
        />
        <p className="text-xs text-muted-foreground">
          Lowercase letters, numbers, and underscores; this cannot be changed
          later.
        </p>
        <FieldError
          errors={(state.fieldErrors.key ?? []).map((message) => ({ message }))}
        />
      </Field>

      <section className="space-y-4" aria-labelledby="content-fields-heading">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h2 id="content-fields-heading" className="text-sm font-semibold">
              Fields
            </h2>
            <p className="text-sm text-muted-foreground">
              Each entry uses this schema.
            </p>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() =>
              setFields((current) => [...current, makeField(current.length)])
            }
          >
            <Plus />
            Add field
          </Button>
        </div>

        <div className="space-y-3">
          {fields.map((field, index) => (
            <div
              key={`${index}-${field.key}`}
              className="grid gap-3 border p-4 md:grid-cols-[minmax(0,1fr)_9rem_auto_auto_auto] md:items-end"
            >
              <Field>
                <FieldLabel htmlFor={`field-key-${index}`}>Key</FieldLabel>
                <Input
                  id={`field-key-${index}`}
                  value={field.key}
                  onChange={(event) =>
                    updateField(index, { key: event.target.value })
                  }
                  pattern="[a-z][a-z0-9_]*"
                  maxLength={64}
                  required
                />
              </Field>

              <Field>
                <FieldLabel>Type</FieldLabel>
                <Select
                  value={field.type}
                  onValueChange={(value) =>
                    isContentFieldType(value) && updateField(index, { type: value })
                  }
                >
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent align="start" alignItemWithTrigger={false}>
                    <SelectItem value="text">Text</SelectItem>
                    <SelectItem value="number">Number</SelectItem>
                    <SelectItem value="boolean">Boolean</SelectItem>
                  </SelectContent>
                </Select>
              </Field>

              <Button
                type="button"
                variant={field.required ? "default" : "outline"}
                size="sm"
                aria-pressed={field.required}
                onClick={() =>
                  updateField(index, { required: !field.required })
                }
              >
                Required
              </Button>
              <Button
                type="button"
                variant={field.nullable ? "default" : "outline"}
                size="sm"
                aria-pressed={field.nullable}
                onClick={() =>
                  updateField(index, { nullable: !field.nullable })
                }
              >
                Allows null
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                disabled={fields.length === 1}
                aria-label={`Remove ${field.key || `field ${index + 1}`}`}
                onClick={() =>
                  setFields((current) =>
                    current.filter((_, fieldIndex) => fieldIndex !== index),
                  )
                }
              >
                <Trash2 />
              </Button>
            </div>
          ))}
        </div>
      </section>

      {state.message ? (
        <p className="text-sm text-destructive" role="alert">
          {state.message}
        </p>
      ) : null}

      <div className="flex items-center gap-2">
        <Button type="submit" disabled={pending}>
          {pending ? "Creating..." : "Create content type"}
        </Button>
        <Button
          nativeButton={false}
          variant="ghost"
          render={<Link href={`/workspaces/${workspaceId}/projects/${projectId}`} />}
        >
          Cancel
        </Button>
      </div>
    </form>
  )
}

function isContentFieldType(value: unknown): value is ContentFieldType {
  return value === "text" || value === "number" || value === "boolean"
}
