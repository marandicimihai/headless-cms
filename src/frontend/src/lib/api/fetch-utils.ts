import type { ApiResult } from "../types/general";
import { parseApiError } from "./problem-details";

export class ApiUnavailableError extends Error {}

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
  options?: RequestInit,
): Promise<ApiResult<T>> {
  try {
    const headers = new Headers(options?.headers)

    if (
      typeof options?.body === "string" &&
      !headers.has("Content-Type")
    ) {
      headers.set("Content-Type", "application/json")
    }

    const response = await fetch(`${process.env.BACKEND_URL}` + endpoint, {
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

    return {
      ok: true,
      data: await response.json() as T
    };
  } catch (error) {
    if (
      error instanceof ApiUnavailableError ||
      error instanceof TypeError || // fetch network failure
      (error instanceof DOMException && error.name === "TimeoutError")
    ) {
      return backendUnavailable<T>();
    }

    throw error;
  }
}
