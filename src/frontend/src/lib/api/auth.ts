import { TokenResponse } from "../types/auth";
import "server-only";

export async function login(
  email: string,
  password: string
): Promise<TokenResponse | null> {
  const response = await fetch(`${process.env.BACKEND_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
    cache: "no-store"
  })

  if (response.status === 401) {
    return null;
  }

  if (!response.ok) {
    throw new Error("Login failed");
  }

  return response.json() as Promise<TokenResponse>;
}
