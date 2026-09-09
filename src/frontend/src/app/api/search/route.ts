import { NextRequest, NextResponse } from "next/server"

import { searchWorkspace } from "@/lib/api/search"

export async function GET(request: NextRequest) {
  const workspaceId = request.nextUrl.searchParams.get("workspaceId")
  const query = request.nextUrl.searchParams.get("query")
  const limit = request.nextUrl.searchParams.get("limit")

  if (!workspaceId || !query) {
    return NextResponse.json(
      { detail: "workspaceId and query are required." },
      { status: 400 },
    )
  }

  const parsedLimit = limit ? Number(limit) : 5
  if (!Number.isInteger(parsedLimit)) {
    return NextResponse.json({ detail: "limit must be an integer." }, { status: 400 })
  }

  const result = await searchWorkspace(workspaceId, query, parsedLimit)
  if (!result.ok) {
    return NextResponse.json(result.error, { status: result.error.status })
  }

  return NextResponse.json(result.data)
}
