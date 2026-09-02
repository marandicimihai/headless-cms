import "server-only"

import type { ApiResult } from "@/lib/types/general"
import type {
  CreateWorkspaceResponse,
  PagedWorkspaceResponse,
  WorkspaceInvitation,
  WorkspaceMember,
  WorkspaceRole,
  WorkspaceSummary,
} from "@/lib/types/workspaces"

import { apiFetch } from "./fetch-utils"

export async function listMyWorkspaces(
): Promise<ApiResult<WorkspaceSummary[]>> {
  return apiFetch<WorkspaceSummary[]>("/api/me/workspaces", {
    cache: "no-store",
  })
}

export async function createWorkspace(
  request: { name: string },
): Promise<ApiResult<CreateWorkspaceResponse>> {
  return apiFetch<CreateWorkspaceResponse>("/api/workspaces", {
    method: "POST",
    body: JSON.stringify(request),
    cache: "no-store",
  })
}

export async function renameWorkspace(
  workspaceId: string,
  request: { name: string },
): Promise<ApiResult<WorkspaceSummary>> {
  return apiFetch<WorkspaceSummary>(`/api/workspaces/${workspaceId}`, {
    method: "PATCH",
    body: JSON.stringify(request),
    cache: "no-store",
  })
}

export async function listWorkspaceMembers(
  workspaceId: string,
): Promise<ApiResult<PagedWorkspaceResponse<WorkspaceMember>>> {
  return apiFetch<PagedWorkspaceResponse<WorkspaceMember>>(
    `/api/workspaces/${workspaceId}/members?page=1&pageSize=100`,
    {
      cache: "no-store",
    },
  )
}

export async function listPendingWorkspaceInvitations(
  workspaceId: string,
): Promise<ApiResult<PagedWorkspaceResponse<WorkspaceInvitation>>> {
  return apiFetch<PagedWorkspaceResponse<WorkspaceInvitation>>(
    `/api/workspaces/${workspaceId}/invitations?page=1&pageSize=100&status=pending`,
    {
      cache: "no-store",
    },
  )
}

export async function createWorkspaceInvitation(
  workspaceId: string,
  request: { email: string; role: Exclude<WorkspaceRole, "owner"> },
): Promise<ApiResult<WorkspaceInvitation>> {
  return apiFetch<WorkspaceInvitation>(
    `/api/workspaces/${workspaceId}/invitations`,
    {
      method: "POST",
      body: JSON.stringify(request),
      cache: "no-store",
    },
  )
}

export async function resendWorkspaceInvitation(
  workspaceId: string,
  invitationId: string,
): Promise<ApiResult<WorkspaceInvitation>> {
  return apiFetch<WorkspaceInvitation>(
    `/api/workspaces/${workspaceId}/invitations/${encodeURIComponent(invitationId)}/resend`,
    {
      method: "POST",
      cache: "no-store",
    },
  )
}

export async function revokeWorkspaceInvitation(
  workspaceId: string,
  invitationId: string,
): Promise<ApiResult<void>> {
  return apiFetch<void>(
    `/api/workspaces/${workspaceId}/invitations/${encodeURIComponent(invitationId)}`,
    {
      method: "DELETE",
      cache: "no-store",
    },
  )
}

export async function changeWorkspaceMemberRole(
  workspaceId: string,
  userId: string,
  role: Exclude<WorkspaceRole, "owner">,
): Promise<ApiResult<WorkspaceMember>> {
  return apiFetch<WorkspaceMember>(
    `/api/workspaces/${workspaceId}/members/${encodeURIComponent(userId)}`,
    {
      method: "PATCH",
      body: JSON.stringify({ role }),
      cache: "no-store",
    },
  )
}
