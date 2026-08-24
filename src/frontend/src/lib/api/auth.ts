import "server-only";
import { TokenResponse } from "../types/auth";
import { ApiResult } from "../types/general";
import { apiFetch } from "./fetch-utils";

export async function login(
  email: string,
  password: string
): Promise<ApiResult<TokenResponse>> {
  return await apiFetch("/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
    cache: "no-store"
  })
}
