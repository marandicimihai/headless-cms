import type { ReactNode } from "react"

import { ContentSidebar } from "./content-sidebar"
import { listContentTypes } from "@/lib/api/content"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function ProjectContentLayout({
  children,
  params,
}: {
  children: ReactNode
  params: Promise<{ workspaceId: string; projectId: string }>
}) {
  const { workspaceId, projectId } = await params
  const [workspaceResult, contentTypesResult] = await Promise.all([
    listMyWorkspaces(),
    listContentTypes(workspaceId, projectId),
  ])
  const workspace = workspaceResult.ok
    ? workspaceResult.data.find(
        (item) => item.id.toLowerCase() === workspaceId.toLowerCase(),
      )
    : undefined
  const canWrite =
    workspace?.currentRole === "owner" || workspace?.currentRole === "editor"

  return (
    <div className="flex h-full min-h-0 flex-1">
      <div className="sticky top-0 h-full shrink-0 self-start">
        <ContentSidebar
          workspaceId={workspaceId}
          projectId={projectId}
          contentTypes={contentTypesResult.ok ? contentTypesResult.data : []}
          contentTypesError={
            contentTypesResult.ok ? undefined : contentTypesResult.error.detail
          }
          canWrite={canWrite}
        />
      </div>
      <div className="min-h-0 min-w-0 flex-1 overflow-y-auto overscroll-y-contain">
        {children}
      </div>
    </div>
  )
}
