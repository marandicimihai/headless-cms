"use client"

import { useActionState, useState } from "react"

import {
  deleteProjectAction,
  renameProjectAction,
  type ProjectActionState,
} from "../actions"
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { useActionToast } from "@/hooks/use-action-toast"

const initialProjectActionState: ProjectActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

export function ProjectManagement({
  workspaceId,
  projectId,
  projectName,
}: {
  workspaceId: string
  projectId: string
  projectName: string
}) {
  const renameAction = renameProjectAction.bind(null, workspaceId, projectId)
  const deleteAction = deleteProjectAction.bind(null, workspaceId, projectId)
  const [renameState, renameFormAction, renamePending] = useActionState(
    renameAction,
    initialProjectActionState,
  )
  const [deleteState, deleteFormAction, deletePending] = useActionState(
    deleteAction,
    initialProjectActionState,
  )
  const [name, setName] = useState(projectName)
  const [previousProjectName, setPreviousProjectName] = useState(projectName)
  const [confirmation, setConfirmation] = useState("")
  const nameErrors = renameState.fieldErrors.name ?? []
  const confirmationErrors = deleteState.fieldErrors.confirmation ?? []
  const confirmationMatches = confirmation === projectName

  if (previousProjectName !== projectName) {
    setPreviousProjectName(projectName)
    setName(projectName)
    setConfirmation("")
  }

  useActionToast(renameState)
  useActionToast(deleteState)

  return (
    <div className="space-y-10">
      <section aria-labelledby="rename-project-heading" className="space-y-4">
          <form action={renameFormAction} className="max-w-2xl space-y-4">
          <Field data-invalid={nameErrors.length > 0}>
            <FieldLabel htmlFor="project-name">Project name</FieldLabel>
            <div className="flex flex-col gap-3 sm:flex-row">
              <Input
                id="project-name"
                name="name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                minLength={3}
                maxLength={100}
                required
              />
              <Button type="submit" disabled={renamePending}>
                {renamePending ? "Saving..." : "Save name"}
              </Button>
            </div>
            <FieldError errors={nameErrors.map((message) => ({ message }))} />
          </Field>
        </form>
      </section>

      <section
        aria-labelledby="delete-project-heading"
        className="space-y-4 border-t pt-8"
      >
        <div>
          <h2 id="delete-project-heading" className="text-sm font-semibold">
            Delete project
          </h2>
          <p className="text-sm text-muted-foreground">
            Deleting a project permanently removes its content types and entries.
          </p>
        </div>
        <AlertDialog onOpenChange={(open) => !open && setConfirmation("")}>
          <AlertDialogTrigger render={<Button variant="destructive" />}>
            Delete project
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete {projectName}?</AlertDialogTitle>
              <AlertDialogDescription>
                This permanently removes the project and every content type and entry inside it.
                Type the project name to continue.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <form id="delete-project-form" action={deleteFormAction} className="space-y-2">
              <Field data-invalid={confirmationErrors.length > 0}>
                <FieldLabel htmlFor="delete-project-confirmation">
                  Project name
                </FieldLabel>
                <Input
                  id="delete-project-confirmation"
                  name="confirmation"
                  value={confirmation}
                  onChange={(event) => setConfirmation(event.target.value)}
                  autoComplete="off"
                  required
                />
                <FieldError
                  errors={confirmationErrors.map((message) => ({ message }))}
                />
              </Field>
            </form>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={deletePending}>Cancel</AlertDialogCancel>
              <Button
                type="submit"
                form="delete-project-form"
                variant="destructive"
                disabled={deletePending || !confirmationMatches}
              >
                {deletePending ? "Deleting..." : "Delete project"}
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </section>
    </div>
  )
}
