import { describe, expect, it } from "vitest"

import {
  curlExample,
  groupOperations,
  mediaExample,
  parseDocumentation,
  resolveSchema,
  schemaType,
} from "./documentation"

const contract = {
  openapi: "3.0.3",
  paths: {
    "/api/auth/login": {
      post: {
        operationId: "login",
        tags: ["Authentication"],
        summary: "Log in",
        description: "Access: Anonymous.\\n\\nCreates a session.",
        requestBody: {
          content: {
            "application/json": {
              schema: { $ref: "#/components/schemas/Login" },
            },
          },
        },
        responses: {
          "200": {
            description: "Success.",
            content: {
              "application/json": {
                schema: { type: "object" },
                example: { email: "reader@example.com" },
              },
            },
          },
        },
      },
    },
    "/api/workspaces/{workspaceId}/projects": {
      get: {
        operationId: "list-projects",
        tags: ["Projects"],
        summary: "List projects",
        description: "Access: Workspace Member.\\n\\nLists projects.",
        parameters: [{
          name: "workspaceId",
          in: "path",
          required: true,
          description: "Workspace UUID.",
          schema: { type: "string" },
        }],
        responses: { "204": { description: "No content." } },
      },
    },
  },
  components: {
    schemas: {
      Login: {
        type: "object",
        example: { email: "reader@example.com", password: "example-password-123" },
        properties: { email: { type: "string" }, password: { type: "string" } },
      },
      Role: { type: "string", enum: ["owner", "editor", "member"] },
    },
  },
}

const documentation = parseDocumentation(contract)

describe("documentation contract helpers", () => {
  it("groups and searches operation metadata", () => {
    expect(groupOperations(documentation.operations, "").map(([tag]) => tag)).toEqual(["Authentication", "Projects"])
    expect(groupOperations(documentation.operations, "GET project")[0][1][0].operationId).toBe("list-projects")
    expect(groupOperations(documentation.operations, "anonymous")[0][1][0].operationId).toBe("login")
    expect(groupOperations(documentation.operations, "missing")).toEqual([])
  })

  it("accepts OpenAPI operations that omit optional display metadata", () => {
    const generated = {
      openapi: "3.1.1",
      paths: {
        "/api/workspaces": {
          get: { responses: { "200": { description: "Success." } } },
        },
      },
    }
    const [operation] = parseDocumentation(generated).operations
    expect(operation.operationId).toBe("get-/api/workspaces")
    expect(operation.summary).toBe("GET /api/workspaces")
    expect(operation.tags).toEqual(["Workspaces"])
  })

  it("resolves schemas and uses contract examples", () => {
    const body = documentation.operations[0].requestBody!.content["application/json"]
    expect(mediaExample(body, documentation.schemas)).toEqual({
      email: "reader@example.com",
      password: "example-password-123",
    })
    expect(schemaType({ $ref: "#/components/schemas/Role", nullable: true }, documentation.schemas))
      .toBe("\"owner\" | \"editor\" | \"member\" | null")
    expect(resolveSchema({ $ref: "#/components/schemas/Missing" }, documentation.schemas).type).toBeUndefined()
  })

  it("builds copyable cookie-jar cURL examples without a token", () => {
    const login = curlExample(documentation.operations[0], documentation.schemas, "https://cms.example.com/")
    expect(login).toContain("--url 'https://cms.example.com/api/auth/login'")
    expect(login).toContain("--cookie-jar cookies.txt")
    expect(login).toContain("\"email\": \"reader@example.com\"")
    expect(login).not.toContain("Bearer")
    const list = curlExample(documentation.operations[1], documentation.schemas, "https://cms.example.com")
    expect(list).toContain("<workspaceId>")
    expect(list).not.toContain("--data")
  })

  it("rejects unusable contracts", () => {
    expect(() => parseDocumentation({})).toThrow()
    expect(() => parseDocumentation({ ...contract, paths: {} })).toThrow()
  })
})
