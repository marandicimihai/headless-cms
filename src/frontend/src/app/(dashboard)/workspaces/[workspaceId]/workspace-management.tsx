"use client"

import { CircleAlert, MoreHorizontal } from "lucide-react"
import { useActionState, useId, useState } from "react"
import { toast } from "sonner"

import {
  changeWorkspaceMemberRoleAction,
  deleteWorkspaceAction,
  inviteWorkspaceMemberAction,
  leaveWorkspaceAction,
  renameWorkspaceAction,
  removeWorkspaceMemberAction,
  resendWorkspaceInvitationAction,
  revokeWorkspaceInvitationAction,
  transferWorkspaceOwnershipAction,
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
  AlertDialogTrigger,
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

const dateFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: "UTC",
})

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? "Unknown date"
    : `${dateFormatter.format(date)} UTC`
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
    <form action={formAction} className="max-w-2xl space-y-4" noValidate>
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

function InvitationLink({ url }: { url: string }) {
  const id = useId()
  async function copyLink() {
    try {
      await navigator.clipboard.writeText(url)
      toast.success("Invitation link copied")
    } catch {
      toast.error("Unable to copy. Select the link and copy it manually.")
    }
  }
  return (
    <Field>
      <FieldLabel htmlFor={id}>Invitation link</FieldLabel>
      <div className="flex gap-2">
        <Input id={id} value={url} readOnly onFocus={(event) => event.target.select()} />
        <Button type="button" variant="outline" onClick={copyLink}>Copy link</Button>
      </div>
      <p className="text-sm text-muted-foreground">
        Share this link with the invited person. Copy it now; to retrieve a link later,
        generate a new one from the invitation menu. Generating a new link invalidates the old one.
      </p>
    </Field>
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
    <form action={formAction} className="max-w-2xl space-y-4" noValidate>
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_11rem_auto] sm:items-start">
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
        <Button type="submit" className="sm:mt-7" disabled={pending}>
          {pending ? "Creating..." : "Create invitation"}
        </Button>
      </div>
      {state.invitationUrl && !pending ? <InvitationLink url={state.invitationUrl} /> : null}
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
    <div className="flex items-center justify-end gap-2">
      <form action={formAction} className="flex items-center gap-2" noValidate>
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
      <RemoveMemberButton workspaceId={workspaceId} member={member} />
    </div>
  )
}

function RemoveMemberButton({
  workspaceId,
  member,
}: {
  workspaceId: string
  member: WorkspaceMember
}) {
  const [open, setOpen] = useState(false)
  const removeAction = removeWorkspaceMemberAction.bind(
    null,
    workspaceId,
    member.userId,
  )
  const [state, formAction, pending] = useActionState(
    removeAction,
    initialWorkspaceActionState,
  )
  useActionToast(state)
  const formId = `remove-member-${member.userId}`

  return (
    <AlertDialog
      open={open}
      onOpenChange={(nextOpen) => {
        if (!pending) setOpen(nextOpen)
      }}
    >
      <AlertDialogTrigger render={<Button size="sm" variant="ghost" />}>
        Remove
      </AlertDialogTrigger>
      <AlertDialogContent size="sm">
        <AlertDialogHeader>
          <AlertDialogTitle>Remove {member.email}?</AlertDialogTitle>
          <AlertDialogDescription>
            They will immediately lose access to this workspace and its content.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <form id={formId} action={formAction} noValidate>
          {state.status === "error" ? (
            <Alert variant="destructive">
              <CircleAlert />
              <AlertDescription>{state.message}</AlertDescription>
            </Alert>
          ) : null}
        </form>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
          <Button
            type="submit"
            form={formId}
            variant="destructive"
            disabled={pending}
          >
            {pending ? "Removing..." : "Remove member"}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
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
  const [dismissedLink, setDismissedLink] = useState<string | undefined>()
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
      <form id={resendFormId} action={resendFormAction} noValidate />
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
            {resendPending ? "Generating..." : "Generate new link"}
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
        open={!!resendState.invitationUrl && resendState.invitationUrl !== dismissedLink}
        onOpenChange={(open) => { if (!open) setDismissedLink(resendState.invitationUrl) }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Share invitation</AlertDialogTitle>
            <AlertDialogDescription>
              A new link for {invitation.email}. The previous link no longer works.
            </AlertDialogDescription>
          </AlertDialogHeader>
          {resendState.invitationUrl ? <InvitationLink url={resendState.invitationUrl} /> : null}
          <AlertDialogFooter><AlertDialogCancel>Done</AlertDialogCancel></AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

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
          <form id={revokeFormId} action={revokeFormAction} noValidate />
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
                    Last generated
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
      <TransferOwnershipForm workspaceId={workspaceId} members={members} />
      <DeleteWorkspaceForm key={workspaceName} workspaceId={workspaceId} workspaceName={workspaceName} />
    </div>
  )
}

function TransferOwnershipForm({
  workspaceId,
  members,
}: {
  workspaceId: string
  members: WorkspaceMember[]
}) {
  const eligibleMembers = members.filter((member) => member.role !== "owner")
  const [newOwnerUserId, setNewOwnerUserId] = useState("")
  const [open, setOpen] = useState(false)
  const transferAction = transferWorkspaceOwnershipAction.bind(null, workspaceId)
  const [state, formAction, pending] = useActionState(
    transferAction,
    initialWorkspaceActionState,
  )
  useActionToast(state)
  const newOwnerErrors = state.fieldErrors.newOwnerUserId ?? []
  const newOwner = eligibleMembers.find(
    (member) => member.userId === newOwnerUserId,
  )

  return (
    <section aria-labelledby="transfer-ownership-heading" className="space-y-4">
      <div className="space-y-1">
        <h2 id="transfer-ownership-heading" className="text-sm font-semibold">
          Transfer ownership
        </h2>
        <p className="text-sm text-muted-foreground">
          Choose an existing member to become the owner. You will become an editor
          and can leave the workspace afterward.
        </p>
      </div>
      {eligibleMembers.length ? (
        <div className="flex max-w-2xl flex-col gap-3 sm:flex-row sm:items-end">
          <Field
            className="flex-1"
            data-invalid={newOwnerErrors.length > 0}
          >
            <FieldLabel htmlFor="new-workspace-owner">New owner</FieldLabel>
            <Select
              name="newOwnerUserId"
              value={newOwnerUserId}
              onValueChange={(value) => {
                if (value) setNewOwnerUserId(value)
              }}
              required
            >
              <SelectTrigger id="new-workspace-owner" className="w-full">
                <SelectValue>
                  {(value) => {
                    const member = eligibleMembers.find(
                      (candidate) => candidate.userId === value,
                    )

                    return member
                      ? `${member.email} (${roleLabel(member.role)})`
                      : "Select a member"
                  }}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {eligibleMembers.map((member) => (
                  <SelectItem key={member.userId} value={member.userId}>
                    {member.email} ({roleLabel(member.role)})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <FieldError
              errors={newOwnerErrors.map((message) => ({ message }))}
            />
          </Field>
          <AlertDialog
            open={open}
            onOpenChange={(nextOpen) => {
              if (!pending) setOpen(nextOpen)
            }}
          >
            <AlertDialogTrigger
              disabled={!newOwner}
              render={<Button variant="destructive" />}
            >
              Transfer ownership
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>
                  Transfer ownership to {newOwner?.email}?
                </AlertDialogTitle>
                <AlertDialogDescription>
                  {newOwner?.email} will gain full control, including the ability to
                  delete this workspace. You will become an editor.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <form id="transfer-workspace-ownership" action={formAction} noValidate>
                <input name="newOwnerUserId" type="hidden" value={newOwnerUserId} />
                {state.status === "error" ? (
                  <Alert variant="destructive">
                    <CircleAlert />
                    <AlertDescription>{state.message}</AlertDescription>
                  </Alert>
                ) : null}
              </form>
              <AlertDialogFooter>
                <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
                <Button
                  type="submit"
                  form="transfer-workspace-ownership"
                  variant="destructive"
                  disabled={pending || !newOwner}
                >
                  {pending ? "Transferring..." : "Transfer ownership"}
                </Button>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          Invite and add another member before transferring ownership.
        </p>
      )}
    </section>
  )
}

export function LeaveWorkspace({
  workspaceId,
  workspaceName,
}: {
  workspaceId: string
  workspaceName: string
}) {
  const [open, setOpen] = useState(false)
  const [state, formAction, pending] = useActionState(
    leaveWorkspaceAction.bind(null, workspaceId),
    initialWorkspaceActionState,
  )

  return (
    <section aria-labelledby="leave-workspace-heading" className="space-y-4">
      <div className="space-y-1">
        <h2 id="leave-workspace-heading" className="text-sm font-semibold">
          Leave workspace
        </h2>
        <p className="text-sm text-muted-foreground">
          Leave {workspaceName} and remove your access to its projects and content.
        </p>
      </div>
      <AlertDialog
        open={open}
        onOpenChange={(nextOpen) => {
          if (!pending) setOpen(nextOpen)
        }}
      >
        <AlertDialogTrigger render={<Button variant="destructive" />}>
          Leave workspace
        </AlertDialogTrigger>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Leave {workspaceName}?</AlertDialogTitle>
            <AlertDialogDescription>
              You will no longer be able to access this workspace unless an owner
              invites you again.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <form id="leave-workspace-form" action={formAction} noValidate>
            {state.status === "error" ? (
              <Alert variant="destructive">
                <CircleAlert />
                <AlertDescription>{state.message}</AlertDescription>
              </Alert>
            ) : null}
          </form>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
            <Button
              type="submit"
              form="leave-workspace-form"
              variant="destructive"
              disabled={pending}
            >
              {pending ? "Leaving..." : "Leave workspace"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </section>
  )
}

function DeleteWorkspaceForm({
  workspaceId,
  workspaceName,
}: {
  workspaceId: string
  workspaceName: string
}) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState("")
  const [state, formAction, pending] = useActionState(
    deleteWorkspaceAction.bind(null, workspaceId),
    initialWorkspaceActionState,
  )
  const nameErrors = state.fieldErrors.name ?? []

  return (
    <section aria-labelledby="delete-workspace-heading" className="space-y-4">
      <div className="space-y-1">
        <h2 id="delete-workspace-heading" className="text-sm font-semibold">
          Delete workspace
        </h2>
        <p className="text-sm text-muted-foreground">
          Permanently delete this workspace and all its projects and content. This cannot be undone.
        </p>
      </div>
      <AlertDialog open={open} onOpenChange={(nextOpen) => {
        if (pending) return
        setName("")
        setOpen(nextOpen)
      }}>
        <AlertDialogTrigger render={<Button variant="destructive" />}>
          Delete workspace
        </AlertDialogTrigger>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete workspace?</AlertDialogTitle>
            <AlertDialogDescription>
              This permanently deletes all projects, content, memberships, and invitations
              in this workspace. Type {workspaceName} exactly to confirm.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <form
            id="delete-workspace-form"
            action={formAction}
            onSubmit={(event) => {
              if (pending || name !== workspaceName) event.preventDefault()
            }}
            className="space-y-4"
          >
            {state.status === "error" ? (
              <Alert variant="destructive">
                <CircleAlert />
                <AlertDescription>{state.message}</AlertDescription>
              </Alert>
            ) : null}
            <Field data-invalid={nameErrors.length > 0}>
              <FieldLabel htmlFor="delete-workspace-name">Confirm workspace name</FieldLabel>
              <Input
                id="delete-workspace-name"
                name="name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                autoComplete="off"
                maxLength={100}
                required
                disabled={pending}
                aria-invalid={nameErrors.length > 0}
              />
              <FieldError errors={nameErrors.map((message) => ({ message }))} />
            </Field>
          </form>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
            <Button type="submit" form="delete-workspace-form" variant="destructive"
              disabled={pending || name !== workspaceName}>
              {pending ? "Deleting..." : "Permanently delete workspace"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </section>
  )
}
