import "server-only"

import type { ApiResult } from "@/lib/types/general"
import type { Project } from "@/lib/types/projects"

import { apiFetch } from "./fetch-utils"

function projectsPath(workspaceId: string) {
  return `/api/workspaces/${encodeURIComponent(workspaceId)}/projects`
}

function projectPath(workspaceId: string, projectId: string) {
  return `${projectsPath(workspaceId)}/${encodeURIComponent(projectId)}`
}

export async function listProjects(
  workspaceId: string,
): Promise<ApiResult<Project[]>> {
  return apiFetch<Project[]>(projectsPath(workspaceId), { cache: "no-store" })
}

export async function getProject(
  workspaceId: string,
  projectId: string,
): Promise<ApiResult<Project>> {
  return apiFetch<Project>(projectPath(workspaceId, projectId), {
    cache: "no-store",
  })
}

export async function createProject(
  workspaceId: string,
  request: { name: string },
): Promise<ApiResult<Project>> {
  return apiFetch<Project>(projectsPath(workspaceId), {
    method: "POST",
    body: JSON.stringify(request),
    cache: "no-store",
  })
}

export async function updateProject(
  workspaceId: string,
  projectId: string,
  request: { name: string },
): Promise<ApiResult<Project>> {
  return apiFetch<Project>(projectPath(workspaceId, projectId), {
    method: "PUT",
    body: JSON.stringify(request),
    cache: "no-store",
  })
}

export async function deleteProject(
  workspaceId: string,
  projectId: string,
): Promise<ApiResult<void>> {
  return apiFetch<void>(projectPath(workspaceId, projectId), {
    method: "DELETE",
    cache: "no-store",
  })
}
