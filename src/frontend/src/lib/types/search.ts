import type { ContentEntryStatus } from "./content"

export type SearchGroup<T> = {
  items: T[]
  total: number
}

export type SearchProject = {
  id: string
  name: string
}

export type SearchContentType = {
  id: string
  key: string
  projectId: string
  projectName: string
}

export type SearchEntry = {
  id: string
  status: ContentEntryStatus
  contentTypeKey: string
  projectId: string
  projectName: string
  matchedFieldKey: string
  snippet: string
}

export type WorkspaceSearchResults = {
  projects: SearchGroup<SearchProject>
  contentTypes: SearchGroup<SearchContentType>
  entries: SearchGroup<SearchEntry>
}
