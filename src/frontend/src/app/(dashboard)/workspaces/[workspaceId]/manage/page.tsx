import { redirect } from "next/navigation"
import { CircleAlert } from "lucide-react"

import { LeaveWorkspace, WorkspaceManagement } from "../workspace-management"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { getSession } from "@/lib/api/session"
import {
  listMyWorkspaces,
  listPendingWorkspaceInvitations,
  listWorkspaceMembers,
} from "@/lib/api/workspaces"
import type { WorkspaceRole } from "@/lib/types/workspaces"

const dateFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: "UTC",
})

const roleDetails: Record<
  WorkspaceRole,
  { label: string }
> = {
  owner: { label: "Owner" },
  editor: { label: "Editor" },
  member: { label: "Read only" },
}

function formatCreatedAt(createdAt: string) {
  const date = new Date(createdAt)
  return Number.isNaN(date.getTime())
    ? "Unknown date"
    : `${dateFormatter.format(date)} UTC`
}

export default async function WorkspaceManagementPage({
  params,
}: {
  params: Promise<{ workspaceId: string }>
}) {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  const { workspaceId } = await params
  const workspaceResult = await listMyWorkspaces()

  if (!workspaceResult.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
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
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Workspace unavailable
          </h1>
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
  const isOwner = role === "owner"
  const [membersResult, invitationsResult] = isOwner
    ? await Promise.all([
        listWorkspaceMembers(resolvedWorkspaceId),
        listPendingWorkspaceInvitations(resolvedWorkspaceId),
      ])
    : [null, null]

  return (
    <main className="flex flex-1 flex-col gap-8 p-4 md:p-6">
      <div className="space-y-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-1">
            <h1 className="text-2xl font-semibold tracking-tight">
              {workspace.name}
            </h1>
            <p className="text-sm text-muted-foreground">
              Created {formatCreatedAt(workspace.createdAt)}
            </p>
          </div>
          <Badge
            variant={
              role === "owner"
                ? "default"
                : role === "editor"
                  ? "secondary"
                  : "outline"
            }
          >
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
          membersError={
            membersResult && !membersResult.ok
              ? membersResult.error.detail
              : undefined
          }
          invitationsError={
            invitationsResult && !invitationsResult.ok
              ? invitationsResult.error.detail
              : undefined
          }
        />
      ) : (
        <LeaveWorkspace
          workspaceId={resolvedWorkspaceId}
          workspaceName={workspace.name}
        />
      )}
    </main>
  )
}
