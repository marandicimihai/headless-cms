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
    <div className="flex min-h-0 flex-1">
      <ContentSidebar
        workspaceId={workspaceId}
        projectId={projectId}
        contentTypes={contentTypesResult.ok ? contentTypesResult.data : []}
        contentTypesError={
          contentTypesResult.ok ? undefined : contentTypesResult.error.detail
        }
        canWrite={canWrite}
      />
      <div className="min-w-0 flex-1">
        {children}
      </div>
    </div>
  )
}
