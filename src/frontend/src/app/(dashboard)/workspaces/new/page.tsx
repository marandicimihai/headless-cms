"use client"

import Link from "next/link"
import { useActionState } from "react"

import {
  createWorkspaceAction,
  type CreateWorkspaceState,
} from "./actions"
import { Button } from "@/components/ui/button"
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { useErrorToast } from "@/hooks/use-action-toast"

const initialState: CreateWorkspaceState = {
  error: null,
}

export default function NewWorkspacePage() {
  const [state, formAction, pending] = useActionState(
    createWorkspaceAction,
    initialState,
  )

  const nameErrors = state.error?.fieldErrors.name ?? []
  const hasFieldErrors = Object.keys(state.error?.fieldErrors ?? {}).length > 0
  const formError = state.error && !hasFieldErrors ? state.error : null
  useErrorToast(formError)

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Create workspace
        </h1>
      </div>

      <form action={formAction} className="max-w-xl" noValidate>
        <FieldGroup>
          <Field data-invalid={nameErrors.length > 0}>
            <FieldLabel htmlFor="name">Workspace name</FieldLabel>
            <Input
              id="name"
              name="name"
              maxLength={100}
              placeholder="Acme content"
              required
            />
            <FieldError
              errors={nameErrors.map((message) => ({ message }))}
            />
          </Field>

          <div className="flex items-center gap-2">
            <Button type="submit" disabled={pending}>
              {pending ? "Creating..." : "Create workspace"}
            </Button>
            <Button
              nativeButton={false}
              variant="ghost"
              render={<Link href="/" />}
            >
              Cancel
            </Button>
          </div>
        </FieldGroup>
      </form>
    </main>
  )
}
