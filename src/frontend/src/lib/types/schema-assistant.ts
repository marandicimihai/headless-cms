import type { ContentFieldType, ContentTypeInput } from "./content"

export type SchemaAssistantMessage = {
  role: "user" | "assistant"
  content: string
  proposal?: SchemaAssistantContentType[]
}

export type SchemaAssistantField = {
  key: string
  type: ContentFieldType
  required: boolean
  defaultValue: string | number | boolean | null
}

export type SchemaAssistantContentType = {
  key: string
  fields: SchemaAssistantField[]
}

export type SchemaAssistantResponse = {
  reply: string
  contentTypes: SchemaAssistantContentType[]
  existingKeyConflicts: string[]
}

export type CreatedContentTypesResponse = {
  contentTypes: Array<{
    id: string
    projectId: string
    key: string
    createdAt: string
    updatedAt: string
    fields: Array<{
      key: string
      type: ContentFieldType
      required: boolean
      position: number
      settings: Record<string, unknown>
    }>
  }>
}

export function toContentTypeInputs(
  types: SchemaAssistantContentType[],
): ContentTypeInput[] {
  return types.map((type) => ({
    key: type.key,
    fields: type.fields.map((field) => ({
      key: field.key,
      type: field.type,
      required: field.required,
      settings: field.defaultValue === null ? {} : { default: field.defaultValue },
    })),
  }))
}
