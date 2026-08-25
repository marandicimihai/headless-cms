export default function WorkspacePreviewPage() {
  return (
    <main className="flex flex-1 flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Preview</h1>
        <p className="text-sm text-muted-foreground">
          A workspace overview will appear here as projects and content are
          added.
        </p>
      </div>

      <section aria-labelledby="preview-placeholder-heading" className="border-y">
        <div className="py-5">
          <h2 id="preview-placeholder-heading" className="text-sm font-semibold">
            Workspace overview
          </h2>
          <p className="text-sm text-muted-foreground">
            Preview data is not available yet.
          </p>
        </div>
        <div aria-hidden="true" className="grid gap-4 border-t py-5 md:grid-cols-3">
          <div className="h-20 rounded-lg bg-muted" />
          <div className="h-20 rounded-lg bg-muted" />
          <div className="h-20 rounded-lg bg-muted" />
        </div>
      </section>
    </main>
  )
}
