import { redirect } from "next/navigation"
import { CircleAlert } from "lucide-react"

import { WorkspaceManagement } from "./workspace-management"
import { BackButton } from "@/components/dashboard/back-button"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { getSession } from "@/lib/api/session"
import {
  listMyWorkspaces,
  listPendingWorkspaceInvitations,
  listWorkspaceMembers,
} from "@/lib/api/workspaces"
import type { WorkspaceRole } from "@/lib/types/workspaces"

const dateFormatter = new Intl.DateTimeFormat("en", {
  dateStyle: "medium",
  timeZone: "UTC",
})

const roleDetails: Record<
  WorkspaceRole,
  { label: string; summary: string }
> = {
  Owner: {
    label: "Owner",
    summary: "Full access",
  },
  Editor: {
    label: "Editor",
    summary: "Can manage projects and content",
  },
  Member: {
    label: "Read only",
    summary: "View-only access",
  },
}

function formatCreatedAt(createdAt: string) {
  const date = new Date(createdAt)
  return Number.isNaN(date.getTime()) ? "Unknown date" : dateFormatter.format(date)
}

export default async function WorkspacePage({
  params,
}: {
  params: Promise<{ workspaceId: string }>
}) {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  const { workspaceId } = await params
  const workspaceResult = await listMyWorkspaces(session.accessToken)

  if (!workspaceResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <BackButton href="/workspaces">Back to workspaces</BackButton>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Workspace</h1>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Unable to load workspace</AlertTitle>
          <AlertDescription>{workspaceResult.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  const workspace = workspaceResult.data.find(
    (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
  )

  if (!workspace?.currentRole) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <BackButton href="/workspaces">Back to workspaces</BackButton>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Workspace unavailable</h1>
          <p className="text-sm text-muted-foreground">
            This workspace is not available to your account.
          </p>
        </div>
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>Access unavailable</AlertTitle>
          <AlertDescription>
            Ask a workspace owner to send an invitation if you need access.
          </AlertDescription>
        </Alert>
      </main>
    )
  }

  const role = workspace.currentRole
  const resolvedWorkspaceId = workspace.id
  const details = roleDetails[role]
  const isOwner = role === "Owner"
  const [membersResult, invitationsResult] = isOwner
    ? await Promise.all([
        listWorkspaceMembers(session.accessToken, resolvedWorkspaceId),
        listPendingWorkspaceInvitations(session.accessToken, resolvedWorkspaceId),
      ])
    : [null, null]

  return (
    <main className="flex flex-1 flex-col gap-8 p-4 md:p-6">
      <div className="space-y-4">
        <BackButton href="/workspaces">Back to workspaces</BackButton>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-1">
            <h1 className="text-2xl font-semibold tracking-tight">{workspace.name}</h1>
            <p className="text-sm text-muted-foreground">
              {details.summary}
              <span className="px-1.5" aria-hidden="true">·</span>
              Created {formatCreatedAt(workspace.createdAt)}
            </p>
          </div>
          <Badge variant={role === "Owner" ? "default" : role === "Editor" ? "secondary" : "outline"}>
            {details.label}
          </Badge>
        </div>
      </div>

      {isOwner ? (
        <WorkspaceManagement
          workspaceId={resolvedWorkspaceId}
          workspaceName={workspace.name}
          members={membersResult?.ok ? membersResult.data.items : []}
          invitations={invitationsResult?.ok ? invitationsResult.data.items : []}
          memberTotal={membersResult?.ok ? membersResult.data.total : 0}
          invitationTotal={
            invitationsResult?.ok ? invitationsResult.data.total : 0
          }
          membersError={membersResult && !membersResult.ok ? membersResult.error.detail : undefined}
          invitationsError={
            invitationsResult && !invitationsResult.ok
              ? invitationsResult.error.detail
              : undefined
          }
        />
      ) : (
        <section aria-labelledby="management-heading" className="space-y-2">
          <h2 id="management-heading" className="text-sm font-semibold">
            Workspace management
          </h2>
          <p className="max-w-2xl text-sm text-muted-foreground">
            Only the workspace owner can rename this workspace, invite people,
            or change member access. Contact the owner if your role needs to
            change.
          </p>
        </section>
      )}
    </main>
  )
}
