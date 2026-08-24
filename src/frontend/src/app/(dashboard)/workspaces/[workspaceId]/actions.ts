"use server"

import { revalidatePath } from "next/cache"
import { redirect } from "next/navigation"

import { getSession } from "@/lib/api/session"
import {
  changeWorkspaceMemberRole,
  createWorkspaceInvitation,
  renameWorkspace,
} from "@/lib/api/workspaces"
import type { ApiError } from "@/lib/types/general"

export type WorkspaceActionState = {
  status: "idle" | "success" | "error"
  message: string | null
  fieldErrors: Record<string, string[]>
}

function errorState(error: ApiError): WorkspaceActionState {
  return {
    status: "error",
    message: error.detail,
    fieldErrors: error.fieldErrors,
  }
}

async function requireSession() {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  return session
}

function refreshWorkspace(workspaceId: string) {
  revalidatePath("/workspaces")
  revalidatePath(`/workspaces/${workspaceId}`)
}

export async function renameWorkspaceAction(
  workspaceId: string,
  _previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  const session = await requireSession()
  const result = await renameWorkspace(session.accessToken, workspaceId, {
    name: String(formData.get("name") ?? ""),
  })

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: "Workspace name updated.",
    fieldErrors: {},
  }
}

export async function inviteWorkspaceMemberAction(
  workspaceId: string,
  _previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  const session = await requireSession()
  const role = String(formData.get("role") ?? "")

  if (role !== "Editor" && role !== "Member") {
    return {
      status: "error",
      message: "Choose editor or read-only access.",
      fieldErrors: { role: ["Choose editor or read-only access."] },
    }
  }

  const result = await createWorkspaceInvitation(
    session.accessToken,
    workspaceId,
    {
      email: String(formData.get("email") ?? ""),
      role,
    },
  )

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: `Invitation sent to ${result.data.email}.`,
    fieldErrors: {},
  }
}

export async function changeWorkspaceMemberRoleAction(
  workspaceId: string,
  userId: string,
  _previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  const session = await requireSession()
  const role = String(formData.get("role") ?? "")

  if (role !== "Editor" && role !== "Member") {
    return {
      status: "error",
      message: "Choose editor or read-only access.",
      fieldErrors: { role: ["Choose editor or read-only access."] },
    }
  }

  const result = await changeWorkspaceMemberRole(
    session.accessToken,
    workspaceId,
    userId,
    role,
  )

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: `${result.data.email} is now ${role === "Member" ? "read only" : "an editor"}.`,
    fieldErrors: {},
  }
}
