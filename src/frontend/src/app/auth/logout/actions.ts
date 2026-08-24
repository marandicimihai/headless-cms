"use server"

import { deleteSession } from "@/lib/api/session"
import { redirect } from "next/navigation"

export async function logoutAction() {
  await deleteSession()
  redirect("/auth/login")
}