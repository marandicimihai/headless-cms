"use client"

import { CircleAlert, MoreHorizontal } from "lucide-react"
import { useActionState, useState } from "react"

import {
  changeWorkspaceMemberRoleAction,
  inviteWorkspaceMemberAction,
  renameWorkspaceAction,
  resendWorkspaceInvitationAction,
  revokeWorkspaceInvitationAction,
} from "./actions"
import type { WorkspaceActionState } from "./actions"
import { useActionToast } from "@/hooks/use-action-toast"
import { Alert, AlertDescription } from "@/components/ui/alert"
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import type {
  WorkspaceInvitation,
  WorkspaceMember,
  WorkspaceRole,
} from "@/lib/types/workspaces"

const initialWorkspaceActionState: WorkspaceActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

const dateFormatter = new Intl.DateTimeFormat("en", {
  dateStyle: "medium",
  timeZone: "UTC",
})

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? "Unknown date" : dateFormatter.format(date)
}

function roleLabel(role: WorkspaceRole) {
  return role === "member" ? "Read only" : role === "owner" ? "Owner" : "Editor"
}

function RenameWorkspaceForm({
  workspaceId,
  workspaceName,
}: {
  workspaceId: string
  workspaceName: string
}) {
  const renameAction = renameWorkspaceAction.bind(null, workspaceId)
  const [state, formAction, pending] = useActionState(
    renameAction,
    initialWorkspaceActionState,
  )
  useActionToast(state)
  const [name, setName] = useState(workspaceName)
  const [previousWorkspaceName, setPreviousWorkspaceName] =
    useState(workspaceName)
  const nameErrors = state.fieldErrors.name ?? []

  if (previousWorkspaceName !== workspaceName) {
    setPreviousWorkspaceName(workspaceName)
    setName(workspaceName)
  }

  return (
    <form action={formAction} className="max-w-2xl space-y-4">
      <Field data-invalid={nameErrors.length > 0}>
        <FieldLabel htmlFor="workspace-name">Workspace name</FieldLabel>
        <div className="flex flex-col gap-3 sm:flex-row">
          <Input
            id="workspace-name"
            name="name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={100}
            required
          />
          <Button type="submit" disabled={pending}>
            {pending ? "Saving..." : "Save name"}
          </Button>
        </div>
        <FieldError errors={nameErrors.map((message) => ({ message }))} />
      </Field>
    </form>
  )
}

function InviteMemberForm({ workspaceId }: { workspaceId: string }) {
  const inviteAction = inviteWorkspaceMemberAction.bind(null, workspaceId)
  const [state, formAction, pending] = useActionState(
    inviteAction,
    initialWorkspaceActionState,
  )
  useActionToast(state)
  const emailErrors = state.fieldErrors.email ?? []
  const roleErrors = state.fieldErrors.role ?? []

  return (
    <form action={formAction} className="max-w-2xl space-y-4">
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_11rem_auto] sm:items-end">
        <Field data-invalid={emailErrors.length > 0}>
          <FieldLabel htmlFor="invitation-email">Email address</FieldLabel>
          <Input
            id="invitation-email"
            name="email"
            type="email"
            placeholder="person@example.com"
            maxLength={320}
            required
          />
          <FieldError errors={emailErrors.map((message) => ({ message }))} />
        </Field>
        <Field data-invalid={roleErrors.length > 0}>
          <FieldLabel htmlFor="invitation-role">Access</FieldLabel>
          <Select name="role" defaultValue="member" required>
            <SelectTrigger id="invitation-role" className="w-full">
              <SelectValue>
                {(value) => (value === "editor" ? "Editor" : "Read only")}
              </SelectValue>
            </SelectTrigger>
            <SelectContent align="start" alignItemWithTrigger={false}>
              <SelectItem value="member">Read only</SelectItem>
              <SelectItem value="editor">Editor</SelectItem>
            </SelectContent>
          </Select>
          <FieldError errors={roleErrors.map((message) => ({ message }))} />
        </Field>
        <Button type="submit" disabled={pending}>
          {pending ? "Sending..." : "Send invitation"}
        </Button>
      </div>
    </form>
  )
}

function MemberRoleForm({
  workspaceId,
  member,
}: {
  workspaceId: string
  member: WorkspaceMember
}) {
  const roleAction = changeWorkspaceMemberRoleAction.bind(
    null,
    workspaceId,
    member.userId,
  )
  const [state, formAction, pending] = useActionState(
    roleAction,
    initialWorkspaceActionState,
  )
  useActionToast(state)
  const [role, setRole] = useState(member.role)
  const [previousRole, setPreviousRole] = useState(member.role)

  if (previousRole !== member.role) {
    setPreviousRole(member.role)
    setRole(member.role)
  }

  if (member.role === "owner") {
    return <Badge>Owner</Badge>
  }

  return (
    <div>
      <form action={formAction} className="flex items-center justify-end gap-2">
        <Select
          name="role"
          value={role}
          onValueChange={(value) => {
            if (value === "member" || value === "editor") setRole(value)
          }}
          required
        >
          <SelectTrigger
            aria-label={`Access for ${member.email}`}
            size="sm"
            className="w-28"
          >
            <SelectValue>
              {(value) => (value === "editor" ? "Editor" : "Read only")}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="member">Read only</SelectItem>
            <SelectItem value="editor">Editor</SelectItem>
          </SelectContent>
        </Select>
        <Button type="submit" variant="outline" size="sm" disabled={pending}>
          {pending ? "Saving..." : "Save"}
        </Button>
      </form>
    </div>
  )
}

function InvitationActions({
  workspaceId,
  invitation,
}: {
  workspaceId: string
  invitation: WorkspaceInvitation
}) {
  const [revokeDialogOpen, setRevokeDialogOpen] = useState(false)
  const resendAction = resendWorkspaceInvitationAction.bind(
    null,
    workspaceId,
    invitation.id,
  )
  const revokeAction = revokeWorkspaceInvitationAction.bind(
    null,
    workspaceId,
    invitation.id,
  )
  const [resendState, resendFormAction, resendPending] = useActionState(
    resendAction,
    initialWorkspaceActionState,
  )
  const [revokeState, revokeFormAction, revokePending] = useActionState(
    revokeAction,
    initialWorkspaceActionState,
  )

  useActionToast(resendState)
  useActionToast(revokeState)
  const resendFormId = `resend-invitation-${invitation.id}`
  const revokeFormId = `revoke-invitation-${invitation.id}`

  return (
    <>
      <form id={resendFormId} action={resendFormAction} />
      <DropdownMenu>
        <DropdownMenuTrigger
          render={
            <Button
              aria-label={`Open invitation actions for ${invitation.email}`}
              size="icon-sm"
              variant="ghost"
            />
          }
        >
          <MoreHorizontal />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-fit">
          <DropdownMenuItem
            nativeButton
            disabled={resendPending}
            render={<button type="submit" form={resendFormId} />}
          >
            {resendPending ? "Resending..." : "Resend"}
          </DropdownMenuItem>
          <DropdownMenuItem
            variant="destructive"
            disabled={revokePending}
            onClick={() => setRevokeDialogOpen(true)}
          >
            Revoke
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <AlertDialog
        open={revokeDialogOpen}
        onOpenChange={setRevokeDialogOpen}
      >
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Revoke invitation?</AlertDialogTitle>
            <AlertDialogDescription>
              {invitation.email} will no longer be able to use this invitation link.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <form id={revokeFormId} action={revokeFormAction} />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={revokePending}>Cancel</AlertDialogCancel>
            <Button
              type="submit"
              form={revokeFormId}
              variant="destructive"
              disabled={revokePending}
            >
              {revokePending ? "Revoking..." : "Revoke invitation"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}

export function WorkspaceManagement({
  workspaceId,
  workspaceName,
  members,
  invitations,
  memberTotal,
  invitationTotal,
  membersError,
  invitationsError,
}: {
  workspaceId: string
  workspaceName: string
  members: WorkspaceMember[]
  invitations: WorkspaceInvitation[]
  memberTotal: number
  invitationTotal: number
  membersError?: string
  invitationsError?: string
}) {
  return (
    <div className="space-y-10">
      <RenameWorkspaceForm workspaceId={workspaceId} workspaceName={workspaceName} />

      <section aria-labelledby="invite-heading" className="space-y-4">
        <div>
          <h2 id="invite-heading" className="text-sm font-semibold">
            Invite people
          </h2>
        </div>
        <InviteMemberForm workspaceId={workspaceId} />
      </section>

      <section aria-labelledby="members-heading" className="space-y-4">
        <div>
          <h2 id="members-heading" className="text-sm font-semibold">
            Members
          </h2>
        </div>
        {membersError ? (
          <Alert variant="destructive">
            <CircleAlert />
            <AlertDescription>{membersError}</AlertDescription>
          </Alert>
        ) : members.length ? (
          <div className="overflow-x-auto rounded-xl border">
            <Table className="min-w-[36rem]">
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>
                    Member
                  </TableHead>
                  <TableHead>
                    Joined
                  </TableHead>
                  <TableHead className="text-right">
                    Access
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {members.map((member) => (
                  <TableRow key={member.userId}>
                    <TableCell className="font-medium">
                      {member.email}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {formatDate(member.joinedAt)}
                    </TableCell>
                    <TableCell className="text-right">
                      <MemberRoleForm workspaceId={workspaceId} member={member} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            {memberTotal > members.length ? (
              <p className="border-t px-6 py-3 text-xs text-muted-foreground">
                Showing the first {members.length} of {memberTotal} members.
              </p>
            ) : null}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">No members found.</p>
        )}
      </section>

      <section
        aria-labelledby="invitations-heading"
        className="space-y-4"
      >
        <div>
          <h2 id="invitations-heading" className="text-sm font-semibold">
            Pending invitations
          </h2>
        </div>
        {invitationsError ? (
          <Alert variant="destructive">
            <CircleAlert />
            <AlertDescription>{invitationsError}</AlertDescription>
          </Alert>
        ) : invitations.length ? (
          <div className="overflow-x-auto rounded-xl border">
            <Table className="min-w-[44rem]">
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>
                    Email
                  </TableHead>
                  <TableHead>
                    Sent
                  </TableHead>
                  <TableHead>
                    Expires
                  </TableHead>
                  <TableHead>
                    Access
                  </TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {invitations.map((invitation) => (
                  <TableRow key={invitation.id}>
                    <TableCell className="font-medium">
                      {invitation.email}
                    </TableCell>
                    <TableCell>
                      {formatDate(invitation.lastSentAt ?? invitation.createdAt)}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {formatDate(invitation.expiresAt)}
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{roleLabel(invitation.role)}</Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <InvitationActions
                        workspaceId={workspaceId}
                        invitation={invitation}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            {invitationTotal > invitations.length ? (
              <p className="border-t px-6 py-3 text-xs text-muted-foreground">
                Showing the first {invitations.length} of {invitationTotal} pending invitations.
              </p>
            ) : null}
          </div>
        ) : (
          <div className="overflow-x-auto rounded-xl border">
            <Table className="min-w-[44rem]">
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>
                    Email
                  </TableHead>
                  <TableHead>
                    Sent
                  </TableHead>
                  <TableHead>
                    Expires
                  </TableHead>
                  <TableHead>
                    Access
                  </TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow>
                  <TableCell
                    colSpan={5}
                    className="py-10 text-center text-sm text-muted-foreground"
                  >
                    There are no pending invitations.
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        )}
      </section>
    </div>
  )
}
