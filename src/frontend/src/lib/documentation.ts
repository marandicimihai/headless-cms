import { z } from "zod"

export type Schema = {
  $ref?: string
  type?: string
  format?: string
  description?: string
  nullable?: boolean
  enum?: unknown[]
  default?: unknown
  example?: unknown
  required?: string[]
  properties?: Record<string, Schema>
  items?: Schema
  allOf?: Schema[]
  oneOf?: Schema[]
  anyOf?: Schema[]
  minimum?: number
  maximum?: number
  minLength?: number
  maxLength?: number
  pattern?: string
}

const schema: z.ZodType<Schema> = z.lazy(() => z.object({
  $ref: z.string().optional(), type: z.string().optional(), format: z.string().optional(),
  description: z.string().optional(), nullable: z.boolean().optional(),
  enum: z.array(z.unknown()).optional(), default: z.unknown().optional(), example: z.unknown().optional(),
  required: z.array(z.string()).optional(), properties: z.record(z.string(), schema).optional(),
  items: schema.optional(), allOf: z.array(schema).optional(), oneOf: z.array(schema).optional(),
  anyOf: z.array(schema).optional(), minimum: z.number().optional(), maximum: z.number().optional(),
  minLength: z.number().optional(), maxLength: z.number().optional(), pattern: z.string().optional(),
}))

const media = z.object({
  schema: schema.optional(), example: z.unknown().optional(),
  examples: z.record(z.string(), z.object({ value: z.unknown().optional() })).optional(),
})
const content = z.record(z.string(), media)
const parameter = z.object({
  name: z.string(), in: z.string(), required: z.boolean().optional(),
  description: z.string().optional(), schema: schema.optional(), example: z.unknown().optional(),
})
const operation = z.object({
  operationId: z.string().optional(), summary: z.string().optional(), description: z.string().optional(),
  tags: z.array(z.string()).optional(), parameters: z.array(parameter).optional(),
  requestBody: z.object({ required: z.boolean().optional(), content }).optional(),
  responses: z.record(z.string(), z.object({ description: z.string(), content: content.optional() })),
})

export type Media = z.infer<typeof media>
export type Operation = Omit<z.infer<typeof operation>, "operationId" | "summary" | "tags"> & {
  operationId: string
  summary: string
  tags: string[]
  method: string
  path: string
}
export type Documentation = { operations: Operation[]; schemas: Record<string, Schema> }
export const groupOrder = ["Authentication", "Workspaces", "Members", "Invitations", "Projects", "Search", "Content types", "Content entries"]

export function parseDocumentation(value: unknown): Documentation {
  // A generated OpenAPI document may contain vendor extensions and optional
  // top-level sections. Validate only the pieces this renderer consumes.
  if (!isRecord(value) || typeof value.openapi !== "string" || !value.openapi.startsWith("3.") || !isRecord(value.paths)) {
    throw new Error("The API did not return an OpenAPI 3 document")
  }

  const schemas: Record<string, Schema> = {}
  if (isRecord(value.components) && isRecord(value.components.schemas)) {
    for (const [name, candidate] of Object.entries(value.components.schemas)) {
      const result = schema.safeParse(candidate)
      if (result.success) schemas[name] = result.data
    }
  }

  const operations: Operation[] = []
  for (const [path, pathItem] of Object.entries(value.paths)) {
    if (!isRecord(pathItem)) continue
    for (const [method, candidate] of Object.entries(pathItem)) {
      if (!["get", "post", "put", "patch", "delete", "head", "options"].includes(method)) continue
      const result = operation.safeParse(candidate)
      if (!result.success) continue
      operations.push({
        ...result.data,
        operationId: result.data.operationId ?? `${method}-${path}`,
        summary: result.data.summary ?? `${method.toUpperCase()} ${path}`,
        tags: result.data.tags?.length ? result.data.tags : [tagForPath(path)],
        method: method.toUpperCase(),
        path,
      })
    }
  }
  if (!operations.length) throw new Error("The API reference is empty")
  return { operations, schemas }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
}

function tagForPath(path: string) {
  if (path.includes("/auth/")) return "Authentication"
  if (path.includes("/search")) return "Search"
  if (path.includes("/content-types/") && path.includes("/entries")) return "Content entries"
  if (path.includes("/content-types")) return "Content types"
  if (path.includes("/projects")) return "Projects"
  if (path.includes("/invitations")) return "Invitations"
  if (path.includes("/members") || path.includes("ownership-transfer")) return "Members"
  return "Workspaces"
}

export function groupOperations(operations: Operation[], search: string) {
  const terms = search.trim().toLowerCase().split(/\s+/).filter(Boolean)
  const groups = new Map<string, Operation[]>()
  for (const operation of operations) {
    const haystack = [operation.method, operation.path, operation.summary, operation.description, ...operation.tags].join(" ").toLowerCase()
    if (!terms.every((term) => haystack.includes(term))) continue
    const tag = operation.tags[0]
    groups.set(tag, [...(groups.get(tag) ?? []), operation])
  }
  return [...groups.entries()].sort(([a], [b]) => {
    const rank = (tag: string) => groupOrder.includes(tag) ? groupOrder.indexOf(tag) : groupOrder.length
    return rank(a) - rank(b) || a.localeCompare(b)
  })
}

export function resolveSchema(value: Schema, schemas: Documentation["schemas"], seen: string[] = []): Schema {
  if (value.$ref) {
    const key = value.$ref.replace("#/components/schemas/", "")
    if (seen.includes(key)) return { type: "object", description: "Recursive object." }
    return { ...resolveSchema(schemas[key] ?? {}, schemas, [...seen, key]), ...value, $ref: undefined }
  }
  if (value.allOf) {
    const parts = value.allOf.map((part) => resolveSchema(part, schemas, seen))
    return {
      ...Object.assign({}, ...parts), ...value, allOf: undefined,
      properties: Object.assign({}, ...parts.map((part) => part.properties), value.properties),
      required: [...new Set([...parts.flatMap((part) => part.required ?? []), ...(value.required ?? [])])],
    }
  }
  return value
}

export function schemaType(value: Schema, schemas: Documentation["schemas"]): string {
  const resolved = resolveSchema(value, schemas)
  const alternatives = resolved.oneOf ?? resolved.anyOf
  const type = alternatives ? alternatives.map((item) => schemaType(item, schemas)).join(" | ")
    : resolved.enum ? resolved.enum.map((item) => JSON.stringify(item)).join(" | ")
    : resolved.type === "array" ? `${schemaType(resolved.items ?? {}, schemas)}[]`
    : resolved.format ? `${resolved.type ?? "string"} (${resolved.format})`
    : resolved.type ?? "object"
  return type + (resolved.nullable ? " | null" : "")
}

export function mediaExample(value: Media, schemas: Documentation["schemas"]): unknown {
  if (value.example !== undefined) return value.example
  if (value.examples) return Object.values(value.examples)[0]?.value
  return value.schema ? resolveSchema(value.schema, schemas).example : undefined
}

export function curlExample(operation: Operation, schemas: Documentation["schemas"], baseUrl: string) {
  const query = new URLSearchParams()
  for (const parameter of operation.parameters ?? []) {
    if (parameter.in !== "query") continue
    const definition = resolveSchema(parameter.schema ?? {}, schemas)
    const value = parameter.example ?? definition.default ?? definition.example
    if (value !== undefined || parameter.required) query.set(parameter.name, String(value ?? `YOUR_${parameter.name.toUpperCase()}`))
  }
  const path = operation.path.replace(/\{([^}]+)\}/g, "<$1>")
  const url = `${baseUrl.replace(/\/$/, "")}${path}${query.size ? `?${query}` : ""}`
  const quote = (value: string) => `'${value.replaceAll("'", `'"'"'`)}'`
  const lines = [`curl --request ${operation.method}`, `  --url ${quote(url)}`, "  --cookie cookies.txt", "  --cookie-jar cookies.txt"]
  const body = operation.requestBody?.content["application/json"]
  if (body) {
    lines.push("  --header 'Content-Type: application/json'")
    const example = mediaExample(body, schemas)
    if (example !== undefined) lines.push(`  --data ${quote(JSON.stringify(example, null, 2))}`)
  }
  return lines.join(" \\\n")
}
