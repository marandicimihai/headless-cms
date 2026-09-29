"use server"

import { revalidatePath } from "next/cache"
import { invalidateWorkspaceCache } from "@/lib/api/cached-read"
import { redirect } from "next/navigation"

import {
  createProject,
  deleteProject,
  getProject,
  updateProject,
} from "@/lib/api/projects"
import { setCurrentWorkspace } from "@/lib/current-workspace"
import { getSession } from "@/lib/api/session"
import type { ApiError } from "@/lib/types/general"

export type ProjectActionState = {
  status: "idle" | "success" | "error"
  message: string | null
  fieldErrors: Record<string, string[]>
}

function errorState(error: ApiError): ProjectActionState {
  return {
    status: "error",
    message: error.detail,
    fieldErrors: error.fieldErrors,
  }
}

async function requireSession() {
  const session = await getSession()

  if (!session) redirect("/auth/login")
}

function refreshProjects(workspaceId: string, projectId?: string) {
  invalidateWorkspaceCache(workspaceId)
  revalidatePath("/", "layout")
  revalidatePath(`/workspaces/${workspaceId}`, "layout")
  revalidatePath(`/workspaces/${workspaceId}/projects`, "layout")

  if (projectId) {
    revalidatePath(`/workspaces/${workspaceId}/projects/${projectId}`, "layout")
  }
}

export async function createProjectAction(
  workspaceId: string,
  _previousState: ProjectActionState,
  formData: FormData,
): Promise<ProjectActionState> {
  await requireSession()

  const result = await createProject(workspaceId, {
    name: String(formData.get("name") ?? ""),
  })

  if (!result.ok) return errorState(result.error)

  await setCurrentWorkspace(workspaceId)
  refreshProjects(workspaceId, result.data.id)
  redirect(`/workspaces/${workspaceId}/projects/${result.data.id}`)
}

export async function renameProjectAction(
  workspaceId: string,
  projectId: string,
  _previousState: ProjectActionState,
  formData: FormData,
): Promise<ProjectActionState> {
  await requireSession()

  const result = await updateProject(workspaceId, projectId, {
    name: String(formData.get("name") ?? ""),
  })

  if (!result.ok) return errorState(result.error)

  refreshProjects(workspaceId, projectId)
  return {
    status: "success",
    message: "Project name updated.",
    fieldErrors: {},
  }
}

export async function deleteProjectAction(
  workspaceId: string,
  projectId: string,
  _previousState: ProjectActionState,
  formData: FormData,
): Promise<ProjectActionState> {
  await requireSession()

  const currentProject = await getProject(workspaceId, projectId, { fresh: true })

  if (!currentProject.ok) return errorState(currentProject.error)

  const confirmation = String(formData.get("confirmation") ?? "")

  if (confirmation !== currentProject.data.name) {
    return {
      status: "error",
      message: "Enter the current project name to confirm deletion.",
      fieldErrors: {
        confirmation: ["Enter the project name exactly as shown."],
      },
    }
  }

  const result = await deleteProject(workspaceId, projectId)

  if (!result.ok) return errorState(result.error)

  refreshProjects(workspaceId, projectId)
  redirect(`/workspaces/${workspaceId}/projects`)
}
