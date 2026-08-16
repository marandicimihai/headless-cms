"use server";

import { login } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/problem-details";
import { cookies } from "next/headers";
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

  const tokens = loginResponse.data

  const cookieStore = await cookies();

  cookieStore.set("access_token", tokens.accessToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: new Date(tokens.accessExpiry),
  });

  cookieStore.set("refresh_token", tokens.refreshToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: new Date(tokens.refreshExpiry),
  })

  redirect("/")
}