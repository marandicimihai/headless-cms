import "server-only"

import type { ApiResult } from "@/lib/types/general"
import type {
  ContentEntriesPage,
  ContentEntry,
  ContentEntryStatus,
  ContentType,
  ContentTypeInput,
} from "@/lib/types/content"

import { apiFetch } from "./fetch-utils"

function contentTypesPath(workspaceId: string, projectId: string) {
  return `/api/workspaces/${encodeURIComponent(workspaceId)}/projects/${encodeURIComponent(projectId)}/content-types`
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
  return apiFetch<ContentType[]>(contentTypesPath(workspaceId, projectId), {
    cache: "no-store",
  })
}

export async function getContentType(
  workspaceId: string,
  projectId: string,
  contentTypeKey: string,
): Promise<ApiResult<ContentType>> {
  return apiFetch<ContentType>(contentTypePath(workspaceId, projectId, contentTypeKey), {
    cache: "no-store",
  })
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
  options?: { status?: ContentEntryStatus; page?: number; pageSize?: number },
): Promise<ApiResult<ContentEntriesPage>> {
  const params = new URLSearchParams({
    page: String(options?.page ?? 1),
    pageSize: String(options?.pageSize ?? 25),
  })
  if (options?.status) params.set("status", options.status)

  return apiFetch<ContentEntriesPage>(
    `${entriesPath(workspaceId, projectId, contentTypeKey)}?${params}`,
    { cache: "no-store" },
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
