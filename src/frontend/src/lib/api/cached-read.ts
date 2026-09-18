import "server-only"

import { cache } from "react"
import { updateTag } from "next/cache"

import type { ApiResult } from "@/lib/types/general"
import type { WorkspaceRole } from "@/lib/types/workspaces"
import { apiFetch } from "./fetch-utils"
import { getSession } from "./session"
import { listMyWorkspaces } from "./workspaces"

export function workspaceCacheTag(workspaceId: string) {
  return `workspace:${workspaceId.toLowerCase()}`
}

export function invalidateWorkspaceCache(workspaceId: string) {
  updateTag(workspaceCacheTag(workspaceId))
}

const resolveAccess = cache(async (workspaceId: string): Promise<ApiResult<WorkspaceRole>> => {
  if (!await getSession()) {
    return { ok: false, error: { status: 401, detail: "Please sign in again.", fieldErrors: {} } }
  }
  const workspaces = await listMyWorkspaces()
  if (!workspaces.ok) return workspaces
  const role = workspaces.data.find((item) => item.id.toLowerCase() === workspaceId)?.currentRole
  if (!role) {
    return { ok: false, error: { status: 403, detail: "This workspace is not available to your account.", fieldErrors: {} } }
  }
  return { ok: true, data: role }
})

export async function cachedWorkspaceRead<T>(
  workspaceId: string,
  endpoint: string,
  options?: { fresh?: boolean; roleOf?: (data: T) => WorkspaceRole },
): Promise<ApiResult<T>> {
  const id = workspaceId.toLowerCase()
  const access = await resolveAccess(id)
  if (!access.ok) return access
  if (options?.fresh) return apiFetch<T>(endpoint, { cache: "no-store" })
  const result = await apiFetch<T>(endpoint, {
    cache: "force-cache",
    next: { revalidate: 30, tags: [workspaceCacheTag(id)] },
  })
  // Owner-only preview fields must not survive a membership role change.
  if (result.ok && options?.roleOf && options.roleOf(result.data) !== access.data) {
    return apiFetch<T>(endpoint, { cache: "no-store" })
  }
  return result
}
