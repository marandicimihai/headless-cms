"use client"

import Link from "next/link"
import { useActionState } from "react"

import {
  createProjectAction,
  type ProjectActionState,
} from "../actions"
import { Button } from "@/components/ui/button"
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { useActionToast } from "@/hooks/use-action-toast"

const initialProjectActionState: ProjectActionState = {
  status: "idle",
  message: null,
  fieldErrors: {},
}

export function CreateProjectForm({ workspaceId }: { workspaceId: string }) {
  const [state, formAction, pending] = useActionState(
    createProjectAction.bind(null, workspaceId),
    initialProjectActionState,
  )
  const nameErrors = state.fieldErrors.name ?? []
  useActionToast(state)

  return (
    <form action={formAction} className="max-w-xl">
      <FieldGroup>
        <Field data-invalid={nameErrors.length > 0}>
          <FieldLabel htmlFor="project-name">Project name</FieldLabel>
          <Input
            id="project-name"
            name="name"
            minLength={3}
            maxLength={100}
            placeholder="Marketing website"
            required
          />
          <FieldError errors={nameErrors.map((message) => ({ message }))} />
        </Field>

        <div className="flex items-center gap-2">
          <Button type="submit" disabled={pending}>
            {pending ? "Creating..." : "Create project"}
          </Button>
          <Button
            nativeButton={false}
            variant="ghost"
            render={<Link href={`/workspaces/${workspaceId}/projects`} />}
          >
            Cancel
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}
