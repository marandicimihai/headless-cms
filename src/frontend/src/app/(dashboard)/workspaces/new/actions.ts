"use server"

import { redirect } from "next/navigation"
import { invalidateWorkspaceCache } from "@/lib/api/cached-read"

import { getSession } from "@/lib/api/session"
import { setCurrentWorkspace } from "@/lib/current-workspace"
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

  const result = await createWorkspace({
    name: String(formData.get("name")),
  })

  if (!result.ok) {
    return {
      error: result.error,
    }
  }

  invalidateWorkspaceCache(result.data.id)
  await setCurrentWorkspace(result.data.id)
  redirect(`/workspaces/${result.data.id}`)
}
