"use client"

import { useMemo, useState } from "react"
import { Copy, ExternalLink } from "lucide-react"
import { toast } from "sonner"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import {
  curlExample, groupOperations, mediaExample, resolveSchema, schemaType,
  type Documentation, type Operation, type Schema,
} from "@/lib/documentation"

const groupId = (tag: string) => `group-${tag.toLowerCase().replaceAll(" ", "-")}`

function CodeBlock({ label, value }: { label: string; value: string }) {
  async function copy() {
    try {
      await navigator.clipboard.writeText(value)
      toast.success(`${label} copied`)
    } catch {
      toast.error("Unable to copy. Select and copy the example manually.")
    }
  }
  return (
    <div className="min-w-0 space-y-2">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">{label}</span>
        <Button size="sm" variant="ghost" aria-label={`Copy ${label}`} onClick={copy}>
          <Copy aria-hidden="true" /> Copy
        </Button>
      </div>
      <pre className="max-w-full overflow-x-auto rounded-xl border p-4 text-xs leading-relaxed"><code>{value}</code></pre>
    </div>
  )
}

type SchemaRow = { name: string; value: Schema; required: boolean }

type QueryParameterDisplay = {
  type?: string
  allowedValues?: string
  defaultOrRules?: string
}

// A query key can be dynamic (for example filter[field][operator]), and some
// FastEndpoints/NSwag parameter schemas omit validation metadata. Keep the
// wire-contract values explicit so request tables never hide usable inputs.
const queryParameterDisplay: Record<string, Record<string, QueryParameterDisplay>> = {
  "/api/workspaces/{workspaceId}/projects/{projectId}/content-types/{contentTypeKey}/entries": {
    "filter[field][operator]": { type: "string", allowedValues: "text: eq, contains · number/date: eq, gt, gte, lt, lte · boolean: eq", defaultOrRules: "No default" },
    sort: { type: "string", allowedValues: "Content field or $id, $status, $createdAt, $updatedAt", defaultOrRules: "Default: -$updatedAt" },
    status: { type: "string", allowedValues: "draft, published", defaultOrRules: "No default" },
    page: { type: "integer", allowedValues: "1+", defaultOrRules: "Default: 1" },
    pageSize: { type: "integer", allowedValues: "1–100", defaultOrRules: "Default: 25" },
  },
  "/api/workspaces/{workspaceId}/search": {
    query: { type: "string", allowedValues: "2–100 characters", defaultOrRules: "No default" },
    limit: { type: "integer", allowedValues: "1–10", defaultOrRules: "Default: 5" },
  },
  "/api/workspaces/{workspaceId}/invitations": {
    page: { type: "integer", allowedValues: "1+", defaultOrRules: "Default: 1" },
    pageSize: { type: "integer", allowedValues: "1–100", defaultOrRules: "Default: 20" },
    status: { type: "string", allowedValues: "pending, accepted, expired, revoked", defaultOrRules: "No default" },
  },
  "/api/workspaces/{workspaceId}/members": {
    page: { type: "integer", allowedValues: "1+", defaultOrRules: "Default: 1" },
    pageSize: { type: "integer", allowedValues: "1–100", defaultOrRules: "Default: 20" },
  },
  "/api/workspaces": {
    page: { type: "integer", allowedValues: "1+", defaultOrRules: "Default: 1" },
    pageSize: { type: "integer", allowedValues: "1–100", defaultOrRules: "Default: 20" },
  },
}

function schemaRows(value: Schema, schemas: Documentation["schemas"], prefix = "", depth = 0): SchemaRow[] {
  if (depth > 8) return []
  const resolved = resolveSchema(value, schemas)
  if (resolved.type === "array" && resolved.items) return schemaRows(resolved.items, schemas, `${prefix}[]`, depth + 1)
  return Object.entries(resolved.properties ?? {}).flatMap(([name, child]) => {
    const path = prefix ? `${prefix}.${name}` : name
    return [
      { name: path, value: resolveSchema(child, schemas), required: resolved.required?.includes(name) ?? false },
      ...schemaRows(child, schemas, path, depth + 1),
    ]
  })
}

function allowedValues(value: Schema, schemas: Documentation["schemas"]): string | undefined {
  const resolved = resolveSchema(value, schemas)
  const alternatives = resolved.oneOf ?? resolved.anyOf
  if (resolved.enum) return resolved.enum.map((item) => JSON.stringify(item)).join(", ")
  if (alternatives) {
    const values: string[] = alternatives
      .map((item) => allowedValues(item, schemas))
      .filter((item): item is string => Boolean(item))
    return values.length ? values.join(", ") : undefined
  }
  return undefined
}

function constraints(value: Schema, schemas: Documentation["schemas"]) {
  const resolved = resolveSchema(value, schemas)
  return [
    resolved.default !== undefined ? "Default: " + JSON.stringify(resolved.default) : undefined,
    resolved.minimum !== undefined ? "Minimum: " + resolved.minimum : undefined,
    resolved.maximum !== undefined ? "Maximum: " + resolved.maximum : undefined,
    resolved.minLength !== undefined ? "Minimum length: " + resolved.minLength : undefined,
    resolved.maxLength !== undefined ? "Maximum length: " + resolved.maxLength : undefined,
    resolved.pattern ? "Pattern: " + resolved.pattern : undefined,
  ].filter((item): item is string => Boolean(item))
}

function brief(value: string | undefined) {
  if (!value) return "—"
  const sentence = value.replaceAll("\n", " ").match(/^.*?[.!?](?:\s|$)/)?.[0] ?? value
  return sentence.length > 120 ? `${sentence.slice(0, 117).trimEnd()}…` : sentence
}

function SchemaTable({ value, schemas }: { value: Schema; schemas: Documentation["schemas"] }) {
  const rows = schemaRows(value, schemas)
  return (
    <div className="space-y-2">
      <p className="text-xs text-muted-foreground">Schema: {schemaType(value, schemas)}</p>
      {rows.length > 0 && (
        <div className="overflow-x-auto rounded-xl border">
          <Table>
            <TableHeader className="bg-muted"><TableRow><TableHead>Field</TableHead><TableHead>Required</TableHead><TableHead>Type</TableHead><TableHead>Allowed values</TableHead><TableHead>Default / rules</TableHead><TableHead>Description</TableHead></TableRow></TableHeader>
            <TableBody>
              {rows.map(({ name, value, required }) => (
                <TableRow key={name}>
                  <TableCell className="align-top"><code className="text-xs">{name}</code></TableCell>
                  <TableCell className="align-top text-xs">{required ? "Yes" : "No"}</TableCell>
                  <TableCell className="max-w-64 whitespace-normal align-top text-xs">{schemaType(value, schemas)}</TableCell>
                  <TableCell className="max-w-64 whitespace-normal align-top text-xs">{allowedValues(value, schemas) ?? "—"}</TableCell>
                  <TableCell className="min-w-44 whitespace-normal align-top text-xs">{constraints(value, schemas).join(" · ") || "—"}</TableCell>
                  <TableCell className="min-w-48 whitespace-normal align-top text-xs">{brief(value.description)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  )
}

function EndpointReference({ operation, schemas, baseUrl }: {
  operation: Operation; schemas: Documentation["schemas"]; baseUrl: string
}) {
  const [access] = (operation.description ?? "").split("\n\n")
  const request = operation.requestBody?.content["application/json"]
  const requestExample = request && mediaExample(request, schemas)
  const responses = Object.entries(operation.responses).sort(([a], [b]) => a.localeCompare(b))
  return (
    <article id={operation.operationId} className="min-w-0 scroll-mt-6 space-y-5 border-t pt-8 first:border-t-0 first:pt-0" aria-labelledby={`${operation.operationId}-title`}>
      <div className="space-y-3">
        <h3 id={`${operation.operationId}-title`} className="font-semibold">
          <a href={`#${operation.operationId}`} className="underline-offset-4 hover:underline">{operation.summary}</a>
        </h3>
        <div className="overflow-x-auto rounded-xl border">
          <Table>
            <TableHeader className="bg-muted"><TableRow><TableHead>Method</TableHead><TableHead>Route</TableHead><TableHead>Access</TableHead></TableRow></TableHeader>
            <TableBody><TableRow>
              <TableCell><Badge variant="outline">{operation.method}</Badge></TableCell>
              <TableCell className="min-w-80"><code className="break-all text-xs">{operation.path}</code></TableCell>
              <TableCell className="min-w-56 text-xs">{access.replace(/^Access:\s*/i, "") || "—"}</TableCell>
            </TableRow></TableBody>
          </Table>
        </div>
      </div>
      {!!operation.parameters?.length && (
        <section className="space-y-2" aria-label={`${operation.summary} parameters`}>
          <h4 className="text-sm font-semibold">Request parameters</h4>
          <div className="overflow-x-auto rounded-xl border">
            <Table>
              <TableHeader className="bg-muted"><TableRow><TableHead>Name</TableHead><TableHead>In</TableHead><TableHead>Required</TableHead><TableHead>Type</TableHead><TableHead>Allowed values</TableHead><TableHead>Default / rules</TableHead><TableHead>Description</TableHead></TableRow></TableHeader>
              <TableBody>{operation.parameters.map((parameter) => (
                <TableRow key={`${parameter.in}-${parameter.name}`}>
                  <TableCell className="align-top"><code className="text-xs">{parameter.name}</code></TableCell>
                  <TableCell className="align-top text-xs">{parameter.in}</TableCell>
                  <TableCell className="align-top text-xs">{parameter.required ? "Yes" : "No"}</TableCell>
                  <TableCell className="max-w-56 whitespace-normal align-top text-xs">{queryParameterDisplay[operation.path]?.[parameter.name]?.type ?? schemaType(parameter.schema ?? {}, schemas)}</TableCell>
                  <TableCell className="max-w-64 whitespace-normal align-top text-xs">{queryParameterDisplay[operation.path]?.[parameter.name]?.allowedValues ?? allowedValues(parameter.schema ?? {}, schemas) ?? "—"}</TableCell>
                  <TableCell className="min-w-44 whitespace-normal align-top text-xs">{(queryParameterDisplay[operation.path]?.[parameter.name]?.defaultOrRules ?? constraints(parameter.schema ?? {}, schemas).join(" · ")) || "—"}</TableCell>
                  <TableCell className="min-w-48 whitespace-normal align-top text-xs">{brief(parameter.description)}</TableCell>
                </TableRow>
              ))}</TableBody>
            </Table>
          </div>
        </section>
      )}
      {request?.schema && <section className="space-y-2"><h4 className="text-sm font-semibold">Request body</h4><SchemaTable value={request.schema} schemas={schemas} /></section>}
      <Tabs defaultValue="curl">
        <TabsList aria-label={`${operation.summary} request examples`}>
          <TabsTrigger value="curl">cURL</TabsTrigger>
          {requestExample !== undefined && <TabsTrigger value="json">JSON body</TabsTrigger>}
        </TabsList>
        <TabsContent value="curl"><CodeBlock label={`${operation.summary} cURL`} value={curlExample(operation, schemas, baseUrl)} /></TabsContent>
        {requestExample !== undefined && <TabsContent value="json"><CodeBlock label={`${operation.summary} JSON body`} value={JSON.stringify(requestExample, null, 2)} /></TabsContent>}
      </Tabs>
      <section className="space-y-2" aria-label={`${operation.summary} responses`}>
        <h4 className="text-sm font-semibold">Responses</h4>
        <Tabs defaultValue={responses[0]?.[0]}>
          <div className="overflow-x-auto"><TabsList aria-label={`${operation.summary} response status`}>
            {responses.map(([status]) => <TabsTrigger key={status} value={status}>{status}</TabsTrigger>)}
          </TabsList></div>
          {responses.map(([status, response]) => (
            <TabsContent key={status} value={status} className="space-y-3">
              <p className="text-muted-foreground">{response.description}</p>
              {!response.content && <p className="text-xs text-muted-foreground">No response body.</p>}
              {Object.entries(response.content ?? {}).map(([contentType, body]) => {
                const example = mediaExample(body, schemas)
                return <div key={contentType} className="space-y-3">
                  <p className="text-xs text-muted-foreground">{contentType}</p>
                  {body.schema && <SchemaTable value={body.schema} schemas={schemas} />}
                  {example !== undefined && <CodeBlock label={`${operation.summary} ${status} response`} value={typeof example === "string" ? example : JSON.stringify(example, null, 2)} />}
                </div>
              })}
            </TabsContent>
          ))}
        </Tabs>
      </section>
    </article>
  )
}

export function ApiReference({ documentation, baseUrl }: { documentation: Documentation; baseUrl: string }) {
  const [search, setSearch] = useState("")
  const groups = useMemo(() => groupOperations(documentation.operations, search), [documentation.operations, search])
  const count = groups.reduce((total, [, operations]) => total + operations.length, 0)
  return (
    <div className="grid min-w-0 gap-8 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-12">
      <aside className="space-y-4 lg:sticky lg:top-6 lg:max-h-[calc(100svh-8rem)] lg:self-start lg:overflow-y-auto lg:pr-3">
        <div className="space-y-2">
          <Label htmlFor="documentation-search">Search endpoints</Label>
          <Input id="documentation-search" type="search" placeholder="Method, route, or keyword…" value={search} onChange={(event) => setSearch(event.target.value)} />
          <p className="text-xs text-muted-foreground" role="status">{count} {count === 1 ? "endpoint" : "endpoints"}</p>
        </div>
        <nav aria-label="Documentation sections" className="space-y-5 text-sm">
          <a className="font-medium underline-offset-4 hover:underline" href="#getting-started">Getting started</a>
          {groups.map(([tag, operations]) => (
            <div key={tag} className="space-y-2">
              <a href={`#${groupId(tag)}`} className="font-medium underline-offset-4 hover:underline">{tag}</a>
              <ul className="hidden space-y-3 border-l pl-3 lg:block">{operations.map((operation) => (
                <li key={operation.operationId}><a className="block text-xs text-muted-foreground underline-offset-4 hover:underline" href={`#${operation.operationId}`}>{operation.summary}</a></li>
              ))}</ul>
            </div>
          ))}
        </nav>
      </aside>
      <div className="min-w-0 space-y-12">
        <section id="getting-started" className="scroll-mt-6 space-y-5 text-sm">
          <h2 className="font-semibold">Getting started</h2>
          <p className="text-muted-foreground">Use the API to manage your workspaces, define content types, and read or publish entries. This reference covers every product endpoint; each operation lists the access it requires.</p>
          <p>API origin: <code className="break-all">{baseUrl}</code>. Routes below include <code>/api</code>.</p>
          <p className="text-muted-foreground">Examples use synthetic data. Replace <code>&lt;workspaceId&gt;</code> and other route placeholders with your own IDs. Run the login example first; cURL saves and reuses the session in <code>cookies.txt</code>.</p>
          <Button variant="link" nativeButton={false} render={<a href="https://go.postman.co/collection/30832597-e019a46d-66de-4a93-90a3-53a14453bd92" target="_blank" rel="noreferrer" />}>
            Explore requests in Postman <ExternalLink aria-hidden="true" />
          </Button>
          <h3 className="font-semibold">Authentication and access</h3>
          <p className="text-muted-foreground">Registration is invitation-only. Login and invitation registration set the HttpOnly <code>cms_session</code> cookie; the API never returns its secret in JSON. Keep your client’s cookie jar enabled. Sessions expire after 30 days of inactivity or one year, and logout revokes only the current session.</p>
          <p className="text-muted-foreground">Workspace Owners and Editors can write content; Members can read it, including drafts. PlatformAdmin is a separate platform role and does not grant workspace membership. Public, anonymous content delivery and bearer API keys are not supported.</p>
          <h3 className="font-semibold">Pagination and filtering</h3>
          <p className="text-muted-foreground">Paginated lists return <code>items</code>, <code>page</code>, <code>pageSize</code>, and <code>total</code>. Content entries default to 25 per page; workspace, member, and invitation lists default to 20. The maximum is 100. Other list endpoints return arrays.</p>
          <p className="text-muted-foreground">Entry filters use <code>filter[field][operator]</code>, such as <code>filter[title][contains]=Hello</code>. Combine filters with AND. Use <code>status=published</code> to exclude drafts and <code>sort=-$updatedAt</code> for most recently updated first. Encode brackets and special characters when building query strings.</p>
          <h3 className="font-semibold">Content fields</h3>
          <p className="text-muted-foreground">Fields support <code>text</code>, <code>number</code>, and <code>boolean</code>. Keys are immutable identifiers; the ordered field list defines the schema. Entry <code>data</code> must match that schema. Optional fields may use a type-correct <code>settings.default</code>; required fields cannot define defaults. Updating an entry replaces its complete data and status.</p>
          <h3 className="font-semibold">Errors</h3>
          <p className="text-muted-foreground">Validation failures use problem details with an <code>errors</code> array. Authentication and business errors include <code>type</code>, <code>title</code>, <code>status</code>, <code>code</code>, and <code>detail</code>. Some 403 and 404 responses are empty; a duplicate content-type key returns plain text with 409. Check the response status and content type before parsing JSON.</p>
        </section>
        {groups.length === 0 && <p className="text-sm text-muted-foreground">No endpoints match “{search}”. Try a method, route, or a different keyword.</p>}
        {groups.map(([tag, operations]) => (
          <section key={tag} id={groupId(tag)} className="min-w-0 scroll-mt-6 space-y-10" aria-labelledby={`${groupId(tag)}-title`}>
            <h2 id={`${groupId(tag)}-title`} className="border-b pb-3 font-semibold">{tag}</h2>
            <div className="space-y-10">{operations.map((operation) => <EndpointReference key={operation.operationId} operation={operation} schemas={documentation.schemas} baseUrl={baseUrl} />)}</div>
          </section>
        ))}
      </div>
    </div>
  )
}
