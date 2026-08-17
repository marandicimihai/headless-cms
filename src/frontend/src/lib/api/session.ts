import "server-only";

import { EncryptJWT, jwtDecrypt } from "jose";
import { TokenResponse } from "../types/auth";
import { cookies } from "next/headers";

const COOKIE_NAME = "cms_session";
const ISSUER = "headless-cms-frontend";
const AUDIENCE = "headless-cms-frontend";

function getEncryptionKey(): Uint8Array {
  const secret = process.env.SESSION_SECRET;

  if (!secret) {
    throw new Error("SESSION_SECRET is not configured");
  }

  const key = Buffer.from(secret, "base64");

  if (key.length !== 32) {
    throw new Error("SESSION_SECRET must contain exactly 32 random bytes");
  }

  return key;
}

export async function createSession(tokens: TokenResponse): Promise<void> {
  const refreshExpiry = new Date(tokens.refreshExpiry);

  const encrypted = await new EncryptJWT({...tokens})
    .setProtectedHeader({
      alg: "dir",
      enc: "A256GCM",
    })
    .setIssuer(ISSUER)
    .setAudience(AUDIENCE)
    .setIssuedAt()
    .setExpirationTime(refreshExpiry)
    .encrypt(getEncryptionKey());

  const c = await cookies();

  c.set(COOKIE_NAME, encrypted, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: refreshExpiry,
  });
}

export async function getSession(): Promise<TokenResponse | null> {
  const c = await cookies();
  const encrypted = c.get(COOKIE_NAME)?.value;

  if (!encrypted) {
    return null;
  }

  try {
    const { payload } = await jwtDecrypt<TokenResponse>(
      encrypted,
      getEncryptionKey(),
      {
        issuer: ISSUER,
        audience: AUDIENCE,
        keyManagementAlgorithms: ["dir"],
        contentEncryptionAlgorithms: ["A256GCM"],
      },
    );
    
    if (
      typeof payload.userId !== "string" ||
      typeof payload.accessToken !== "string" ||
      typeof payload.refreshToken !== "string" ||
      typeof payload.accessExpiry !== "string" ||
      typeof payload.refreshExpiry !== "string"
    ) {
      return null;
    }

    return payload;
  } catch {
    // Invalid, modified, expired, or encrypted with another key.
    return null;
  }
}

export async function deleteSession(): Promise<void> {
  const c = await cookies();
  c.delete(COOKIE_NAME);
}