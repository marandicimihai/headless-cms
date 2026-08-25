import Link from "next/link"
import { redirect } from "next/navigation"
import { AlertCircle, Boxes, Plus } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardAction,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { getSession } from "@/lib/api/session"
import { listMyWorkspaces } from "@/lib/api/workspaces"
import type { WorkspaceRole } from "@/lib/types/workspaces"

const dateFormatter = new Intl.DateTimeFormat("en", {
  dateStyle: "medium",
  timeZone: "UTC",
})

const roleLabels: Record<WorkspaceRole, string> = {
  Owner: "Owner",
  Editor: "Editor",
  Member: "Read only",
}

function formatCreatedAt(createdAt: string) {
  const date = new Date(createdAt)

  return Number.isNaN(date.getTime())
    ? "Unknown date"
    : dateFormatter.format(date)
}

function roleBadgeVariant(role: WorkspaceRole | null) {
  if (role === "Owner") return "default" as const
  if (role === "Editor") return "secondary" as const
  return "outline" as const
}

export default async function WorkspacesPage() {
  const session = await getSession()

  if (!session) {
    redirect("/auth/login")
  }

  const result = await listMyWorkspaces()
  const error = result.ok ? undefined : result.error.detail
  const workspaces = result.ok ? result.data : undefined
  const summary = [
    {
      title: "Total workspaces",
      value: workspaces?.length ?? 0,
      description: "Available to your account",
    },
    {
      title: "Owner",
      value:
        workspaces?.filter((workspace) => workspace.currentRole === "Owner")
          .length ?? 0,
      description: "Full workspace management",
    },
    {
      title: "Editor",
      value:
        workspaces?.filter((workspace) => workspace.currentRole === "Editor")
          .length ?? 0,
      description: "Content management access",
    },
    {
      title: "Read only",
      value:
        workspaces?.filter((workspace) => workspace.currentRole === "Member")
          .length ?? 0,
      description: "View-only content access",
    },
  ]

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Workspaces</h1>
          <p className="text-sm text-muted-foreground">
            The workspaces available to your account.
          </p>
        </div>
        {workspaces?.length ? (
          <Button
            nativeButton={false}
            render={<Link href="/workspaces/new" />}
          >
            <Plus />
            Create workspace
          </Button>
        ) : null}
      </div>

      {error ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>Unable to load workspaces</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : workspaces?.length ? (
        <>
          <dl className="grid gap-6 border-y py-5 sm:grid-cols-2 xl:grid-cols-4">
            {summary.map((item) => (
              <div key={item.title} className="space-y-1">
                <dt className="text-sm text-muted-foreground">{item.title}</dt>
                <dd className="text-2xl font-semibold tracking-tight">
                  {item.value}
                </dd>
                <dd className="text-xs text-muted-foreground">
                  {item.description}
                </dd>
              </div>
            ))}
          </dl>

          <section aria-labelledby="workspace-list-heading">
            <h2 id="workspace-list-heading" className="sr-only">
              Workspace list
            </h2>
            <ul className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
              {workspaces.map((workspace) => (
                <li key={workspace.id}>
                  <Link
                    href={`/workspaces/${workspace.id}`}
                    className="group block h-full rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                  >
                    <Card className="h-full min-h-36 transition-colors group-hover:bg-accent/40">
                      <CardHeader>
                        <CardTitle>
                          <h3>{workspace.name}</h3>
                        </CardTitle>
                        <CardDescription>
                          Created {formatCreatedAt(workspace.createdAt)}
                        </CardDescription>
                        <CardAction>
                          <Badge variant={roleBadgeVariant(workspace.currentRole)}>
                            {workspace.currentRole
                              ? roleLabels[workspace.currentRole]
                              : "Unknown role"}
                          </Badge>
                        </CardAction>
                      </CardHeader>
                    </Card>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        </>
      ) : (
        <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
          <Boxes className="size-8 text-muted-foreground" />
          <div className="max-w-sm space-y-1">
            <h2 className="text-sm font-semibold">No workspaces yet</h2>
            <p className="text-sm text-muted-foreground">
              Create your first workspace to start organizing projects and
              content.
            </p>
          </div>
          <Button
            nativeButton={false}
            render={<Link href="/workspaces/new" />}
          >
            <Plus />
            Create workspace
          </Button>
        </section>
      )}
    </main>
  )
}
