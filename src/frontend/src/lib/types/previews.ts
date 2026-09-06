import type { ContentEntryStatus } from "./content"
import type { WorkspaceRole } from "./workspaces"

export type WorkspacePreview = {
  name: string
  currentRole: WorkspaceRole
  projectCount: number
  entryCount: number
  publishedEntryCount: number
  draftEntryCount: number
  memberCount: number
  pendingInvitationCount: number | null
  projects: {
    id: string
    name: string
    contentTypeCount: number
    entryCount: number
    lastContentUpdatedAt: string | null
  }[]
}

export type ProjectPreview = {
  name: string
  currentRole: WorkspaceRole
  contentTypeCount: number
  publishedEntryCount: number
  draftEntryCount: number
  recentEntries: {
    id: string
    contentTypeKey: string
    status: ContentEntryStatus
    updatedAt: string
  }[]
}
