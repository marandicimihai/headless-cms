import {
  CURRENT_WORKSPACE_COOKIE_MAX_AGE,
  CURRENT_WORKSPACE_COOKIE_NAME,
} from "./current-workspace-cookie"

export function setCurrentWorkspaceCookie(workspaceId: string): void {
  if (typeof document === "undefined") return

  const attributes = [
    "Path=/",
    `Max-Age=${CURRENT_WORKSPACE_COOKIE_MAX_AGE}`,
    "SameSite=Lax",
  ]

  if (window.location.protocol === "https:") {
    attributes.push("Secure")
  }

  document.cookie = `${CURRENT_WORKSPACE_COOKIE_NAME}=${encodeURIComponent(workspaceId)}; ${attributes.join("; ")}`
}

export function clearCurrentWorkspaceCookie(): void {
  if (typeof document === "undefined") return

  document.cookie = `${CURRENT_WORKSPACE_COOKIE_NAME}=; Path=/; Max-Age=0; SameSite=Lax`
}
