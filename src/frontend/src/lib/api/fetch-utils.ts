import "server-only";

import type { ApiResult } from "../types/general";
import { parseApiError } from "./problem-details";
import { getSessionCookie, SESSION_COOKIE_NAME } from "./session";

export class ApiUnavailableError extends Error {}

function isFetchUnavailableError(error: unknown) {
  return (
    error instanceof ApiUnavailableError ||
    error instanceof TypeError ||
    (error instanceof DOMException && error.name === "TimeoutError")
  )
}

function backendUnavailable<T>(status = 503): ApiResult<T> {
  return {
    ok: false,
    error: {
      status,
      code: "BACKEND_UNAVAILABLE",
      detail: "The service is temporarily unavailable.",
      fieldErrors: {}
    },
  };
}

export async function apiFetch<T>(
  endpoint: string,
  options?: RequestInit & { next?: { revalidate?: number; tags?: string[] } },
): Promise<ApiResult<T>> {
  try {
    const headers = new Headers(options?.headers)
    const sessionSecret = await getSessionCookie()

    if (sessionSecret) {
      headers.set("Cookie", `${SESSION_COOKIE_NAME}=${sessionSecret}`)
    }

    if (
      typeof options?.body === "string" &&
      !headers.has("Content-Type")
    ) {
      headers.set("Content-Type", "application/json")
    }

    const response = await fetch(`${process.env.BACKEND_URL}` + endpoint, {
      cache: "no-store",
      ...options,
      signal: AbortSignal.timeout(10_000),
      headers,
    })

    if (response.status >= 500) {
      return backendUnavailable<T>();
    }

    if (!response.ok) {
      return {
        ok: false,
        error: await parseApiError(response)
      };
    }

    if (response.status === 204) {
      return {
        ok: true,
        data: undefined as T,
      }
    }

    return {
      ok: true,
      data: await response.json() as T
    };
  } catch (error) {
    if (isFetchUnavailableError(error)) {
      return backendUnavailable<T>();
    }

    throw error;
  }
}
