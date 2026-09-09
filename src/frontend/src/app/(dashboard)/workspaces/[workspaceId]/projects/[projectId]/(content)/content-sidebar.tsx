"use client"

import Link from "next/link"
import { Database, Plus } from "lucide-react"
import { usePathname } from "next/navigation"

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar"
import type { ContentType } from "@/lib/types/content"

export function ContentSidebar({
  workspaceId,
  projectId,
  contentTypes,
  contentTypesError,
  canWrite,
}: {
  workspaceId: string
  projectId: string
  contentTypes: ContentType[]
  contentTypesError?: string
  canWrite: boolean
}) {
  const pathname = usePathname()
  const projectHref = `/workspaces/${workspaceId}/projects/${projectId}`
  const contentTypesHref = `${projectHref}/content-types`
  const createContentTypeHref = `${contentTypesHref}/new`

  return (
    <Sidebar
      aria-label="Content types"
      collapsible="none"
      className="shrink-0 border-r"
    >
      <SidebarHeader className="border-b">
        <div className="flex items-center gap-2 px-2 py-1">
          <Database className="size-4 shrink-0" />
          <span className="truncate text-sm font-semibold">Content types</span>
        </div>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            {contentTypesError ? (
              <p className="px-2 py-2 text-xs text-destructive">
                Unable to load content types: {contentTypesError}
              </p>
            ) : contentTypes.length ? (
              <SidebarMenu>
                {contentTypes.map((contentType) => {
                  const href = `${contentTypesHref}/${encodeURIComponent(contentType.key)}`
                  const isActive =
                    pathname === href || pathname.startsWith(`${href}/`)

                  return (
                    <SidebarMenuItem key={contentType.id}>
                      <SidebarMenuButton
                        isActive={isActive}
                        render={<Link href={href} />}
                      >
                        <span>{contentType.key}</span>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  )
                })}
              </SidebarMenu>
            ) : (
              <p className="px-2 py-2 text-xs text-muted-foreground">
                No content types yet.
              </p>
            )}
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>

      {canWrite ? (
        <SidebarFooter>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton render={<Link href={createContentTypeHref} />}>
                <Plus />
                <span>Create content type</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarFooter>
      ) : null}
    </Sidebar>
  )
}
