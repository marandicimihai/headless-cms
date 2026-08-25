import "server-only";

import type { AuthSession } from "../types/auth";
import { cookies } from "next/headers";

export const SESSION_COOKIE_NAME = "cms_session";

export async function getSessionCookie(): Promise<string | null> {
  const cookieStore = await cookies();
  return cookieStore.get(SESSION_COOKIE_NAME)?.value ?? null;
}

export async function mirrorBackendSessionCookie(
  setCookieHeader: string,
  absoluteExpiresAt: string,
): Promise<void> {
  const [cookiePair] = setCookieHeader.split(";", 1);
  const separator = cookiePair.indexOf("=");

  if (
    separator < 1 ||
    cookiePair.slice(0, separator).trim() !== SESSION_COOKIE_NAME
  ) {
    throw new Error("The backend did not return a valid session cookie");
  }

  const value = cookiePair.slice(separator + 1).trim();
  const expires = new Date(absoluteExpiresAt);

  if (!value || Number.isNaN(expires.getTime())) {
    throw new Error("The backend returned invalid session metadata");
  }

  const cookieStore = await cookies();
  cookieStore.set(SESSION_COOKIE_NAME, value, {
    httpOnly: true,
    secure: /(?:^|;)\s*secure(?:;|$)/i.test(setCookieHeader),
    sameSite: "lax",
    path: "/",
    expires,
  });
}

export async function getSession(): Promise<AuthSession | null> {
  const secret = await getSessionCookie();

  if (!secret) {
    return null;
  }

  try {
    const response = await fetch(
      `${process.env.BACKEND_URL}/api/auth/session`,
      {
        headers: { Cookie: `${SESSION_COOKIE_NAME}=${secret}` },
        cache: "no-store",
        signal: AbortSignal.timeout(10_000),
      },
    );

    if (!response.ok) {
      return null;
    }

    return await response.json() as AuthSession;
  } catch {
    return null;
  }
}

export async function deleteSession(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(SESSION_COOKIE_NAME);
}
