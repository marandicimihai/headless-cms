import "server-only";
import type {
  AuthSession,
  InvitationMembership,
  InvitationPreview,
  InvitationRegistration,
} from "../types/auth";
import { ApiResult } from "../types/general";
import { apiFetch } from "./fetch-utils";
import { parseApiError } from "./problem-details";
import { getSessionCookie, SESSION_COOKIE_NAME } from "./session";

export type LoginResult = {
  session: AuthSession;
  setCookieHeader: string;
}

export type InvitationRegistrationResult = {
  registration: InvitationRegistration;
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

export async function previewInvitation(
  token: string,
): Promise<ApiResult<InvitationPreview>> {
  return anonymousPost<InvitationPreview>("/api/auth/invitations/preview", { token });
}

export async function registerWithInvitation(
  token: string,
  password: string,
): Promise<ApiResult<InvitationRegistrationResult>> {
  try {
    const response = await fetch(
      `${process.env.BACKEND_URL}/api/auth/invitations/register`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, password }),
        cache: "no-store",
        signal: AbortSignal.timeout(10_000),
      },
    );

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
        registration: await response.json() as InvitationRegistration,
        setCookieHeader,
      },
    };
  } catch {
    return unavailable();
  }
}

export async function acceptInvitation(
  token: string,
): Promise<ApiResult<InvitationMembership>> {
  return apiFetch<InvitationMembership>("/api/auth/invitations/accept", {
    method: "POST",
    body: JSON.stringify({ token }),
    cache: "no-store",
  });
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

async function anonymousPost<T>(
  endpoint: string,
  body: object,
): Promise<ApiResult<T>> {
  try {
    const response = await fetch(`${process.env.BACKEND_URL}${endpoint}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      cache: "no-store",
      signal: AbortSignal.timeout(10_000),
    });

    if (response.status >= 500) {
      return unavailable();
    }

    if (!response.ok) {
      return { ok: false, error: await parseApiError(response) };
    }

    return { ok: true, data: await response.json() as T };
  } catch {
    return unavailable();
  }
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
