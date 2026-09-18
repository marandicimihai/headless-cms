"use server"

import { redirect } from "next/navigation"
import { invalidateWorkspaceCache } from "@/lib/api/cached-read"

import {
  acceptInvitation,
  registerWithInvitation,
} from "@/lib/api/auth"
import { setCurrentWorkspace } from "@/lib/current-workspace"
import { getSession, mirrorBackendSessionCookie } from "@/lib/api/session"
import type { ApiError } from "@/lib/types/general"

type InvitationActionState = {
  error: ApiError | null
}

function tokenFrom(formData: FormData): string {
  return String(formData.get("token") ?? "")
}

export async function registerInvitationAction(
  _previousState: InvitationActionState,
  formData: FormData,
): Promise<InvitationActionState> {
  const result = await registerWithInvitation(
    tokenFrom(formData),
    String(formData.get("password") ?? ""),
  )

  if (!result.ok) {
    return { error: result.error }
  }

  await mirrorBackendSessionCookie(result.data.setCookieHeader)
  invalidateWorkspaceCache(result.data.registration.membership.workspaceId)
  await setCurrentWorkspace(result.data.registration.membership.workspaceId)
  redirect(`/workspaces/${result.data.registration.membership.workspaceId}`)
}

export async function acceptInvitationAction(
  _previousState: InvitationActionState,
  formData: FormData,
): Promise<InvitationActionState> {
  const token = tokenFrom(formData)
  const session = await getSession()

  if (!session) {
    redirect(`/auth/login?returnTo=${encodeURIComponent(invitationPath(token))}`)
  }

  const result = await acceptInvitation(token)

  if (!result.ok) {
    return { error: result.error }
  }

  invalidateWorkspaceCache(result.data.workspaceId)
  await setCurrentWorkspace(result.data.workspaceId)
  redirect(`/workspaces/${result.data.workspaceId}`)
}

function invitationPath(token: string): string {
  return `/auth/invitations/accept?token=${encodeURIComponent(token)}`
}
