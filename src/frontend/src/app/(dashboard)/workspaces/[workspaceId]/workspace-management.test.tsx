/** @vitest-environment jsdom */

import { cleanup, render } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

vi.mock("./actions", () => ({
  changeWorkspaceMemberRoleAction: vi.fn(),
  inviteWorkspaceMemberAction: vi.fn(),
  renameWorkspaceAction: vi.fn(),
}))

import { WorkspaceManagement } from "./workspace-management"
import type { WorkspaceMember } from "@/lib/types/workspaces"

const member: WorkspaceMember = {
  userId: "user-1",
  email: "member@example.test",
  role: "Member",
  joinedAt: "2026-08-24T12:00:00Z",
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
        members={[{ ...member, role: "Editor" }]}
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
    expect(memberRoleInput.value).toBe("Editor")
    expect(
      consoleError.mock.calls.some((call) =>
        call.some((value) =>
          String(value).includes("changing the default value state"),
        ),
      ),
    ).toBe(false)
  })
})
