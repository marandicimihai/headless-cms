export type WorkspaceRole = "owner" | "editor" | "member"

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
  | "pending"
  | "accepted"
  | "expired"
  | "revoked"

export type WorkspaceInvitation = {
  invitationUrl?: string
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
