"use client"

import { useEffect, useId, useMemo, useRef, useState } from "react"
import { useRouter } from "next/navigation"
import {
  FileText,
  FolderKanban,
  LoaderCircle,
  Search,
  Shapes,
} from "lucide-react"

import { Alert, AlertDescription } from "@/components/ui/alert"
import { Input } from "@/components/ui/input"
import type {
  SearchContentType,
  SearchEntry,
  SearchProject,
  WorkspaceSearchResults,
} from "@/lib/types/search"

type SearchItem = {
  id: string
  href: string
  label: string
  description: string
  icon: typeof FolderKanban
}

type RequestState = "idle" | "loading" | "success" | "error"

const minimumQueryLength = 2
const debounceMs = 250

function projectItem(workspaceId: string, project: SearchProject): SearchItem {
  return {
    id: `project-${project.id}`,
    href: `/workspaces/${workspaceId}/projects/${project.id}`,
    label: project.name,
    description: "Project",
    icon: FolderKanban,
  }
}

function contentTypeItem(
  workspaceId: string,
  contentType: SearchContentType,
): SearchItem {
  return {
    id: `content-type-${contentType.id}`,
    href: `/workspaces/${workspaceId}/projects/${contentType.projectId}/content-types/${encodeURIComponent(contentType.key)}`,
    label: contentType.key,
    description: contentType.projectName,
    icon: Shapes,
  }
}

function entryItem(workspaceId: string, entry: SearchEntry): SearchItem {
  return {
    id: `entry-${entry.id}`,
    href: `/workspaces/${workspaceId}/projects/${entry.projectId}/content-types/${encodeURIComponent(entry.contentTypeKey)}?entry=${encodeURIComponent(entry.id)}`,
    label: entry.snippet,
    description: `${entry.projectName} · ${entry.contentTypeKey} · ${entry.id}`,
    icon: FileText,
  }
}

export function WorkspaceSearch({ workspaceId }: { workspaceId: string }) {
  const router = useRouter()
  const inputId = useId()
  const listboxId = useId()
  const rootRef = useRef<HTMLDivElement>(null)
  const [query, setQuery] = useState("")
  const [results, setResults] = useState<WorkspaceSearchResults | null>(null)
  const [state, setState] = useState<RequestState>("idle")
  const [error, setError] = useState<string | null>(null)
  const [isOpen, setIsOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  const [retryKey, setRetryKey] = useState(0)
  const normalizedQuery = query.trim()

  const groups = useMemo(() => {
    if (!results) return []

    return [
      {
        label: "Projects",
        total: results.projects.total,
        items: results.projects.items.map((item) => projectItem(workspaceId, item)),
      },
      {
        label: "Content types",
        total: results.contentTypes.total,
        items: results.contentTypes.items.map((item) => contentTypeItem(workspaceId, item)),
      },
      {
        label: "Entries",
        total: results.entries.total,
        items: results.entries.items.map((item) => entryItem(workspaceId, item)),
      },
    ].filter((group) => group.items.length > 0)
  }, [results, workspaceId])
  const items = useMemo(() => groups.flatMap((group) => group.items), [groups])
  const shouldSearch = normalizedQuery.length >= minimumQueryLength
  const showDropdown = isOpen && shouldSearch
  const hasListbox = showDropdown && state === "success"

  useEffect(() => {
    if (!shouldSearch) return

    const controller = new AbortController()
    const timeout = window.setTimeout(async () => {
      setError(null)
      setIsOpen(true)

      try {
        const params = new URLSearchParams({
          workspaceId,
          query: normalizedQuery,
          limit: "5",
        })
        const response = await fetch(`/api/search?${params}`, {
          signal: controller.signal,
        })
        const body: unknown = await response.json()

        if (!response.ok || !isWorkspaceSearchResults(body)) {
          const detail = isErrorResponse(body) ? body.detail : null
          throw new Error(detail || "Search is temporarily unavailable.")
        }

        setResults(body)
        setState("success")
        setActiveIndex(-1)
      } catch (requestError) {
        if (controller.signal.aborted) return
        setResults(null)
        setState("error")
        setError(
          requestError instanceof Error
            ? requestError.message
            : "Search is temporarily unavailable.",
        )
      }
    }, debounceMs)

    return () => {
      controller.abort()
      window.clearTimeout(timeout)
    }
  }, [normalizedQuery, retryKey, shouldSearch, workspaceId])

  useEffect(() => {
    function closeOnOutsidePointer(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setIsOpen(false)
        setActiveIndex(-1)
      }
    }

    document.addEventListener("pointerdown", closeOnOutsidePointer)
    return () => document.removeEventListener("pointerdown", closeOnOutsidePointer)
  }, [])

  function openItem(item: SearchItem) {
    setQuery("")
    setResults(null)
    setIsOpen(false)
    setActiveIndex(-1)
    router.push(item.href)
  }

  function handleChange(value: string) {
    setQuery(value)
    setIsOpen(true)
    setActiveIndex(-1)

    if (value.trim().length < minimumQueryLength) {
      setResults(null)
      setState("idle")
      setError(null)
      return
    }

    setResults(null)
    setState("loading")
    setError(null)
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape") {
      setIsOpen(false)
      setActiveIndex(-1)
      return
    }

    if (!items.length) return

    if (event.key === "ArrowDown") {
      event.preventDefault()
      setIsOpen(true)
      setActiveIndex((index) => (index + 1) % items.length)
      return
    }

    if (event.key === "ArrowUp") {
      event.preventDefault()
      setIsOpen(true)
      setActiveIndex((index) => (index <= 0 ? items.length - 1 : index - 1))
      return
    }

    if (event.key === "Enter" && activeIndex >= 0) {
      event.preventDefault()
      openItem(items[activeIndex])
    }
  }

  return (
    <div ref={rootRef} className="relative hidden w-full max-w-sm sm:block">
      <Search className="pointer-events-none absolute top-1/2 left-2.5 z-10 size-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        aria-activedescendant={activeIndex >= 0 ? `${listboxId}-${items[activeIndex]?.id}` : undefined}
        aria-autocomplete="list"
        aria-controls={hasListbox ? listboxId : undefined}
        aria-expanded={showDropdown}
        aria-label="Search workspace content"
        className="pl-8"
        id={inputId}
        placeholder="Search content..."
        role="combobox"
        type="search"
        value={query}
        onChange={(event) => handleChange(event.target.value)}
        onFocus={() => shouldSearch && setIsOpen(true)}
        onKeyDown={handleKeyDown}
      />
      {showDropdown ? (
        <div className="absolute top-[calc(100%+0.5rem)] z-50 w-full overflow-hidden rounded-xl border bg-popover text-popover-foreground shadow-md">
          {state === "loading" ? (
            <div className="flex items-center gap-2 px-3 py-3 text-sm text-muted-foreground">
              <LoaderCircle aria-hidden="true" className="size-4 animate-spin" />
              Searching…
            </div>
          ) : null}
          {state === "error" ? (
            <Alert className="m-2" variant="destructive">
              <AlertDescription>
                {error}
                <button
                  className="ml-1 font-medium underline underline-offset-2"
                  type="button"
                  onClick={() => {
                    setState("loading")
                    setRetryKey((value) => value + 1)
                  }}
                >
                  Retry
                </button>
              </AlertDescription>
            </Alert>
          ) : null}
          {state === "success" && groups.length === 0 ? (
            <p className="px-3 py-3 text-sm text-muted-foreground">
              No results for “{normalizedQuery}”.
            </p>
          ) : null}
          {state === "success" ? (
            <div aria-label="Search results" id={listboxId} role="listbox">
              {groups.map((group) => (
                <section
                  aria-label={group.label}
                  className="border-b last:border-b-0"
                  key={group.label}
                  role="group"
                >
                  <div className="flex items-center justify-between px-3 py-2 text-xs font-medium text-muted-foreground">
                    <span>{group.label}</span>
                    {group.total > group.items.length ? (
                      <span>Showing {group.items.length} of {group.total}</span>
                    ) : null}
                  </div>
                  {group.items.map((item) => {
                    const index = items.findIndex((candidate) => candidate.id === item.id)
                    const Icon = item.icon
                    return (
                      <button
                        aria-selected={activeIndex === index}
                        className="flex w-full items-start gap-2 px-3 py-2 text-left text-sm hover:bg-accent focus:bg-accent focus:outline-none"
                        id={`${listboxId}-${item.id}`}
                        key={item.id}
                        role="option"
                        tabIndex={-1}
                        type="button"
                        onMouseDown={(event) => event.preventDefault()}
                        onMouseMove={() => setActiveIndex(index)}
                        onClick={() => openItem(item)}
                      >
                        <Icon aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                        <span className="min-w-0">
                          <span className="block truncate">{item.label}</span>
                          <span className="block truncate text-xs text-muted-foreground">
                            {item.description}
                          </span>
                        </span>
                      </button>
                    )
                  })}
                </section>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

function isErrorResponse(value: unknown): value is { detail?: string } {
  return typeof value === "object" && value !== null
}

function isWorkspaceSearchResults(value: unknown): value is WorkspaceSearchResults {
  if (typeof value !== "object" || value === null) return false

  const candidate = value as Partial<WorkspaceSearchResults>
  return (
    isSearchGroup(candidate.projects) &&
    isSearchGroup(candidate.contentTypes) &&
    isSearchGroup(candidate.entries)
  )
}

function isSearchGroup(value: unknown): value is { items: unknown[]; total: number } {
  return (
    typeof value === "object" &&
    value !== null &&
    Array.isArray((value as { items?: unknown }).items) &&
    typeof (value as { total?: unknown }).total === "number"
  )
}
