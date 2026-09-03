/** @vitest-environment jsdom */

import { cleanup, fireEvent, render } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("./actions", () => ({
  changeWorkspaceMemberRoleAction: vi.fn(),
  inviteWorkspaceMemberAction: vi.fn(),
  renameWorkspaceAction: vi.fn(),
  resendWorkspaceInvitationAction: vi.fn(),
  revokeWorkspaceInvitationAction: vi.fn(),
}))

import { WorkspaceManagement } from "./workspace-management"
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

  it("provides resend and revoke controls inside the invitation actions menu", () => {
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

    expect(view.getByRole("menuitem", { name: "Resend" })).toBeTruthy()
    expect(view.getByRole("menuitem", { name: "Revoke" })).toBeTruthy()
  })
})
