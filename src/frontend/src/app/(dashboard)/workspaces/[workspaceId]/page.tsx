export default function WorkspacePreviewPage() {
  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Preview</h1>
      </div>

      <div aria-hidden="true" className="grid gap-4 py-5 md:grid-cols-3">
          <div className="h-20 rounded-xl bg-muted" />
          <div className="h-20 rounded-xl bg-muted" />
          <div className="h-20 rounded-xl bg-muted" />
      </div>
    </main>
  )
}
