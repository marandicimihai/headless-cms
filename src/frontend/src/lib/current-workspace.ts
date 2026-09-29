import "server-only"

import { cookies } from "next/headers"

import {
  CURRENT_WORKSPACE_COOKIE_MAX_AGE,
  CURRENT_WORKSPACE_COOKIE_NAME,
} from "./current-workspace-cookie"

export async function getCurrentWorkspaceId(): Promise<string | null> {
  const cookieStore = await cookies()

  return cookieStore.get(CURRENT_WORKSPACE_COOKIE_NAME)?.value ?? null
}

export async function setCurrentWorkspace(workspaceId: string): Promise<void> {
  const cookieStore = await cookies()

  cookieStore.set(CURRENT_WORKSPACE_COOKIE_NAME, workspaceId, {
    httpOnly: false,
    maxAge: CURRENT_WORKSPACE_COOKIE_MAX_AGE,
    path: "/",
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
  })
}

export async function clearCurrentWorkspace(): Promise<void> {
  const cookieStore = await cookies()

  // Match the path used when setting the preference so the root-scoped cookie
  // is removed during workspace deletion or leave actions.
  cookieStore.set(CURRENT_WORKSPACE_COOKIE_NAME, "", {
    httpOnly: false,
    maxAge: 0,
    path: "/",
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
  })
}
