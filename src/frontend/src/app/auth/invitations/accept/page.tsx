import Link from "next/link"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { previewInvitation } from "@/lib/api/auth"
import { getSession } from "@/lib/api/session"

import { InvitationForm } from "./invitation-form"

type InvitationPageProps = {
  searchParams: Promise<{ token?: string | string[] }>
}

export default async function AcceptInvitationPage({
  searchParams,
}: InvitationPageProps) {
  const { token } = await searchParams

  if (typeof token !== "string" || token.length === 0) {
    return <InvitationError detail="This invitation link is missing its token." />
  }

  const preview = await previewInvitation(token)

  if (!preview.ok) {
    return <InvitationError detail={preview.error.detail} />
  }

  const session = await getSession()
  const loginHref = `/auth/login?returnTo=${encodeURIComponent(
    `/auth/invitations/accept?token=${token}`,
  )}`

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle>Join {preview.data.workspaceName}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-6">
        <InvitationForm
          invitation={preview.data}
          session={session}
          token={token}
        />
        {!session ? (
          <p className="text-sm text-muted-foreground">
            Already have an account?{" "}
            <Button
              nativeButton={false}
              variant="link"
              className="h-auto p-0"
              render={<Link href={loginHref} />}
            >
              Sign in to join
            </Button>
          </p>
        ) : null}
      </CardContent>
    </Card>
  )
}

function InvitationError({ detail }: { detail: string }) {
  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle>Invitation unavailable</CardTitle>
      </CardHeader>
      <CardContent>
        <Alert variant="destructive">
          <AlertTitle>Unable to open invitation</AlertTitle>
          <AlertDescription>{detail}</AlertDescription>
        </Alert>
      </CardContent>
    </Card>
  )
}
