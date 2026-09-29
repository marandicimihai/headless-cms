import { headers } from "next/headers"
import { redirect } from "next/navigation"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { getDocumentation } from "@/lib/api/documentation"
import { getSession } from "@/lib/api/session"
import { ApiReference } from "./api-reference"

export const metadata = { title: "Documentation · Headless CMS" }

export default async function DocumentationPage() {
  // Guard this fetch independently: Next.js renders pages and layouts concurrently.
  if (!await getSession()) redirect("/auth/login")
  const documentation = await getDocumentation()
  const requestHeaders = await headers()
  const origin = process.env.PUBLIC_API_URL ?? (process.env.NODE_ENV === "development"
    ? "http://localhost:5123"
    : `${requestHeaders.get("x-forwarded-proto") ?? "https"}://${requestHeaders.get("x-forwarded-host") ?? requestHeaders.get("host")}`)

  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6">
      <h1 className="mb-6 text-2xl font-semibold">Documentation</h1>
      {documentation ? <ApiReference documentation={documentation} baseUrl={origin} /> : (
        <Alert variant="destructive">
          <AlertTitle>Unable to load API documentation</AlertTitle>
          <AlertDescription>The API reference is temporarily unavailable. Refresh the page to try again.</AlertDescription>
        </Alert>
      )}
    </div>
  )
}
