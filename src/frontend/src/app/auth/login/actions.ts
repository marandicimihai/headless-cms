"use server";

import { login } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/problem-details";
import { createSession } from "@/lib/api/session";
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

  await createSession(loginResponse.data)
  redirect("/")
}