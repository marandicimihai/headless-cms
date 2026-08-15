"use server";

import { login } from "@/lib/api/auth";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export type LoginState = {
  error: string | null
}

export async function loginAction(
  _previousState: LoginState, 
  formData: FormData
): Promise<LoginState> {
  let tokens;

  try {
    tokens = await login(
      String(formData.get("email")),
      String(formData.get("password")),
    );
  } catch {
    return {
      error: "The login service is temporarily unavailable.",
    };
  }

  if (!tokens) {
    return {
      error: "Invalid credentials",
    };
  }

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