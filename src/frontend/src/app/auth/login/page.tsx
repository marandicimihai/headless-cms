"use client";

import { Button } from "@/components/ui/button";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { loginAction } from "./actions";
import { useActionState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { CircleAlert } from "lucide-react";

const initialState = { error: null }

export default function LoginForm() {
  const [state, formAction, pending] = useActionState(
    loginAction,
    initialState
  )

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle className="w-full flex justify-center font-semibold">
          Login to cms
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form action={formAction}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="email">Email</FieldLabel>
              <Input
                id="email"
                name="email"
                type="email"
                placeholder="user@example.com"
                required
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="pwd">Password</FieldLabel>
              <Input
                id="pwd"
                name="password"
                type="password"
                required
              />
            </Field>
              {state.error && (
                <Alert variant="destructive">
                  <CircleAlert />
                  <AlertTitle>Unable to sign in</AlertTitle>
                  <AlertDescription>
                    Check your email and password, then try again.
                  </AlertDescription>
                </Alert>
              )}
            <Field className="my-2">
              <Button type="submit" disabled={pending}>
                {pending ? "Logging in..." : "Login"}
              </Button>
            </Field>
            <p className="text-center text-xs text-muted-foreground">
              Accounts are created through workspace invitations.
            </p>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  )
}