import "server-only"

import type { ProjectPreview, WorkspacePreview } from "@/lib/types/previews"
import { apiFetch } from "./fetch-utils"

export function getWorkspacePreview(workspaceId: string) {
  return apiFetch<WorkspacePreview>(
    `/api/workspaces/${encodeURIComponent(workspaceId)}/preview`,
    { cache: "no-store" },
  )
}

export function getProjectPreview(workspaceId: string, projectId: string) {
  return apiFetch<ProjectPreview>(
    `/api/workspaces/${encodeURIComponent(workspaceId)}/projects/${encodeURIComponent(projectId)}/preview`,
    { cache: "no-store" },
  )
}
