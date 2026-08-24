export type WorkspaceRole = "Owner" | "Editor" | "Member"

export type WorkspaceSummary = {
  id: string
  name: string
  createdAt: string
  currentRole: WorkspaceRole | null
}

export type CreateWorkspaceResponse = WorkspaceSummary

export type WorkspaceMember = {
  userId: string
  email: string
  role: WorkspaceRole
  joinedAt: string
}

export type WorkspaceInvitationStatus =
  | "Pending"
  | "Accepted"
  | "Expired"
  | "Revoked"

export type WorkspaceInvitation = {
  id: string
  workspaceId: string
  email: string
  role: WorkspaceRole
  status: WorkspaceInvitationStatus
  createdAt: string
  expiresAt: string
  lastSentAt: string | null
  acceptedAt: string | null
  revokedAt: string | null
}

export type PagedWorkspaceResponse<T> = {
  items: T[]
  page: number
  pageSize: number
  total: number
}
