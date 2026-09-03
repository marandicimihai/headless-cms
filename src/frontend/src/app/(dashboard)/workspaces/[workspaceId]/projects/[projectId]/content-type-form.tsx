"use client"

import Link from "next/link"
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react"
import { type FormEvent, useActionState, useState } from "react"

import {
  createContentTypeAction,
  updateContentTypeAction,
  type ContentActionState,
} from "./content-actions"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Field, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  showContentValidationToasts,
  useContentActionToast,
  validateContentType,
} from "@/lib/content-validation"
import type {
  ContentField,
  ContentFieldInput,
  ContentFieldType,
  ContentType,
} from "@/lib/types/content"

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
    settings: {},
  }
}

function toInput(field: ContentField): ContentFieldInput {
  const settings = { ...field.settings }
  if (field.required || settings.default === null) delete settings.default

  return {
    key: field.key,
    type: field.type,
    required: field.required,
    settings,
  }
}

const fieldTypeLabels: Record<ContentFieldType, string> = {
  text: "Text",
  number: "Number",
  boolean: "Boolean",
}

function hasDefault(field: ContentFieldInput) {
  return Object.prototype.hasOwnProperty.call(field.settings, "default")
}

function defaultInputValue(field: ContentFieldInput) {
  const value = field.settings.default
  if (field.type === "boolean") {
    return typeof value === "boolean"
      ? String(value)
      : typeof value === "string"
        ? value
        : ""
  }
  return typeof value === "string" || typeof value === "number"
    ? String(value)
    : ""
}

export function ContentTypeForm({
  workspaceId,
  projectId,
  mode = "create",
  initialContentType,
  onCancel,
}: {
  workspaceId: string
  projectId: string
  mode?: "create" | "edit"
  initialContentType?: ContentType
  onCancel?: () => void
}) {
  const [fields, setFields] = useState<ContentFieldInput[]>(() =>
    initialContentType?.fields.length
      ? initialContentType.fields.map(toInput)
      : [makeField(0)],
  )
  const [key, setKey] = useState(initialContentType?.key ?? "")
  const action =
    mode === "edit" && initialContentType
      ? updateContentTypeAction.bind(
          null,
          workspaceId,
          projectId,
          initialContentType.key,
        )
      : createContentTypeAction.bind(null, workspaceId, projectId)
  const [state, formAction, pending] = useActionState(action, initialState)
  useContentActionToast(state)
  const definition = JSON.stringify({ key, fields })
  const originalFields = initialContentType?.fields ?? []
  const originalByKey = new Map(originalFields.map((field) => [field.key, field]))
  const changedTypes = fields.some((field) => {
    const original = originalByKey.get(field.key)
    return original && original.type !== field.type
  })
  const actionLabel = mode === "edit" ? "Save changes" : "Create content type"

  function validateBeforeSubmit(event: FormEvent<HTMLFormElement>) {
    const issues = validateContentType(key, fields)
    if (issues.length === 0) return

    event.preventDefault()
    showContentValidationToasts(issues)
  }

  function updateField(index: number, update: Partial<ContentFieldInput>) {
    setFields((current) =>
      current.map((field, fieldIndex) => {
        if (fieldIndex !== index) return field

        const next = { ...field, ...update }
        const settings = { ...field.settings }
        if (update.type && update.type !== field.type && hasDefault(field)) {
          settings.default = defaultForType(update.type)
        }
        if (next.required) delete settings.default

        return { ...next, settings }
      }),
    )
  }

  function updateDefault(index: number, value: string) {
    setFields((current) =>
      current.map((field, fieldIndex) => {
        if (fieldIndex !== index) return field

        const settings = { ...field.settings }
        if (value.trim() === "") {
          delete settings.default
        } else if (field.type === "number") {
          settings.default = Number(value)
        } else if (field.type === "boolean") {
          settings.default = value === "true" ? true : value === "false" ? false : value
        } else {
          settings.default = value
        }

        return { ...field, settings }
      }),
    )
  }

  function moveField(index: number, direction: -1 | 1) {
    const nextIndex = index + direction
    if (nextIndex < 0 || nextIndex >= fields.length) return
    setFields((current) => {
      const next = [...current]
      const [moved] = next.splice(index, 1)
      next.splice(nextIndex, 0, moved)
      return next
    })
  }

  return (
    <form
      action={formAction}
      className="max-w-5xl space-y-8"
      noValidate
      onSubmit={validateBeforeSubmit}
    >
      <input type="hidden" name="definition" value={definition} />

      <Field>
        <FieldLabel htmlFor="content-type-key">Content type key</FieldLabel>
        {mode === "edit" ? (
          <output
            id="content-type-key"
            className="flex h-8 items-center rounded-xl border px-2.5 font-mono text-sm"
          >
            {key}
          </output>
        ) : (
          <Input
            id="content-type-key"
            value={key}
            onChange={(event) => setKey(event.target.value)}
            placeholder="articles"
            maxLength={64}
          />
        )}
      </Field>

      {mode === "edit" && changedTypes ? (
        <Alert variant="destructive">
          <AlertTitle>Existing field types cannot change</AlertTitle>
          <AlertDescription>
            The API rejects type changes for existing fields. Remove this field and
            add a replacement field with the new type instead.
          </AlertDescription>
        </Alert>
      ) : null}

      <section className="space-y-4" aria-labelledby="content-fields-heading">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h2 id="content-fields-heading" className="text-sm font-semibold">
              Fields
            </h2>
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
          {fields.map((field, index) => {
            const existingField = originalByKey.get(field.key)
            const typeChangeNotAllowed = Boolean(existingField)

            return (
              <div
                key={index}
                className="space-y-4 rounded-xl border p-4"
              >
                <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_9rem_auto] md:items-start">
                  <Field>
                    <FieldLabel htmlFor={`field-key-${index}`}>Key</FieldLabel>
                    {mode === "edit" && existingField ? (
                      <output
                        id={`field-key-${index}`}
                        className="flex h-8 items-center rounded-xl border px-2.5 font-mono text-sm"
                      >
                        {field.key}
                      </output>
                    ) : null}
                    {mode !== "edit" || !existingField ? (
                      <Input
                        id={`field-key-${index}`}
                        value={field.key}
                        onChange={(event) =>
                          updateField(index, { key: event.target.value })
                        }
                        maxLength={64}
                      />
                    ) : null}
                  </Field>

                  <Field>
                    <FieldLabel htmlFor={`field-type-${index}`}>Type</FieldLabel>
                    <Select
                      value={field.type}
                      onValueChange={(value) =>
                        isContentFieldType(value) &&
                        updateField(index, { type: value })
                      }
                      disabled={typeChangeNotAllowed}
                    >
                      <SelectTrigger id={`field-type-${index}`} className="w-full">
                        <SelectValue>
                          {(value) => fieldTypeLabels[value as ContentFieldType] ?? "Text"}
                        </SelectValue>
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
                    className="md:mt-7"
                    variant={field.required ? "default" : "outline"}
                    size="sm"
                    aria-pressed={field.required}
                    onClick={() =>
                      updateField(index, { required: !field.required })
                    }
                  >
                    Required
                  </Button>
                </div>

                {!field.required ? (
                  <Field>
                    <FieldLabel htmlFor={`field-default-${index}`}>
                      Default value
                    </FieldLabel>
                    <Input
                      id={`field-default-${index}`}
                      type={field.type === "number" ? "number" : "text"}
                      value={defaultInputValue(field)}
                      onChange={(event) => updateDefault(index, event.target.value)}
                      placeholder={field.type === "boolean" ? "true or false" : undefined}
                      step={field.type === "number" ? "any" : undefined}
                    />
                  </Field>
                ) : null}

                <div className="flex flex-wrap items-center justify-end gap-2 pt-1">
                  <div className="flex items-center gap-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      disabled={index === 0}
                      aria-label={`Move ${field.key || `field ${index + 1}`} up`}
                      onClick={() => moveField(index, -1)}
                    >
                      <ArrowUp />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      disabled={index === fields.length - 1}
                      aria-label={`Move ${field.key || `field ${index + 1}`} down`}
                      onClick={() => moveField(index, 1)}
                    >
                      <ArrowDown />
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
                </div>
              </div>
            )
          })}
        </div>
      </section>

      {state.message ? (
        <p className="text-sm text-destructive" role="alert">
          {state.message}
        </p>
      ) : null}

      <div className="flex items-center gap-2">
        <Button type="submit" disabled={pending}>
          {pending ? "Saving..." : actionLabel}
        </Button>
        {onCancel ? (
          <Button type="button" variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
        ) : (
          <Button
            nativeButton={false}
            variant="ghost"
            render={<Link href={`/workspaces/${workspaceId}/projects/${projectId}/content`} />}
          >
            Cancel
          </Button>
        )}
      </div>

    </form>
  )
}

function defaultForType(type: ContentFieldType) {
  if (type === "boolean") return false
  if (type === "number") return 0
  return ""
}

function isContentFieldType(value: unknown): value is ContentFieldType {
  return value === "text" || value === "number" || value === "boolean"
}
