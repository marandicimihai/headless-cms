"use client"

import { useState, useTransition, type FormEvent } from "react"
import { AlertCircle, Bot, LoaderCircle, Send, Sparkles } from "lucide-react"
import { MessageScroller } from "@shadcn/react/message-scroller"

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Bubble, BubbleContent } from "@/components/ui/bubble"
import { Marker, MarkerContent, MarkerIcon } from "@/components/ui/marker"
import { Message, MessageAvatar, MessageContent, MessageHeader } from "@/components/ui/message"
import { Textarea } from "@/components/ui/textarea"
import {
  createSchemaProposalAction,
  generateSchemaProposalAction,
} from "./schema-assistant-actions"
import type {
  SchemaAssistantContentType,
  SchemaAssistantMessage,
} from "@/lib/types/schema-assistant"

export function SchemaAssistantChat({
  workspaceId,
  projectId,
}: {
  workspaceId: string
  projectId: string
}) {
  const [messages, setMessages] = useState<SchemaAssistantMessage[]>([])
  const [proposal, setProposal] = useState<SchemaAssistantContentType[]>([])
  const [conflicts, setConflicts] = useState<string[]>([])
  const [prompt, setPrompt] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [created, setCreated] = useState<string[]>([])
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [isPending, startTransition] = useTransition()

  function submitMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const content = prompt.trim()
    if (!content || isPending) return
    if (content.length > 2000) {
      setError("Keep each message under 2000 characters.")
      return
    }

    const nextMessages: SchemaAssistantMessage[] = [
      ...messages.slice(-11),
      { role: "user", content },
    ]
    setMessages(nextMessages)
    setPrompt("")
    setError(null)
    setCreated([])
    startTransition(async () => {
      const result = await generateSchemaProposalAction(
        workspaceId,
        projectId,
        nextMessages,
      )
      if (!result.ok) {
        setError(result.error)
        return
      }

      const response = result.data
      setProposal(response.contentTypes)
      setConflicts(response.existingKeyConflicts)
      setMessages((current) => [
        ...current,
        {
          role: "assistant",
          content: response.reply,
          proposal: response.contentTypes,
        },
      ])
    })
  }

  function createProposal() {
    if (proposal.length === 0 || conflicts.length > 0 || isPending) return
    setError(null)
    setCreated([])
    startTransition(async () => {
      const result = await createSchemaProposalAction(
        workspaceId,
        projectId,
        proposal,
      )
      if (!result.ok) {
        setError(result.error)
        setConfirmOpen(false)
        return
      }
      setCreated(result.data.contentTypes.map((type) => type.key))
      setConfirmOpen(false)
    })
  }

  return (
    <div className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Schema assistant</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Describe the content you need. Refine the proposal in chat, then review it before creation.
        </p>
      </div>

      <section className="flex min-h-0 flex-1 flex-col gap-5" aria-label="Schema assistant chat">
        <MessageScroller.Provider autoScroll defaultScrollPosition="end">
          <MessageScroller.Root className="relative h-[min(48vh,32rem)] min-h-40 overflow-hidden rounded-xl border">
            <MessageScroller.Viewport className="h-full overflow-y-auto">
              <MessageScroller.Content className="flex min-h-full flex-col justify-end gap-4 p-4">
                {messages.length === 0 ? (
                  <MessageScroller.Item messageId="empty-prompt">
                    <Message>
                      <MessageAvatar>
                        <Avatar><AvatarFallback><Sparkles aria-hidden="true" /></AvatarFallback></Avatar>
                      </MessageAvatar>
                      <MessageContent>
                        <MessageHeader>Schema assistant</MessageHeader>
                        <Bubble variant="muted"><BubbleContent>For example: “I need articles with a title, body, author name, publication status, and view count. Also add categories.”</BubbleContent></Bubble>
                      </MessageContent>
                    </Message>
                  </MessageScroller.Item>
                ) : messages.map((message, index) => (
                  <MessageScroller.Item
                    key={`${message.role}-${index}`}
                    messageId={`${message.role}-${index}`}
                    scrollAnchor={message.role === "user"}
                  >
                    <Message align={message.role === "user" ? "end" : "start"}>
                      <MessageAvatar>
                        <Avatar><AvatarFallback>{message.role === "assistant" ? <Bot aria-hidden="true" /> : "You"}</AvatarFallback></Avatar>
                      </MessageAvatar>
                      <MessageContent>
                        <MessageHeader>{message.role === "assistant" ? "Schema assistant" : "You"}</MessageHeader>
                        <Bubble variant={message.role === "user" ? "secondary" : "muted"}>
                          <BubbleContent className="whitespace-pre-wrap">{message.content}</BubbleContent>
                        </Bubble>
                      </MessageContent>
                    </Message>
                  </MessageScroller.Item>
                ))}
                {isPending ? (
                  <MessageScroller.Item messageId="pending-response">
                    <Message>
                      <Marker role="status">
                        <MarkerIcon><LoaderCircle className="animate-spin" /></MarkerIcon>
                        <MarkerContent>Working on the proposal…</MarkerContent>
                      </Marker>
                    </Message>
                  </MessageScroller.Item>
                ) : null}
              </MessageScroller.Content>
            </MessageScroller.Viewport>
            <MessageScroller.Button className="absolute bottom-3 left-1/2 z-10 -translate-x-1/2 rounded-full border bg-background px-3 py-1 text-xs font-medium shadow-sm inert:opacity-0">
              Jump to latest
            </MessageScroller.Button>
          </MessageScroller.Root>
        </MessageScroller.Provider>

        <form onSubmit={submitMessage} className="flex items-end gap-2">
          <label className="sr-only" htmlFor="schema-assistant-prompt">Describe or refine your schema</label>
          <Textarea
            id="schema-assistant-prompt"
            value={prompt}
            onChange={(event) => setPrompt(event.target.value)}
            maxLength={2000}
            rows={3}
            placeholder="Describe the content types and fields you need…"
            disabled={isPending}
          />
          <Button type="submit" aria-label="Send message" disabled={isPending || !prompt.trim()}>
            <Send />
            Send
          </Button>
        </form>
        <p className="text-xs text-muted-foreground">
          AI suggestions can be wrong. Prompts and proposals are sent to Groq using this app’s configured key. The CMS supports text, number, and boolean fields.
        </p>

        {error ? (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertTitle>Unable to continue</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}
        {created.length > 0 ? (
          <Alert>
            <AlertTitle>Content types created</AlertTitle>
            <AlertDescription>{created.join(", ")}</AlertDescription>
          </Alert>
        ) : null}

        {proposal.length > 0 ? (
          <section className="space-y-3" aria-labelledby="proposal-heading">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 id="proposal-heading" className="text-sm font-semibold">Proposed content types</h2>
                <p className="text-sm text-muted-foreground">Review the fields below, then confirm to create them.</p>
              </div>
              <Button
                type="button"
                onClick={() => setConfirmOpen(true)}
                disabled={isPending || conflicts.length > 0}
              >
                {isPending ? <LoaderCircle className="animate-spin" /> : null}
                Create {proposal.length} content {proposal.length === 1 ? "type" : "types"}
              </Button>
            </div>

            {conflicts.length > 0 ? (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertTitle>Some keys already exist</AlertTitle>
                <AlertDescription>
                  Ask the assistant to choose different keys: {conflicts.join(", ")}.
                </AlertDescription>
              </Alert>
            ) : null}

            <div className="space-y-3">
              {proposal.map((type) => (
                <div key={type.key} className="rounded-xl border p-4">
                  <h3 className="font-mono text-sm font-semibold">{type.key}</h3>
                  <ul className="mt-2 divide-y text-sm">
                    {type.fields.map((field) => (
                      <li key={field.key} className="flex flex-wrap items-center gap-x-2 py-2 first:pt-0 last:pb-0">
                        <span className="font-mono">{field.key}</span>
                        <span className="text-muted-foreground">{field.type}</span>
                        {field.required ? <span className="text-muted-foreground">required</span> : null}
                        {field.defaultValue !== null ? (
                          <span className="text-muted-foreground">default: {String(field.defaultValue)}</span>
                        ) : null}
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          </section>
        ) : null}
      </section>

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Create these content types?</AlertDialogTitle>
            <AlertDialogDescription>
              This will add {proposal.length} new content types to the project: {proposal.map((type) => type.key).join(", ")}.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isPending}>Review</AlertDialogCancel>
            <AlertDialogAction onClick={createProposal} disabled={isPending}>
              {isPending ? "Creating…" : "Confirm creation"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
