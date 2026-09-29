import "server-only"

import type { ContentTypeInput } from "../types/content"
import type {
  CreatedContentTypesResponse,
  SchemaAssistantMessage,
  SchemaAssistantResponse,
} from "../types/schema-assistant"
import { apiFetch } from "./fetch-utils"

function projectPath(workspaceId: string, projectId: string) {
  return `/api/workspaces/${workspaceId}/projects/${projectId}`
}

export function generateSchemaProposal(
  workspaceId: string,
  projectId: string,
  messages: Array<Pick<SchemaAssistantMessage, "role" | "content">>,
) {
  return apiFetch<SchemaAssistantResponse>(
    `${projectPath(workspaceId, projectId)}/schema-assistant/generate`,
    {
      method: "POST",
      body: JSON.stringify({ messages }),
      timeoutMs: 45_000,
      preserveBackendError: true,
    },
  )
}

export function createContentTypesBatch(
  workspaceId: string,
  projectId: string,
  contentTypes: ContentTypeInput[],
) {
  return apiFetch<CreatedContentTypesResponse>(
    `${projectPath(workspaceId, projectId)}/content-types/batch`,
    {
      method: "POST",
      body: JSON.stringify({ contentTypes }),
      timeoutMs: 20_000,
    },
  )
}
