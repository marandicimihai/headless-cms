"use server"

import { redirect } from "next/navigation"

import { getSession } from "@/lib/api/session"
import { createWorkspace } from "@/lib/api/workspaces"
import type { ApiError } from "@/lib/types/general"

export type CreateWorkspaceState = {
  error: ApiError | null
}

export async function createWorkspaceAction(
  _previousState: CreateWorkspaceState,
  formData: FormData,
): Promise<CreateWorkspaceState> {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  const result = await createWorkspace(session.accessToken, {
    name: String(formData.get("name")),
  })

  if (!result.ok) {
    return {
      error: result.error,
    }
  }

  redirect(`/workspaces/${result.data.id}`)
}
