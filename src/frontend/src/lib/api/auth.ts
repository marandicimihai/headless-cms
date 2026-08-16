import "server-only";
import { TokenResponse } from "../types/auth";
import { ApiResult } from "../types/general";
import { parseApiError } from "./problem-details";

export async function login(
  email: string,
  password: string
): Promise<ApiResult<TokenResponse>> {
  const response = await fetch(`${process.env.BACKEND_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
    cache: "no-store"
  })

  if (!response.ok) {
    return {
      ok: false,
      error: await parseApiError(response)
    };
  }

  return {
    ok: true,
    data: await response.json() as TokenResponse,
  };
}
