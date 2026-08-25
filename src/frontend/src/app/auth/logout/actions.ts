"use server"

import { logout } from "@/lib/api/auth"
import { deleteSession } from "@/lib/api/session"
import { redirect } from "next/navigation"

export async function logoutAction() {
  try {
    await logout()
  } catch {
    // Local cleanup must succeed even when the backend cannot revoke.
  } finally {
    await deleteSession()
  }
  redirect("/auth/login")
}
