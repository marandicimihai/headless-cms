/** @vitest-environment jsdom */

import { cleanup, fireEvent, render } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("./actions", () => ({
  changeWorkspaceMemberRoleAction: vi.fn(),
  deleteWorkspaceAction: vi.fn(),
  inviteWorkspaceMemberAction: vi.fn(),
  leaveWorkspaceAction: vi.fn(),
  renameWorkspaceAction: vi.fn(),
  removeWorkspaceMemberAction: vi.fn(),
  resendWorkspaceInvitationAction: vi.fn(),
  revokeWorkspaceInvitationAction: vi.fn(),
  transferWorkspaceOwnershipAction: vi.fn(),
}))

import { LeaveWorkspace, WorkspaceManagement } from "./workspace-management"
import { inviteWorkspaceMemberAction } from "./actions"
import type { WorkspaceInvitation, WorkspaceMember } from "@/lib/types/workspaces"

const member: WorkspaceMember = {
  userId: "user-1",
  email: "member@example.test",
  role: "member",
  joinedAt: "2026-08-24T12:00:00Z",
}

const invitation: WorkspaceInvitation = {
  id: "invitation-1",
  workspaceId: "workspace-1",
  email: "invitee@example.test",
  role: "member",
  status: "pending",
  createdAt: "2026-08-24T12:00:00Z",
  expiresAt: "2026-08-27T12:00:00Z",
  lastSentAt: "2026-08-24T12:00:00Z",
  acceptedAt: null,
  revokedAt: null,
}

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe("WorkspaceManagement", () => {
  it("displays a shareable link after creating an invitation", async () => {
    const url = "https://cms.example.test/auth/invitations/accept?token=one-time-token"
    vi.mocked(inviteWorkspaceMemberAction).mockResolvedValueOnce({
      status: "success", message: "Invitation created", fieldErrors: {}, invitationUrl: url,
    })
    const view = render(
      <WorkspaceManagement workspaceId="workspace-1" workspaceName="Workspace"
        members={[]} invitations={[]} memberTotal={0} invitationTotal={0} />,
    )
    fireEvent.submit(view.getByRole("button", { name: "Create invitation" }).closest("form")!)
    expect((await view.findByLabelText("Invitation link") as HTMLInputElement).value).toBe(url)
    expect(view.getByRole("button", { name: "Copy link" })).toBeTruthy()
  })

  it("requires an exact workspace name and clears confirmation after cancel", async () => {
    const view = render(
      <WorkspaceManagement workspaceId="workspace-1" workspaceName="Workspace"
        members={[]} invitations={[]} memberTotal={0} invitationTotal={0} />,
    )
    fireEvent.click(view.getByRole("button", { name: "Delete workspace" }))
    const input = view.getByLabelText("Confirm workspace name")
    const confirm = view.getByRole("button", { name: "Permanently delete workspace" }) as HTMLButtonElement
    expect(confirm.disabled).toBe(true)
    fireEvent.change(input, { target: { value: "workspace" } })
    expect(confirm.disabled).toBe(true)
    fireEvent.change(input, { target: { value: "Workspace " } })
    expect(confirm.disabled).toBe(true)
    fireEvent.change(input, { target: { value: "Workspace" } })
    expect(confirm.disabled).toBe(false)
    fireEvent.click(view.getByRole("button", { name: "Cancel" }))
    fireEvent.click(view.getByRole("button", { name: "Delete workspace" }))
    expect((view.getByLabelText("Confirm workspace name") as HTMLInputElement).value).toBe("")
    expect((view.getByRole("button", { name: "Permanently delete workspace" }) as HTMLButtonElement).disabled).toBe(true)
  })

  it("updates server-derived form values as controlled state", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {})
    const view = render(
      <WorkspaceManagement
        workspaceId="workspace-1"
        workspaceName="Original name"
        members={[member]}
        invitations={[]}
        memberTotal={1}
        invitationTotal={0}
      />,
    )

    view.rerender(
      <WorkspaceManagement
        workspaceId="workspace-1"
        workspaceName="Renamed workspace"
        members={[{ ...member, role: "editor" }]}
        invitations={[]}
        memberTotal={1}
        invitationTotal={0}
      />,
    )

    const nameInput = view.container.querySelector<HTMLInputElement>(
      'input[name="name"]',
    )
    const roleInputs = view.container.querySelectorAll<HTMLInputElement>(
      'input[name="role"]',
    )
    const memberRoleInput = roleInputs.item(roleInputs.length - 1)

    expect(nameInput?.value).toBe("Renamed workspace")
    expect(memberRoleInput.value).toBe("editor")
    expect(
      consoleError.mock.calls.some((call) =>
        call.some((value) =>
          String(value).includes("changing the default value state"),
        ),
      ),
    ).toBe(false)
  })

  it("provides regenerate and revoke controls inside the invitation actions menu", () => {
    const view = render(
      <WorkspaceManagement
        workspaceId="workspace-1"
        workspaceName="Workspace"
        members={[]}
        invitations={[invitation]}
        memberTotal={0}
        invitationTotal={1}
      />,
    )

    fireEvent.click(
      view.getByRole("button", {
        name: "Open invitation actions for invitee@example.test",
      }),
    )

    expect(view.getByRole("menuitem", { name: "Generate new link" })).toBeTruthy()
    expect(view.getByRole("menuitem", { name: "Revoke" })).toBeTruthy()
  })

  it("provides ownership transfer and member removal for non-owner members", () => {
    const view = render(
      <WorkspaceManagement
        workspaceId="workspace-1"
        workspaceName="Workspace"
        members={[member]}
        invitations={[]}
        memberTotal={1}
        invitationTotal={0}
      />,
    )

    const transferButton = view.getByRole("button", {
      name: "Transfer ownership",
    }) as HTMLButtonElement
    expect(transferButton.disabled).toBe(true)
    expect(view.getByRole("button", { name: "Remove" })).toBeTruthy()
    expect(view.getByText("Select a member")).toBeTruthy()
  })

  it("provides workspace leaving for editors and read-only members", () => {
    const view = render(
      <LeaveWorkspace workspaceId="workspace-1" workspaceName="Workspace" />,
    )

    fireEvent.click(view.getByRole("button", { name: "Leave workspace" }))
    expect(view.getByRole("heading", { name: "Leave Workspace?" })).toBeTruthy()
  })
})
