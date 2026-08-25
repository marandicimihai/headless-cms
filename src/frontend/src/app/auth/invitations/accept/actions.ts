"use server"

import { redirect } from "next/navigation"

import {
  acceptInvitation,
  registerWithInvitation,
} from "@/lib/api/auth"
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

  redirect(`/workspaces/${result.data.workspaceId}`)
}

function invitationPath(token: string): string {
  return `/auth/invitations/accept?token=${encodeURIComponent(token)}`
}
