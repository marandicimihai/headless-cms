/** @vitest-environment jsdom */

import { afterEach, describe, expect, it } from "vitest"

import {
  clearCurrentWorkspaceCookie,
  setCurrentWorkspaceCookie,
} from "./current-workspace-client"
import { CURRENT_WORKSPACE_COOKIE_NAME } from "./current-workspace-cookie"

afterEach(() => {
  clearCurrentWorkspaceCookie()
})

describe("current workspace preference", () => {
  it("stores a workspace identifier in a browser cookie", () => {
    setCurrentWorkspaceCookie("workspace-1")

    expect(document.cookie).toContain(
      `${CURRENT_WORKSPACE_COOKIE_NAME}=workspace-1`,
    )
  })

  it("clears the stored workspace identifier", () => {
    setCurrentWorkspaceCookie("workspace-1")
    clearCurrentWorkspaceCookie()

    expect(document.cookie).not.toContain(CURRENT_WORKSPACE_COOKIE_NAME)
  })
})
