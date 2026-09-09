import "server-only"

import type { ApiResult } from "@/lib/types/general"
import type { WorkspaceSearchResults } from "@/lib/types/search"

import { apiFetch } from "./fetch-utils"

export function workspaceSearchPath(
  workspaceId: string,
  query: string,
  limit = 5,
) {
  const params = new URLSearchParams({ query, limit: String(limit) })
  return `/api/workspaces/${encodeURIComponent(workspaceId)}/search?${params}`
}

export function searchWorkspace(
  workspaceId: string,
  query: string,
  limit = 5,
): Promise<ApiResult<WorkspaceSearchResults>> {
  return apiFetch<WorkspaceSearchResults>(workspaceSearchPath(workspaceId, query, limit), {
    cache: "no-store",
  })
}
