"use client"

import Link from "next/link"
import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react"
import { useActionState, useState, type FormEvent } from "react"

import {
  createContentTypeAction,
  updateContentTypeAction,
  type ContentActionState,
} from "./content-actions"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
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
    nullable: false,
    settings: {},
  }
}

function toInput(field: ContentField): ContentFieldInput {
  return {
    key: field.key,
    type: field.type,
    required: field.required,
    nullable: field.nullable,
    settings: { ...field.settings },
  }
}

type DefaultMode = "none" | "value" | "null"

const defaultModeLabels: Record<DefaultMode, string> = {
  none: "No default",
  value: "Use a value",
  null: "Default to null",
}

const fieldTypeLabels: Record<ContentFieldType, string> = {
  text: "Text",
  number: "Number",
  boolean: "Boolean",
}

function hasDefault(field: ContentFieldInput) {
  return Object.prototype.hasOwnProperty.call(field.settings, "default")
}

function defaultMode(field: ContentFieldInput): DefaultMode {
  if (!hasDefault(field)) return "none"
  return field.settings.default === null ? "null" : "value"
}

function defaultInputValue(field: ContentFieldInput) {
  const value = field.settings.default
  if (field.type === "boolean") {
    return typeof value === "boolean" ? String(value) : "false"
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
}: {
  workspaceId: string
  projectId: string
  mode?: "create" | "edit"
  initialContentType?: ContentType
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
  useActionToast(state)
  const definition = JSON.stringify({ key, fields })
  const originalFields = initialContentType?.fields ?? []
  const originalByKey = new Map(originalFields.map((field) => [field.key, field]))
  const removedKeys = originalFields
    .map((field) => field.key)
    .filter((fieldKey) => !fields.some((field) => field.key === fieldKey))
  const addedFields = fields.filter(
    (field) => !originalByKey.has(field.key),
  )
  const changedKeys = removedKeys.length > 0 || addedFields.length > 0
  const changedTypes = fields.some((field) => {
    const original = originalByKey.get(field.key)
    return original && original.type !== field.type
  })
  const tightenedRequiredFields = fields.some((field) => {
    const original = originalByKey.get(field.key)
    return (
      field.required &&
      !field.nullable &&
      !hasDefault(field) &&
      (!original || !original.required || original.nullable)
    )
  })
  const hasDestructiveChanges = changedKeys || tightenedRequiredFields
  const actionLabel = mode === "edit" ? "Save changes" : "Create content type"

  function updateField(index: number, update: Partial<ContentFieldInput>) {
    setFields((current) =>
      current.map((field, fieldIndex) =>
        fieldIndex === index
          ? {
              ...field,
              ...update,
              settings:
                update.type && update.type !== field.type && hasDefault(field)
                  ? { ...field.settings, default: defaultForType(update.type) }
                  : update.nullable === false && field.settings.default === null
                    ? (() => {
                        const settings = { ...field.settings }
                        delete settings.default
                        return settings
                      })()
                    : field.settings,
            }
          : field,
      ),
    )
  }

  function updateDefault(index: number, update: unknown) {
    setFields((current) =>
      current.map((field, fieldIndex) => {
        if (fieldIndex !== index) return field
        return { ...field, settings: { ...field.settings, default: update } }
      }),
    )
  }

  function setDefaultMode(index: number, value: string) {
    if (value === "none") {
      setFields((current) =>
        current.map((field, fieldIndex) => {
          if (fieldIndex !== index) return field
          const settings = { ...field.settings }
          delete settings.default
          return { ...field, settings }
        }),
      )
      return
    }

    setFields((current) =>
      current.map((field, fieldIndex) => {
        if (fieldIndex !== index) return field
        const nextDefault = value === "null" ? null : defaultForType(field.type)
        return { ...field, settings: { ...field.settings, default: nextDefault } }
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

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    if (!hasDestructiveChanges) return
    const confirmed = window.confirm(
      "These schema changes may remove or invalidate data in existing entries. Continue?",
    )
    if (!confirmed) event.preventDefault()
  }

  return (
    <form action={formAction} onSubmit={handleSubmit} className="max-w-5xl space-y-8">
      <input type="hidden" name="definition" value={definition} />

      <Field data-invalid={(state.fieldErrors.key ?? []).length > 0}>
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
            pattern="[a-z][a-z0-9_]*"
            maxLength={64}
            required
          />
        )}
        <FieldError
          errors={(state.fieldErrors.key ?? []).map((message) => ({ message }))}
        />
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

      {mode === "edit" && hasDestructiveChanges ? (
        <Alert>
          <AlertTitle>Review schema changes</AlertTitle>
          <AlertDescription>
            Removing a field can remove values from existing entries. Adding or
            tightening a required field needs a valid default or the backend may
            reject the migration.
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
                key={`${index}-${field.key}`}
                className="space-y-4 rounded-xl border p-4"
              >
                <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_9rem_auto_auto] md:items-end">
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
                        pattern="[a-z][a-z0-9_]*"
                        maxLength={64}
                        required
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
                </div>

                {!field.required ? (
                  <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
                    <Field>
                      <FieldLabel htmlFor={`field-default-mode-${index}`}>
                        Default
                      </FieldLabel>
                      <Select
                        value={defaultMode(field)}
                        onValueChange={(value) =>
                          value !== null && setDefaultMode(index, value)
                        }
                      >
                        <SelectTrigger id={`field-default-mode-${index}`} className="w-full">
                          <SelectValue>
                            {(value) =>
                              defaultModeLabels[value as DefaultMode] ?? "No default"
                            }
                          </SelectValue>
                        </SelectTrigger>
                        <SelectContent align="start" alignItemWithTrigger={false}>
                          <SelectItem value="none">No default</SelectItem>
                          <SelectItem value="value">Use a value</SelectItem>
                          {field.nullable ? (
                            <SelectItem value="null">Default to null</SelectItem>
                          ) : null}
                        </SelectContent>
                      </Select>
                    </Field>

                    {defaultMode(field) === "value" ? (
                      <Field>
                        <FieldLabel htmlFor={`field-default-${index}`}>
                          Default value
                        </FieldLabel>
                        {field.type === "boolean" ? (
                          <Select
                            value={defaultInputValue(field)}
                            onValueChange={(value) => updateDefault(index, value === "true")}
                          >
                            <SelectTrigger id={`field-default-${index}`} className="w-full">
                              <SelectValue>
                                {(value) => (value === "true" ? "True" : "False")}
                              </SelectValue>
                            </SelectTrigger>
                            <SelectContent align="start" alignItemWithTrigger={false}>
                              <SelectItem value="true">True</SelectItem>
                              <SelectItem value="false">False</SelectItem>
                            </SelectContent>
                          </Select>
                        ) : (
                          <Input
                            id={`field-default-${index}`}
                            type={field.type === "number" ? "number" : "text"}
                            value={defaultInputValue(field)}
                            onChange={(event) =>
                              updateDefault(
                                index,
                                field.type === "number"
                                  ? Number(event.target.value)
                                  : event.target.value,
                              )
                            }
                            step={field.type === "number" ? "any" : undefined}
                            required
                          />
                        )}
                      </Field>
                    ) : (
                      <div aria-hidden="true" />
                    )}
                  </div>
                ) : null}

                <div className="flex flex-wrap items-center justify-end gap-2 border-t pt-3">
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
        <Button
          nativeButton={false}
          variant="ghost"
          render={<Link href={`/workspaces/${workspaceId}/projects/${projectId}/content`} />}
        >
          Cancel
        </Button>
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
