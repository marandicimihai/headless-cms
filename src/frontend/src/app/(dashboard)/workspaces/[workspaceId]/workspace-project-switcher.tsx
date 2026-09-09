"use client"

import Link from "next/link"
import { useEffect, useMemo, useState } from "react"
import { usePathname, useRouter } from "next/navigation"
import {
  Check,
  ChevronDown,
  FolderKanban,
  Plus,
  Search,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
import { SidebarTrigger } from "@/components/ui/sidebar"
import { setCurrentWorkspaceCookie } from "@/lib/current-workspace-client"
import type { Project } from "@/lib/types/projects"
import type { WorkspaceSummary } from "@/lib/types/workspaces"
import { cn } from "@/lib/utils"

function currentProjectId(pathname: string): string | null {
  const segments = pathname.split("/").filter(Boolean)
  const projectsIndex = segments.indexOf("projects")

  if (projectsIndex < 0) return null

  return segments[projectsIndex + 1] ?? null
}

function matchesQuery(value: string, query: string) {
  return value.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase())
}

export function WorkspaceProjectSwitcher({
  workspace,
  workspaces,
  projects,
  projectsError,
}: {
  workspace: WorkspaceSummary
  workspaces: WorkspaceSummary[]
  projects: Project[]
  projectsError?: string
}) {
  const pathname = usePathname()
  const router = useRouter()
  const [open, setOpen] = useState(false)
  const [workspaceQuery, setWorkspaceQuery] = useState("")
  const [projectQuery, setProjectQuery] = useState("")
  const selectedProject = projects.find(
    (project) =>
      project.id.toLowerCase() === currentProjectId(pathname)?.toLowerCase(),
  )
  const canCreateProjects =
    workspace.currentRole === "owner" || workspace.currentRole === "editor"
  const workspaceHref = `/workspaces/${workspace.id}`
  const projectsHref = `${workspaceHref}/projects`
  const filteredWorkspaces = useMemo(
    () => workspaces.filter((item) => matchesQuery(item.name, workspaceQuery)),
    [workspaceQuery, workspaces],
  )
  const filteredProjects = useMemo(
    () => projects.filter((item) => matchesQuery(item.name, projectQuery)),
    [projectQuery, projects],
  )

  useEffect(() => {
    setCurrentWorkspaceCookie(workspace.id)
  }, [workspace.id])

  function selectWorkspace(workspaceId: string) {
    setCurrentWorkspaceCookie(workspaceId)
    setOpen(false)
    router.push(`/workspaces/${workspaceId}`)
  }

  function selectProject(projectId: string) {
    setCurrentWorkspaceCookie(workspace.id)
    setOpen(false)
    router.push(`${projectsHref}/${projectId}`)
  }

  const selectorLabel = selectedProject
    ? `${workspace.name} / ${selectedProject.name}`
    : workspace.name

  return (
    <div className="flex min-w-0 items-center gap-1">
      {selectedProject ? <SidebarTrigger className="md:hidden" /> : null}
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger
          render={
            <Button
              aria-label="Switch workspace or project"
              className="h-9 min-w-0 max-w-full justify-start px-2 text-left"
              variant="ghost"
            />
          }
        >
          <span className="flex size-6 shrink-0 items-center justify-center rounded-md bg-muted text-xs font-semibold text-muted-foreground">
            {workspace.name.slice(0, 2).toUpperCase()}
          </span>
          <span className="truncate text-sm font-medium">{selectorLabel}</span>
          <ChevronDown className="size-4 shrink-0 text-muted-foreground" />
        </PopoverTrigger>
        <PopoverContent
          align="start"
          className="w-[min(42rem,calc(100vw-2rem))] max-w-none gap-0 overflow-hidden p-0"
        >
          <div className="grid sm:grid-cols-2">
            <section aria-labelledby="workspace-switcher-heading" className="min-w-0 border-b p-3 sm:border-r sm:border-b-0">
              <div className="mb-3 space-y-2">
                <p id="workspace-switcher-heading" className="text-sm font-medium">
                  Workspaces
                </p>
                <label className="relative block">
                  <span className="sr-only">Find workspace</span>
                  <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="h-8 pl-8"
                    placeholder="Find workspace..."
                    type="search"
                    value={workspaceQuery}
                    onChange={(event) => setWorkspaceQuery(event.target.value)}
                  />
                </label>
              </div>
              <div className="max-h-64 space-y-1 overflow-y-auto">
                {filteredWorkspaces.map((item) => {
                  const isCurrent = item.id === workspace.id

                  return (
                    <button
                      key={item.id}
                      aria-current={isCurrent ? "page" : undefined}
                      className={cn(
                        "flex w-full items-center gap-2 rounded-md px-2 py-2 text-left outline-none hover:bg-muted focus-visible:bg-muted",
                        isCurrent && "bg-muted",
                      )}
                      type="button"
                      onClick={() => selectWorkspace(item.id)}
                    >
                      <span className="flex size-7 shrink-0 items-center justify-center rounded-md bg-background text-xs font-semibold text-muted-foreground ring-1 ring-border">
                        {item.name.slice(0, 2).toUpperCase()}
                      </span>
                      <span className="min-w-0 flex-1 truncate text-sm font-medium">
                        {item.name}
                      </span>
                      {isCurrent ? <Check className="size-4 shrink-0" /> : null}
                    </button>
                  )
                })}
                {filteredWorkspaces.length === 0 ? (
                  <p className="px-2 py-3 text-sm text-muted-foreground">
                    No workspaces found.
                  </p>
                ) : null}
              </div>
              <Button
                className="mt-3 w-full justify-start"
                nativeButton={false}
                render={<Link href="/workspaces/new" />}
                size="sm"
                variant="ghost"
              >
                <Plus />
                Create workspace
              </Button>
            </section>

            <section aria-labelledby="project-switcher-heading" className="min-w-0 p-3">
              <div className="mb-3 space-y-2">
                <p id="project-switcher-heading" className="text-sm font-medium">
                  Projects in {workspace.name}
                </p>
                <label className="relative block">
                  <span className="sr-only">Find project</span>
                  <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    className="h-8 pl-8"
                    placeholder="Find project..."
                    type="search"
                    value={projectQuery}
                    onChange={(event) => setProjectQuery(event.target.value)}
                  />
                </label>
              </div>
              <div className="max-h-64 space-y-1 overflow-y-auto">
                {projectsError ? (
                  <p className="px-2 py-3 text-sm text-muted-foreground">
                    {projectsError}
                  </p>
                ) : filteredProjects.length ? (
                  filteredProjects.map((project) => {
                    const isCurrent = project.id === selectedProject?.id

                    return (
                      <button
                        key={project.id}
                        aria-current={isCurrent ? "page" : undefined}
                        className={cn(
                          "flex w-full items-center gap-2 rounded-md px-2 py-2 text-left outline-none hover:bg-muted focus-visible:bg-muted",
                          isCurrent && "bg-muted",
                        )}
                        type="button"
                        onClick={() => selectProject(project.id)}
                      >
                        <span className="flex size-7 shrink-0 items-center justify-center rounded-md bg-background text-muted-foreground ring-1 ring-border">
                          <FolderKanban className="size-3.5" />
                        </span>
                        <span className="min-w-0 flex-1 truncate text-sm font-medium">
                          {project.name}
                        </span>
                        {isCurrent ? <Check className="size-4 shrink-0" /> : null}
                      </button>
                    )
                  })
                ) : (
                  <p className="px-2 py-3 text-sm text-muted-foreground">
                    No projects yet.
                  </p>
                )}
              </div>
              {canCreateProjects ? (
                <Button
                  className="mt-3 w-full justify-start"
                  nativeButton={false}
                  render={<Link href={`${projectsHref}/new`} />}
                  size="sm"
                  variant="ghost"
                >
                  <Plus />
                  Create project
                </Button>
              ) : null}
            </section>
          </div>
        </PopoverContent>
      </Popover>
    </div>
  )
}
