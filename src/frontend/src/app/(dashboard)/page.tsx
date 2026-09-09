import { AlertCircle, Boxes, Plus } from "lucide-react"
import Link from "next/link"
import { redirect } from "next/navigation"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { WorkspaceChooser } from "@/app/(dashboard)/workspace-chooser"
import { getCurrentWorkspaceId } from "@/lib/current-workspace"
import { listMyWorkspaces } from "@/lib/api/workspaces"

export default async function DashboardPage() {
  const result = await listMyWorkspaces()

  if (!result.ok) {
    return (
      <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Workspaces</h1>
        </div>
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>Unable to load workspaces</AlertTitle>
          <AlertDescription>{result.error.detail}</AlertDescription>
        </Alert>
      </main>
    )
  }

  const currentWorkspaceId = await getCurrentWorkspaceId()
  const currentWorkspace = result.data.find(
    (workspace) =>
      workspace.id.toLowerCase() === currentWorkspaceId?.toLowerCase(),
  )

  if (currentWorkspace?.currentRole) {
    redirect(`/workspaces/${currentWorkspace.id}`)
  }

  if (result.data.length > 0) {
    return <WorkspaceChooser workspaces={result.data} />
  }

  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <section className="flex min-h-80 flex-col items-center justify-center gap-4 text-center">
        <Boxes className="size-8 text-muted-foreground" />
        <div className="max-w-sm space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">
            Create a workspace
          </h1>
          <p className="text-sm text-muted-foreground">
            Create your first workspace to start organizing projects and content.
          </p>
        </div>
        <Button nativeButton={false} render={<Link href="/workspaces/new" />}>
          <Plus />
          Create workspace
        </Button>
      </section>
    </main>
  )
}
