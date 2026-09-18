import "server-only"

import type { ApiResult } from "@/lib/types/general"
import type {
  ContentEntriesPage,
  ContentEntryFilter,
  ContentEntry,
  ContentEntryStatus,
  ContentType,
  ContentTypeInput,
  ContentTypeUpdateInput,
} from "@/lib/types/content"

import { apiFetch } from "./fetch-utils"
import { cachedWorkspaceRead } from "./cached-read"

function contentTypesPath(workspaceId: string, projectId: string) {
  return `/api/workspaces/${encodeURIComponent(workspaceId.toLowerCase())}/projects/${encodeURIComponent(projectId.toLowerCase())}/content-types`
}

function contentTypePath(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
) {
  return `${contentTypesPath(workspaceId, projectId)}/${encodeURIComponent(contentTypeKey)}`
}

function entriesPath(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
) {
  return `${contentTypePath(workspaceId, projectId, contentTypeKey)}/entries`
}

export async function listContentTypes(workspaceId: string, projectId: string) {
  return cachedWorkspaceRead<ContentType[]>(workspaceId, contentTypesPath(workspaceId, projectId))
}

export async function getContentType(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
): Promise<ApiResult<ContentType>> {
  return cachedWorkspaceRead<ContentType>(workspaceId, contentTypePath(workspaceId, projectId, contentTypeKey))
}

export async function createContentType(
  workspaceId: string,
  projectId: string,
  input: ContentTypeInput,
): Promise<ApiResult<ContentType>> {
  return apiFetch<ContentType>(contentTypesPath(workspaceId, projectId), {
    method: "POST",
    body: JSON.stringify(input),
    cache: "no-store",
  })
}

export async function updateContentType(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  input: ContentTypeUpdateInput,
): Promise<ApiResult<ContentType>> {
  return apiFetch<ContentType>(contentTypePath(workspaceId, projectId, contentTypeKey), {
    method: "PUT",
    body: JSON.stringify(input),
    cache: "no-store",
  })
}

export async function deleteContentType(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
): Promise<ApiResult<void>> {
  return apiFetch<void>(contentTypePath(workspaceId, projectId, contentTypeKey), {
    method: "DELETE",
    cache: "no-store",
  })
}

export async function listContentEntries(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  options?: {
    filters?: ContentEntryFilter[]
    page?: number
    pageSize?: number
    sort?: string
    status?: ContentEntryStatus
  },
): Promise<ApiResult<ContentEntriesPage>> {
  const params = new URLSearchParams({
    page: String(options?.page ?? 1),
    pageSize: String(options?.pageSize ?? 25),
  })
  params.set("sort", options?.sort || "-$updatedAt")
  if (options?.status) params.set("status", options.status)
  for (const filter of [...(options?.filters ?? [])].sort((a, b) => {
    const left = JSON.stringify([a.field, a.operator, a.value])
    const right = JSON.stringify([b.field, b.operator, b.value])
    return left < right ? -1 : left > right ? 1 : 0
  })) {
    params.append(`filter[${filter.field}][${filter.operator}]`, filter.value)
  }

  return cachedWorkspaceRead<ContentEntriesPage>(
    workspaceId,
    `${entriesPath(workspaceId, projectId, contentTypeKey)}?${params}`,
  )
}

export async function getContentEntry(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  entryId: string,
): Promise<ApiResult<ContentEntry>> {
  return apiFetch<ContentEntry>(
    `${entriesPath(workspaceId, projectId, contentTypeKey)}/${encodeURIComponent(entryId)}`,
    { cache: "no-store" },
  )
}

export async function createContentEntry(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  input: Pick<ContentEntry, "data" | "status">,
): Promise<ApiResult<ContentEntry>> {
  return apiFetch<ContentEntry>(entriesPath(workspaceId, projectId, contentTypeKey), {
    method: "POST",
    body: JSON.stringify(input),
    cache: "no-store",
  })
}

export async function updateContentEntry(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  entryId: string,
  input: Pick<ContentEntry, "data" | "status">,
): Promise<ApiResult<ContentEntry>> {
  return apiFetch<ContentEntry>(
    `${entriesPath(workspaceId, projectId, contentTypeKey)}/${encodeURIComponent(entryId)}`,
    { method: "PUT", body: JSON.stringify(input), cache: "no-store" },
  )
}

export async function deleteContentEntry(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
  entryId: string,
): Promise<ApiResult<void>> {
  return apiFetch<void>(
    `${entriesPath(workspaceId, projectId, contentTypeKey)}/${encodeURIComponent(entryId)}`,
    { method: "DELETE", cache: "no-store" },
  )
}
