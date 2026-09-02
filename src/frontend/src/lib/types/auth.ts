export type AuthSession = {
  userId: string;
  email: string;
  platformRole: "User" | "PlatformAdmin";
  idleExpiresAt: string;
  absoluteExpiresAt: string;
}

export type InvitationPreview = {
  workspaceName: string;
  maskedEmail: string;
  role: "owner" | "editor" | "member";
  expiresAt: string;
}

export type InvitationMembership = {
  workspaceId: string;
  workspaceName: string;
  role: "owner" | "editor" | "member";
  joinedAt: string;
}

export type InvitationRegistration = {
  userId: string;
  email: string;
  membership: InvitationMembership;
}
