"use server"

import { revalidatePath } from "next/cache"
import { redirect } from "next/navigation"

import { getSession } from "@/lib/api/session"
import {
  changeWorkspaceMemberRole,
  createWorkspaceInvitation,
  deleteWorkspace,
  renameWorkspace,
  resendWorkspaceInvitation,
  revokeWorkspaceInvitation,
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

}

function refreshWorkspace(workspaceId: string) {
  revalidatePath("/")
  revalidatePath(`/workspaces/${workspaceId}`, "layout")
}

export async function renameWorkspaceAction(
  workspaceId: string,
  _previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  await requireSession()
  const result = await renameWorkspace(workspaceId, {
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

export async function deleteWorkspaceAction(
  workspaceId: string,
  _previousState: WorkspaceActionState,
): Promise<WorkspaceActionState> {
  void _previousState
  await requireSession()
  const result = await deleteWorkspace(workspaceId)

  if (!result.ok) return errorState(result.error)

  revalidatePath("/", "layout")
  redirect("/")
}

export async function inviteWorkspaceMemberAction(
  workspaceId: string,
  _previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  await requireSession()
  const role = String(formData.get("role") ?? "")

  if (role !== "editor" && role !== "member") {
    return {
      status: "error",
      message: "Choose editor or read-only access.",
      fieldErrors: { role: ["Choose editor or read-only access."] },
    }
  }

  const result = await createWorkspaceInvitation(
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

export async function resendWorkspaceInvitationAction(
  workspaceId: string,
  invitationId: string,
  previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  void previousState
  void formData
  await requireSession()
  const result = await resendWorkspaceInvitation(workspaceId, invitationId)

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: `Invitation resent to ${result.data.email}.`,
    fieldErrors: {},
  }
}

export async function revokeWorkspaceInvitationAction(
  workspaceId: string,
  invitationId: string,
  previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  void previousState
  void formData
  await requireSession()
  const result = await revokeWorkspaceInvitation(workspaceId, invitationId)

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: "Invitation revoked.",
    fieldErrors: {},
  }
}

export async function changeWorkspaceMemberRoleAction(
  workspaceId: string,
  userId: string,
  _previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  await requireSession()
  const role = String(formData.get("role") ?? "")

  if (role !== "editor" && role !== "member") {
    return {
      status: "error",
      message: "Choose editor or read-only access.",
      fieldErrors: { role: ["Choose editor or read-only access."] },
    }
  }

  const result = await changeWorkspaceMemberRole(
    workspaceId,
    userId,
    role,
  )

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: `${result.data.email} is now ${role === "member" ? "read only" : "an editor"}.`,
    fieldErrors: {},
  }
}
