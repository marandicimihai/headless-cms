"use client"

import { useRouter } from "next/navigation"
import Link from "next/link"
import { Building2, ChevronRight, Plus } from "lucide-react"

import { Button } from "@/components/ui/button"
import { setCurrentWorkspaceCookie } from "@/lib/current-workspace-client"
import type { WorkspaceRole, WorkspaceSummary } from "@/lib/types/workspaces"

const roleLabels: Record<WorkspaceRole, string> = {
  owner: "Owner",
  editor: "Editor",
  member: "Read only",
}

export function WorkspaceChooser({
  workspaces,
}: {
  workspaces: WorkspaceSummary[]
}) {
  const router = useRouter()

  function selectWorkspace(workspaceId: string) {
    setCurrentWorkspaceCookie(workspaceId)
    router.push(`/workspaces/${workspaceId}`)
  }

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Choose a workspace
          </h1>
          <p className="text-sm text-muted-foreground">
            Select a workspace to continue.
          </p>
        </div>
        <Button
          nativeButton={false}
          render={<Link href="/workspaces/new" />}
        >
          <Plus />
          Create workspace
        </Button>
      </div>

      <ul className="max-w-2xl overflow-hidden rounded-xl border divide-y">
        {workspaces.map((workspace) => (
          <li key={workspace.id}>
            <button
              className="flex w-full items-center gap-3 px-4 py-3 text-left outline-none hover:bg-muted focus-visible:bg-muted"
              type="button"
              onClick={() => selectWorkspace(workspace.id)}
            >
              <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
                <Building2 className="size-4" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium">
                  {workspace.name}
                </span>
                {workspace.currentRole ? (
                  <span className="block text-xs text-muted-foreground">
                    {roleLabels[workspace.currentRole]}
                  </span>
                ) : null}
              </span>
              <ChevronRight className="size-4 text-muted-foreground" />
            </button>
          </li>
        ))}
      </ul>
    </main>
  )
}
