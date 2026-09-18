import "server-only"

import { cache } from "react"

import type { ApiResult } from "@/lib/types/general"
import type { Project } from "@/lib/types/projects"

import { apiFetch } from "./fetch-utils"
import { cachedWorkspaceRead } from "./cached-read"

function projectsPath(workspaceId: string) {
  return `/api/workspaces/${encodeURIComponent(workspaceId.toLowerCase())}/projects`
}

function projectPath(workspaceId: string, projectId: string) {
  return `${projectsPath(workspaceId)}/${encodeURIComponent(projectId.toLowerCase())}`
}

const listProjectsForWorkspace = cache(async (
  workspaceId: string,
): Promise<ApiResult<Project[]>> => {
  return cachedWorkspaceRead<Project[]>(workspaceId, projectsPath(workspaceId))
})

export function listProjects(workspaceId: string): Promise<ApiResult<Project[]>> {
  return listProjectsForWorkspace(workspaceId.toLowerCase())
}

export async function getProject(
  workspaceId: string,
  projectId: string,
  options?: { fresh?: boolean },
): Promise<ApiResult<Project>> {
  return cachedWorkspaceRead<Project>(workspaceId, projectPath(workspaceId, projectId), options)
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
