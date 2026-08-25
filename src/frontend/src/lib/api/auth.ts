import "server-only";
import type { AuthSession } from "../types/auth";
import { ApiResult } from "../types/general";
import { parseApiError } from "./problem-details";
import { getSessionCookie, SESSION_COOKIE_NAME } from "./session";

export type LoginResult = {
  session: AuthSession;
  setCookieHeader: string;
}

export async function login(
  email: string,
  password: string
): Promise<ApiResult<LoginResult>> {
  try {
    const response = await fetch(`${process.env.BACKEND_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
      cache: "no-store",
      signal: AbortSignal.timeout(10_000),
    });

    if (response.status >= 500) {
      return unavailable();
    }

    if (!response.ok) {
      return { ok: false, error: await parseApiError(response) };
    }

    const setCookieHeader = response.headers.get("set-cookie");
    if (!setCookieHeader) {
      return unavailable();
    }

    return {
      ok: true,
      data: {
        session: await response.json() as AuthSession,
        setCookieHeader,
      },
    };
  } catch {
    return unavailable();
  }
}

export async function logout(): Promise<void> {
  const secret = await getSessionCookie();
  const headers = new Headers();

  if (secret) {
    headers.set("Cookie", `${SESSION_COOKIE_NAME}=${secret}`);
  }

  await fetch(`${process.env.BACKEND_URL}/api/auth/logout`, {
    method: "POST",
    headers,
    cache: "no-store",
    signal: AbortSignal.timeout(10_000),
  });
}

function unavailable<T>(): ApiResult<T> {
  return {
    ok: false,
    error: {
      status: 503,
      code: "BACKEND_UNAVAILABLE",
      detail: "The service is temporarily unavailable.",
      fieldErrors: {},
    },
  };
}
