import "server-only"

import { parseDocumentation } from "@/lib/documentation"

export async function getDocumentation() {
  try {
    // Read-only contract on the internal API origin. Never expose BACKEND_URL to the browser.
    const response = await fetch(`${process.env.BACKEND_URL}/openapi/v1.json`, {
      cache: "no-store",
      signal: AbortSignal.timeout(10_000),
    })
    if (!response.ok) return null
    return parseDocumentation(await response.json())
  } catch (error) {
    console.error("Unable to load the generated OpenAPI document.", error)
    return null
  }
}
