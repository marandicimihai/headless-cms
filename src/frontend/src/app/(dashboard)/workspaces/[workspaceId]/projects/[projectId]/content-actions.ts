"use server"

import { revalidatePath } from "next/cache"
import { redirect } from "next/navigation"

import {
  createContentEntry,
  createContentType,
  deleteContentEntry,
  deleteContentType,
  updateContentEntry,
} from "@/lib/api/content"
import { getSession } from "@/lib/api/session"
import type { ApiError } from "@/lib/types/general"
import type {
  ContentField,
  ContentFieldInput,
  ContentFieldType,
  ContentEntryStatus,
  ContentTypeInput,
} from "@/lib/types/content"

export type ContentActionState = {
  status: "idle" | "success" | "error"
  message: string | null
  fieldErrors: Record<string, string[]>
}

const idleState = (): ContentActionState => ({
  status: "idle",
  message: null,
  fieldErrors: {},
})

function errorState(error: ApiError): ContentActionState {
  return { status: "error", message: error.detail, fieldErrors: error.fieldErrors }
}

async function requireSession() {
  if (!await getSession()) redirect("/auth/login")
}

function typeHref(workspaceId: string, projectId: string, contentTypeKey: string) {
  return `/workspaces/${workspaceId}/projects/${projectId}/content-types/${contentTypeKey}`
}

function refreshContent(workspaceId: string, projectId: string, contentTypeKey?: string) {
  const projectHref = `/workspaces/${workspaceId}/projects/${projectId}`
  revalidatePath(projectHref)
  if (contentTypeKey) revalidatePath(typeHref(workspaceId, projectId, contentTypeKey), "layout")
}

function isFieldType(value: unknown): value is ContentFieldType {
  return value === "text" || value === "number" || value === "boolean"
}

function parseDefinition(value: FormDataEntryValue | null): ContentTypeInput | null {
  if (typeof value !== "string") return null
  try {
    const parsed: unknown = JSON.parse(value)
    if (!parsed || typeof parsed !== "object") return null
    const definition = parsed as Partial<ContentTypeInput>
    if (typeof definition.key !== "string" || !Array.isArray(definition.fields)) return null
    const fields: ContentFieldInput[] = []
    for (const field of definition.fields) {
      if (!field || typeof field !== "object") return null
      const input = field as Partial<ContentFieldInput>
      if (typeof input.key !== "string" || !isFieldType(input.type) || typeof input.required !== "boolean" || typeof input.nullable !== "boolean") return null
      fields.push({ key: input.key, type: input.type, required: input.required, nullable: input.nullable, settings: {} })
    }
    return { key: definition.key, fields }
  } catch {
    return null
  }
}

function parseEntryData(fields: ContentField[], formData: FormData) {
  const data: Record<string, string | number | boolean | null> = {}
  for (const field of fields) {
    const value = String(formData.get(`field:${field.key}`) ?? "")
    if (value === "") {
      if (field.nullable) data[field.key] = null
      continue
    }
    if (field.type === "number") {
      const number = Number(value)
      if (!Number.isFinite(number)) throw new Error(`${field.key} must be a valid number.`)
      data[field.key] = number
    } else if (field.type === "boolean") {
      if (value !== "true" && value !== "false") throw new Error(`${field.key} must be true or false.`)
      data[field.key] = value === "true"
    } else {
      data[field.key] = value
    }
  }
  return data
}

function entryInput(fields: ContentField[], formData: FormData) {
  const status = String(formData.get("status") ?? "draft")
  if (status !== "draft" && status !== "published") throw new Error("Choose draft or published.")
  return { data: parseEntryData(fields, formData), status: status as ContentEntryStatus }
}

export async function createContentTypeAction(
  workspaceId: string,
  projectId: string,
  _previousState: ContentActionState,
  formData: FormData,
): Promise<ContentActionState> {
  await requireSession()
  const definition = parseDefinition(formData.get("definition"))
  if (!definition) return { status: "error", message: "The content type form contains invalid data.", fieldErrors: {} }
  const result = await createContentType(workspaceId, projectId, definition)
  if (!result.ok) return errorState(result.error)
  refreshContent(workspaceId, projectId, result.data.key)
  redirect(typeHref(workspaceId, projectId, result.data.key))
}

export async function deleteContentTypeAction(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  _previousState: ContentActionState,
  _formData: FormData,
): Promise<ContentActionState> {
  void _previousState
  void _formData
  await requireSession()
  const result = await deleteContentType(workspaceId, projectId, contentTypeKey)
  if (!result.ok) return errorState(result.error)
  refreshContent(workspaceId, projectId, contentTypeKey)
  redirect(`/workspaces/${workspaceId}/projects/${projectId}`)
}

export async function createContentEntryAction(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  fields: ContentField[],
  _previousState: ContentActionState,
  formData: FormData,
): Promise<ContentActionState> {
  await requireSession()
  try {
    const result = await createContentEntry(workspaceId, projectId, contentTypeKey, entryInput(fields, formData))
    if (!result.ok) return errorState(result.error)
  } catch (error) {
    return { status: "error", message: error instanceof Error ? error.message : "Unable to save entry.", fieldErrors: {} }
  }
  refreshContent(workspaceId, projectId, contentTypeKey)
  redirect(typeHref(workspaceId, projectId, contentTypeKey))
}

export async function updateContentEntryAction(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  entryId: string,
  fields: ContentField[],
  _previousState: ContentActionState,
  formData: FormData,
): Promise<ContentActionState> {
  await requireSession()
  try {
    const result = await updateContentEntry(workspaceId, projectId, contentTypeKey, entryId, entryInput(fields, formData))
    if (!result.ok) return errorState(result.error)
  } catch (error) {
    return { status: "error", message: error instanceof Error ? error.message : "Unable to save entry.", fieldErrors: {} }
  }
  refreshContent(workspaceId, projectId, contentTypeKey)
  redirect(typeHref(workspaceId, projectId, contentTypeKey))
}

export async function deleteContentEntryAction(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  entryId: string,
  _previousState: ContentActionState,
  _formData: FormData,
): Promise<ContentActionState> {
  void _previousState
  void _formData
  await requireSession()
  const result = await deleteContentEntry(workspaceId, projectId, contentTypeKey, entryId)
  if (!result.ok) return errorState(result.error)
  refreshContent(workspaceId, projectId, contentTypeKey)
  return { ...idleState(), status: "success", message: "Entry deleted." }
}
