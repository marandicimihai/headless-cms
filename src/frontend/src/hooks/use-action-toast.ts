"use client"

import { useEffect } from "react"

import { toast } from "sonner"

type ActionToastState = {
  status: "idle" | "success" | "error"
  message: string | null
  fieldErrors?: Record<string, string[]>
}

type ApiError = {
  detail: string
}

export function useActionToast(state: ActionToastState) {
  useEffect(() => {
    if (!state.message || state.status === "idle") return
    if (state.status === "error" && Object.keys(state.fieldErrors ?? {}).length > 0) {
      return
    }

    toast[state.status](state.message)
  }, [state])
}

export function useErrorToast(error: ApiError | null) {
  useEffect(() => {
    if (error) toast.error(error.detail)
  }, [error])
}
