"use client"

import Link from "next/link"
import { useTransition } from "react"
import {
  BookOpen,
  Boxes,
  ChevronsUpDown,
  LogOut,
  Settings,
} from "lucide-react"

import { logoutAction } from "@/app/auth/logout/actions"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { WorkspaceProjectSwitcher } from "@/app/(dashboard)/workspaces/[workspaceId]/workspace-project-switcher"
import { WorkspaceSearch } from "@/app/(dashboard)/workspace-search"
import type { Project } from "@/lib/types/projects"
import type { WorkspaceSummary } from "@/lib/types/workspaces"

export function DashboardHeader({
  email,
  role,
  workspaceContext,
}: {
  email: string
  role: string
  workspaceContext?: {
    workspace: WorkspaceSummary
    workspaces: WorkspaceSummary[]
    projects: Project[]
    projectsError?: string
  }
}) {
  const [isLoggingOut, startTransition] = useTransition()

  function handleLogout() {
    startTransition(async () => {
      await logoutAction()
    })
  }

  return (
    <header className="relative z-10 flex h-16 shrink-0 items-center gap-3 border-b px-4">
      <Link
        aria-label="Headless CMS dashboard"
        className="flex items-center gap-2.5"
        href="/"
      >
        <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
          <Boxes className="size-4" />
        </div>
        <div className="grid text-left text-sm leading-tight">
          <span className="font-semibold">Headless CMS</span>
          <span className="text-xs text-muted-foreground">Control panel</span>
        </div>
      </Link>
      {workspaceContext ? (
        <WorkspaceProjectSwitcher {...workspaceContext} />
      ) : null}
      {workspaceContext ? (
        <WorkspaceSearch
          key={workspaceContext.workspace.id}
          workspaceId={workspaceContext.workspace.id}
        />
      ) : null}
      <Button
        aria-label="Documentation"
        className="ml-auto"
        variant="ghost"
        nativeButton={false}
        render={<Link href="/documentation" />}
      >
        <BookOpen aria-hidden="true" />
        <span className="hidden sm:inline">Documentation</span>
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger
          render={
            <Button
              aria-label="Open account menu"
              className="h-12 gap-2 px-2"
              variant="ghost"
            />
          }
        >
          <Avatar className="size-8 rounded-lg">
            <AvatarFallback className="rounded-lg">{email.slice(0, 2).toUpperCase()}</AvatarFallback>
          </Avatar>
          <div className="hidden text-left leading-tight md:grid">
            <span className="max-w-48 truncate text-sm font-medium">{email}</span>
            {role === "PlatformAdmin" && (
              <span className="text-xs text-muted-foreground">Administrator</span>
            )}
          </div>
          <ChevronsUpDown className="hidden size-4 md:block" />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          {workspaceContext ? (
            <>
              <DropdownMenuGroup>
                <DropdownMenuLabel>Workspace</DropdownMenuLabel>
              <DropdownMenuItem
                render={
                  <Link
                    href={`/workspaces/${workspaceContext.workspace.id}/manage`}
                  />
                }
              >
                <Settings />
                Workspace settings
              </DropdownMenuItem>
              </DropdownMenuGroup>
              <DropdownMenuSeparator />
            </>
          ) : null}
          <DropdownMenuItem
            disabled={isLoggingOut}
            variant="destructive"
            onClick={handleLogout}
          >
            <LogOut />
            {isLoggingOut ? "Logging out..." : "Log out"}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </header>
  )
}
