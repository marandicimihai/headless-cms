import "server-only"

import type { ProjectPreview, WorkspacePreview } from "@/lib/types/previews"
import { cachedWorkspaceRead } from "./cached-read"

export function getWorkspacePreview(workspaceId: string) {
  return cachedWorkspaceRead<WorkspacePreview>(
    workspaceId,
    `/api/workspaces/${encodeURIComponent(workspaceId.toLowerCase())}/preview`,
    { roleOf: (data) => data.currentRole },
  )
}

export function getProjectPreview(workspaceId: string, projectId: string) {
  return cachedWorkspaceRead<ProjectPreview>(
    workspaceId,
    `/api/workspaces/${encodeURIComponent(workspaceId.toLowerCase())}/projects/${encodeURIComponent(projectId.toLowerCase())}/preview`,
    { roleOf: (data) => data.currentRole },
  )
}
