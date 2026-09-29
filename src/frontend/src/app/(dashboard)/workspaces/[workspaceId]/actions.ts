"use server"

import { revalidatePath } from "next/cache"
import { invalidateWorkspaceCache } from "@/lib/api/cached-read"
import { redirect } from "next/navigation"

import { getSession } from "@/lib/api/session"
import { clearCurrentWorkspace } from "@/lib/current-workspace"
import {
  changeWorkspaceMemberRole,
  createWorkspaceInvitation,
  deleteWorkspace,
  leaveWorkspace,
  renameWorkspace,
  removeWorkspaceMember,
  resendWorkspaceInvitation,
  revokeWorkspaceInvitation,
  transferWorkspaceOwnership,
} from "@/lib/api/workspaces"
import type { ApiError } from "@/lib/types/general"

export type WorkspaceActionState = {
  invitationUrl?: string
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
  invalidateWorkspaceCache(workspaceId)
  revalidatePath("/", "page")
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

  invalidateWorkspaceCache(workspaceId)
  await clearCurrentWorkspace()
  revalidatePath("/", "page")
  redirect("/")
}

export async function leaveWorkspaceAction(
  workspaceId: string,
  _previousState: WorkspaceActionState,
): Promise<WorkspaceActionState> {
  void _previousState
  await requireSession()
  const result = await leaveWorkspace(workspaceId)

  if (!result.ok) return errorState(result.error)

  invalidateWorkspaceCache(workspaceId)
  await clearCurrentWorkspace()
  revalidatePath("/")
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
    message: `Invitation created for ${result.data.email}. Share the link with them.`,
    invitationUrl: result.data.invitationUrl,
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
    message: `New invitation link created for ${result.data.email}. The old link no longer works.`,
    invitationUrl: result.data.invitationUrl,
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

export async function removeWorkspaceMemberAction(
  workspaceId: string,
  userId: string,
  _previousState: WorkspaceActionState,
): Promise<WorkspaceActionState> {
  void _previousState
  await requireSession()
  const result = await removeWorkspaceMember(workspaceId, userId)

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: "Member removed from the workspace.",
    fieldErrors: {},
  }
}

export async function transferWorkspaceOwnershipAction(
  workspaceId: string,
  _previousState: WorkspaceActionState,
  formData: FormData,
): Promise<WorkspaceActionState> {
  await requireSession()
  const newOwnerUserId = String(formData.get("newOwnerUserId") ?? "")

  if (!newOwnerUserId) {
    return {
      status: "error",
      message: "Choose a member to become the owner.",
      fieldErrors: { newOwnerUserId: ["Choose a member to become the owner."] },
    }
  }

  const result = await transferWorkspaceOwnership(workspaceId, newOwnerUserId)

  if (!result.ok) return errorState(result.error)

  refreshWorkspace(workspaceId)
  return {
    status: "success",
    message: "Workspace ownership transferred. You are now an editor.",
    fieldErrors: {},
  }
}
