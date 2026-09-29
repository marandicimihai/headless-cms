"use server"

import { revalidatePath } from "next/cache"
import { invalidateWorkspaceCache } from "@/lib/api/cached-read"
import { createContentTypesBatch, generateSchemaProposal } from "@/lib/api/schema-assistant"
import { getSession } from "@/lib/api/session"
import type {
  SchemaAssistantContentType,
  SchemaAssistantMessage,
  SchemaAssistantResponse,
  CreatedContentTypesResponse,
} from "@/lib/types/schema-assistant"
import { toContentTypeInputs } from "@/lib/types/schema-assistant"
import { redirect } from "next/navigation"

export type SchemaAssistantActionResult<T> =
  | { ok: true; data: T }
  | { ok: false; error: string }

async function requireSignedIn() {
  if (!await getSession()) redirect("/auth/login")
}

export async function generateSchemaProposalAction(
  workspaceId: string,
  projectId: string,
  messages: SchemaAssistantMessage[],
): Promise<SchemaAssistantActionResult<SchemaAssistantResponse>> {
  await requireSignedIn()
  if (!Array.isArray(messages) || messages.length === 0 || messages.length > 12) {
    return { ok: false, error: "Keep the chat to 12 messages or fewer." }
  }

  const boundedMessages = messages.map((message) => ({
    role: message.role,
    content: message.role === "assistant" && message.proposal?.length
      ? `${message.content}\nCurrent proposed schema: ${JSON.stringify(message.proposal)}`
      : message.content,
  }))
  const result = await generateSchemaProposal(workspaceId, projectId, boundedMessages)
  return result.ok
    ? { ok: true, data: result.data }
    : { ok: false, error: result.error.detail }
}

export async function createSchemaProposalAction(
  workspaceId: string,
  projectId: string,
  types: SchemaAssistantContentType[],
): Promise<SchemaAssistantActionResult<CreatedContentTypesResponse>> {
  await requireSignedIn()
  if (!Array.isArray(types) || types.length === 0 || types.length > 6) {
    return { ok: false, error: "The proposal must contain between one and six content types." }
  }

  const result = await createContentTypesBatch(
    workspaceId,
    projectId,
    toContentTypeInputs(types),
  )
  if (!result.ok) return { ok: false, error: result.error.detail }

  invalidateWorkspaceCache(workspaceId)
  const contentPath = `/workspaces/${workspaceId}/projects/${projectId}/content`
  revalidatePath(contentPath, "layout")
  revalidatePath(contentPath)
  return { ok: true, data: result.data }
}
