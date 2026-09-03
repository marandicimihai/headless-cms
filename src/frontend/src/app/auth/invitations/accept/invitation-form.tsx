"use client"

import { useActionState } from "react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import type { AuthSession, InvitationPreview } from "@/lib/types/auth"
import type { ApiError } from "@/lib/types/general"

import {
  acceptInvitationAction,
  registerInvitationAction,
} from "./actions"

type InvitationActionState = {
  error: ApiError | null
}

const initialState: InvitationActionState = { error: null }

type InvitationFormProps = {
  invitation: InvitationPreview
  session: AuthSession | null
  token: string
}

export function InvitationForm({
  invitation,
  session,
  token,
}: InvitationFormProps) {
  const action = session ? acceptInvitationAction : registerInvitationAction
  const [state, formAction, pending] = useActionState(action, initialState)
  const passwordErrors = state.error?.fieldErrors.password ?? []

  return (
    <form action={formAction} className="w-full" noValidate>
      <FieldGroup>
        <input name="token" type="hidden" value={token} />
        <div className="space-y-1">
          <p className="text-sm text-muted-foreground">Workspace</p>
          <p className="font-medium">{invitation.workspaceName}</p>
        </div>
        <div className="space-y-1">
          <p className="text-sm text-muted-foreground">Invitation for</p>
          <p className="font-medium">{invitation.maskedEmail}</p>
        </div>
        {state.error ? (
          <Alert variant="destructive">
            <AlertTitle>Unable to accept invitation</AlertTitle>
            <AlertDescription>{state.error.detail}</AlertDescription>
          </Alert>
        ) : null}
        {session ? (
          <>
            <p className="text-sm text-muted-foreground">
              You are signed in as {session.email}.
            </p>
            <Button type="submit" disabled={pending}>
              {pending ? "Joining workspace..." : "Join workspace"}
            </Button>
          </>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">
              Create a password to join this workspace and sign in.
            </p>
            <Field data-invalid={passwordErrors.length > 0}>
              <FieldLabel htmlFor="password">Password</FieldLabel>
              <Input
                id="password"
                name="password"
                type="password"
                autoComplete="new-password"
                required
              />
              <FieldError
                errors={passwordErrors.map((message) => ({ message }))}
              />
            </Field>
            <Button type="submit" disabled={pending}>
              {pending ? "Creating account..." : "Create account and join"}
            </Button>
          </>
        )}
      </FieldGroup>
    </form>
  )
}
