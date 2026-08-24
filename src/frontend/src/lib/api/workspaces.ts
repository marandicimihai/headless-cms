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
  accessToken: string,
): Promise<ApiResult<WorkspaceSummary[]>> {
  return apiFetch<WorkspaceSummary[]>("/api/me/workspaces", {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
    cache: "no-store",
  })
}

export async function createWorkspace(
  accessToken: string,
  request: { name: string },
): Promise<ApiResult<CreateWorkspaceResponse>> {
  return apiFetch<CreateWorkspaceResponse>("/api/workspaces", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(request),
    cache: "no-store",
  })
}

export async function renameWorkspace(
  accessToken: string,
  workspaceId: string,
  request: { name: string },
): Promise<ApiResult<WorkspaceSummary>> {
  return apiFetch<WorkspaceSummary>(`/api/workspaces/${workspaceId}`, {
    method: "PATCH",
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify(request),
    cache: "no-store",
  })
}

export async function listWorkspaceMembers(
  accessToken: string,
  workspaceId: string,
): Promise<ApiResult<PagedWorkspaceResponse<WorkspaceMember>>> {
  return apiFetch<PagedWorkspaceResponse<WorkspaceMember>>(
    `/api/workspaces/${workspaceId}/members?page=1&pageSize=100`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
      cache: "no-store",
    },
  )
}

export async function listPendingWorkspaceInvitations(
  accessToken: string,
  workspaceId: string,
): Promise<ApiResult<PagedWorkspaceResponse<WorkspaceInvitation>>> {
  return apiFetch<PagedWorkspaceResponse<WorkspaceInvitation>>(
    `/api/workspaces/${workspaceId}/invitations?page=1&pageSize=100&status=Pending`,
    {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
      cache: "no-store",
    },
  )
}

export async function createWorkspaceInvitation(
  accessToken: string,
  workspaceId: string,
  request: { email: string; role: Exclude<WorkspaceRole, "Owner"> },
): Promise<ApiResult<WorkspaceInvitation>> {
  return apiFetch<WorkspaceInvitation>(
    `/api/workspaces/${workspaceId}/invitations`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify(request),
      cache: "no-store",
    },
  )
}

export async function changeWorkspaceMemberRole(
  accessToken: string,
  workspaceId: string,
  userId: string,
  role: Exclude<WorkspaceRole, "Owner">,
): Promise<ApiResult<WorkspaceMember>> {
  return apiFetch<WorkspaceMember>(
    `/api/workspaces/${workspaceId}/members/${encodeURIComponent(userId)}`,
    {
      method: "PATCH",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify({ role }),
      cache: "no-store",
    },
  )
}
