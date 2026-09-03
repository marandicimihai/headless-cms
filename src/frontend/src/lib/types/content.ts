export type ContentFieldType = "text" | "number" | "boolean"

export type ContentEntryStatus = "draft" | "published"

export type ContentField = {
  key: string
  type: ContentFieldType
  required: boolean
  position: number
  settings: Record<string, unknown>
}

export type ContentType = {
  id: string
  projectId: string
  key: string
  createdAt: string
  updatedAt: string
  fields: ContentField[]
}

export type ContentFieldInput = {
  key: string
  type: ContentFieldType
  required: boolean
  settings: Record<string, unknown>
}

export type ContentTypeInput = {
  key: string
  fields: ContentFieldInput[]
}

export type ContentTypeUpdateInput = {
  fields: ContentFieldInput[]
}

export type ContentEntry = {
  id: string
  status: ContentEntryStatus
  data: Record<string, unknown>
  createdAt: string
  updatedAt: string
}

export type ContentEntriesPage = {
  items: ContentEntry[]
  total: number
  page: number
  pageSize: number
}
