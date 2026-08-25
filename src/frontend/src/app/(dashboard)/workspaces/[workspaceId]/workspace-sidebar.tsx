"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import { ArrowLeft, LayoutDashboard, Settings } from "lucide-react"

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

export function WorkspaceSidebar({
  workspaceId,
  workspaceName,
}: {
  workspaceId: string
  workspaceName: string
}) {
  const pathname = usePathname()
  const { isMobile, setOpenMobile, state } = useSidebar()
  const isWorkspaceIdentityHidden = !isMobile && state === "collapsed"
  const workspaceHref = `/workspaces/${workspaceId}`
  const manageHref = `${workspaceHref}/manage`
  const navigation = [
    { title: "Preview", href: workspaceHref, icon: LayoutDashboard },
    { title: "Manage", href: manageHref, icon: Settings },
  ]

  return (
    <Sidebar
      collapsible="icon"
      className="md:top-16 md:bottom-0 md:h-auto"
    >
      <SidebarHeader className="group-data-[collapsible=icon]:pb-0">
        <div className="relative flex h-12 min-w-0 items-center gap-1 overflow-hidden transition-[height,gap] duration-200 ease-linear motion-reduce:transition-none group-data-[collapsible=icon]:h-8 group-data-[collapsible=icon]:gap-0">
          <SidebarMenu className="relative z-0 min-w-0 flex-1 overflow-hidden transition-opacity duration-200 ease-linear motion-reduce:transition-none group-data-[collapsible=icon]:pointer-events-none group-data-[collapsible=icon]:opacity-0">
            <SidebarMenuItem>
              <SidebarMenuButton
                render={
                  <Link
                    href={workspaceHref}
                    aria-hidden={isWorkspaceIdentityHidden || undefined}
                    onClick={() => setOpenMobile(false)}
                    tabIndex={isWorkspaceIdentityHidden ? -1 : undefined}
                  />
                }
                size="lg"
              >
                <div className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                  <LayoutDashboard className="size-4" />
                </div>
                <div className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-semibold">{workspaceName}</span>
                  <span className="truncate text-xs">Workspace</span>
                </div>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
          <SidebarTrigger className="relative z-10 size-8 shrink-0 bg-sidebar" />
        </div>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>Workspace</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu className="group-data-[collapsible=icon]:gap-2">
              {navigation.map((item) => (
                <SidebarMenuItem key={item.href}>
                  <SidebarMenuButton
                    isActive={
                      item.href === workspaceHref
                        ? pathname === workspaceHref
                        : pathname === item.href ||
                          pathname.startsWith(`${item.href}/`)
                    }
                    render={
                      <Link
                        href={item.href}
                        onClick={() => setOpenMobile(false)}
                      />
                    }
                  >
                    <item.icon />
                    <span>{item.title}</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>

      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton
              render={
                <Link href="/" onClick={() => setOpenMobile(false)} />
              }
            >
              <ArrowLeft />
              <span>All workspaces</span>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
    </Sidebar>
  )
}
