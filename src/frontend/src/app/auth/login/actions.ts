"use server";

import { login } from "@/lib/api/auth";
import { mirrorBackendSessionCookie } from "@/lib/api/session";
import { ApiError } from "@/lib/types/general";
import { redirect } from "next/navigation";

export type LoginState = {
  error: ApiError | null
}

export async function loginAction(
  _previousState: LoginState, 
  formData: FormData
): Promise<LoginState> {
  const loginResponse = await login(
      String(formData.get("email")),
      String(formData.get("password")),
    );

  if (!loginResponse.ok) {
    return {
      error: loginResponse.error,
    };
  }

  await mirrorBackendSessionCookie(
    loginResponse.data.setCookieHeader,
    loginResponse.data.session.absoluteExpiresAt,
  )
  redirect(invitationReturnPath(String(formData.get("returnTo") ?? "")) ?? "/")
}

function invitationReturnPath(value: string): string | null {
  if (!value.startsWith("/")) {
    return null
  }

  const target = new URL(value, "https://frontend.invalid")

  if (
    target.origin !== "https://frontend.invalid" ||
    target.pathname !== "/auth/invitations/accept" ||
    !target.searchParams.get("token")
  ) {
    return null
  }

  return `${target.pathname}?${target.searchParams.toString()}`
}
