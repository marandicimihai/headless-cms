"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import {
  ArrowLeft,
  FolderKanban,
  LayoutDashboard,
  Library,
  Sparkles,
  Settings,
} from "lucide-react"

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarTrigger,
  useSidebar,
} from "@/components/ui/sidebar"
import type { Project } from "@/lib/types/projects"

function projectIdFromPathname(pathname: string): string | null {
  const segments = pathname.split("/").filter(Boolean)
  const projectsIndex = segments.indexOf("projects")

  if (projectsIndex < 0) return null

  return segments[projectsIndex + 1] ?? null
}

export function WorkspaceSidebar({
  workspaceId,
  projects,
  canWrite,
}: {
  workspaceId: string
  projects: Project[]
  canWrite: boolean
}) {
  const pathname = usePathname()
  const { isMobile, setOpenMobile, state } = useSidebar()
  const currentProject = projects.find(
    (project) =>
      project.id.toLowerCase() === projectIdFromPathname(pathname)?.toLowerCase(),
  )
  const isIdentityHidden = !isMobile && state === "collapsed"
  const workspaceHref = `/workspaces/${workspaceId}`
  const projectsHref = `${workspaceHref}/projects`
  const closeMobileSidebar = () => setOpenMobile(false)

  if (currentProject) {
    const projectHref = `${projectsHref}/${currentProject.id}`
    const contentTypesHref = `${projectHref}/content`
    const schemaAssistantHref = `${projectHref}/schema-assistant`
    const settingsHref = `${projectHref}/manage`
    const contentTypesActive = pathname.startsWith(`${projectHref}/content`)

    return (
      <Sidebar collapsible="icon" className="md:top-16 md:bottom-0 md:h-auto">
        <SidebarHeader className="group-data-[collapsible=icon]:pb-0">
          <div className="relative flex h-12 min-w-0 items-center gap-1 overflow-hidden transition-[height,gap] duration-200 ease-linear motion-reduce:transition-none group-data-[collapsible=icon]:h-8 group-data-[collapsible=icon]:gap-0">
            <SidebarMenu className="relative z-0 min-w-0 flex-1 overflow-hidden transition-opacity duration-200 ease-linear motion-reduce:transition-none group-data-[collapsible=icon]:pointer-events-none group-data-[collapsible=icon]:opacity-0">
              <SidebarMenuItem>
                <SidebarMenuButton
                  render={
                    <Link
                      aria-hidden={isIdentityHidden || undefined}
                      href={projectHref}
                      tabIndex={isIdentityHidden ? -1 : undefined}
                      onClick={closeMobileSidebar}
                    />
                  }
                  size="lg"
                >
                  <div className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                    <FolderKanban className="size-4" />
                  </div>
                  <div className="grid flex-1 text-left text-sm leading-tight">
                    <span className="truncate font-semibold">{currentProject.name}</span>
                    <span className="truncate text-xs">Project</span>
                  </div>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
            <SidebarTrigger className="relative z-10 size-8 shrink-0 bg-sidebar" />
          </div>
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>Project</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu className="group-data-[collapsible=icon]:gap-2">
                <SidebarMenuItem>
                  <SidebarMenuButton
                    isActive={pathname === projectHref}
                    render={<Link href={projectHref} onClick={closeMobileSidebar} />}
                  >
                    <LayoutDashboard />
                    <span>Overview</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton
                    isActive={contentTypesActive}
                    render={<Link href={contentTypesHref} onClick={closeMobileSidebar} />}
                  >
                    <Library />
                    <span>Content types</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                {canWrite ? (
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      isActive={pathname === schemaAssistantHref}
                      render={<Link href={schemaAssistantHref} onClick={closeMobileSidebar} />}
                    >
                      <Sparkles />
                      <span>Schema assistant</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ) : null}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>

        <SidebarFooter>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton
                render={<Link href={projectsHref} onClick={closeMobileSidebar} />}
              >
                <ArrowLeft />
                <span>All projects</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton
                isActive={pathname === settingsHref || pathname.startsWith(`${settingsHref}/`)}
                render={<Link href={settingsHref} onClick={closeMobileSidebar} />}
              >
                <Settings />
                <span>Settings</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarFooter>
      </Sidebar>
    )
  }

  return null
}
