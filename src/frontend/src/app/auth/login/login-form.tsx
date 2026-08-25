"use client"

import { useActionState } from "react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { useErrorToast } from "@/hooks/use-action-toast"

import { loginAction } from "./actions"

const initialState = { error: null }

export default function LoginForm({ returnTo }: { returnTo?: string }) {
  const [state, formAction, pending] = useActionState(loginAction, initialState)
  const emailErrors = state.error?.fieldErrors.email ?? []
  const passwordErrors = state.error?.fieldErrors.password ?? []
  const hasFieldErrors = Object.keys(state.error?.fieldErrors ?? {}).length > 0
  const formError = state.error && !hasFieldErrors ? state.error : null

  useErrorToast(formError)

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="flex w-full justify-center font-semibold">
          Login to cms
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form action={formAction}>
          <FieldGroup>
            {returnTo ? <input name="returnTo" type="hidden" value={returnTo} /> : null}
            <Field data-invalid={emailErrors.length > 0}>
              <FieldLabel htmlFor="email">Email</FieldLabel>
              <Input
                id="email"
                name="email"
                type="email"
                placeholder="user@example.com"
                autoComplete="email"
                required
              />
              <FieldError
                errors={emailErrors.map((message) => ({ message }))}
              />
            </Field>
            <Field data-invalid={passwordErrors.length > 0}>
              <FieldLabel htmlFor="pwd">Password</FieldLabel>
              <Input
                id="pwd"
                name="password"
                type="password"
                autoComplete="current-password"
                required
              />
              <FieldError
                errors={passwordErrors.map((message) => ({ message }))}
              />
            </Field>
            <Field className="my-2">
              <Button type="submit" disabled={pending}>
                {pending ? "Logging in..." : "Login"}
              </Button>
            </Field>
            <p className="text-center text-xs text-muted-foreground">
              {returnTo
                ? "Sign in to accept your workspace invitation."
                : "Accounts are created through workspace invitations."}
            </p>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  )
}
