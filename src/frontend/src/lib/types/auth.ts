export type AuthSession = {
  userId: string;
  email: string;
  platformRole: "User" | "PlatformAdmin";
  idleExpiresAt: string;
  absoluteExpiresAt: string;
}
