export type FieldErrors = Record<string, string[]>;

export type ApiError = {
  status: number;
  code?: string;
  detail: string;
  fieldErrors: FieldErrors;
  traceId?: string;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

export async function parseApiError(
  response: Response
): Promise<ApiError> {
  let body: unknown;

  try {
    body = await response.json()
  } catch {
    body = null
  }

  const problem = isObject(body) ? body : {}
  const fieldErrors: FieldErrors = {}

  if (Array.isArray(problem.errors)) {
    for (const error of problem.errors) {
      if (
        isObject(error) &&
        typeof error.name === "string" &&
        typeof error.reason === "string"
      ) {
        fieldErrors[error.name] ??= [];
        fieldErrors[error.name].push(error.reason);
      }
    }
  }

  return {
    status: response.status,
    code:
      typeof problem.code === "string"
        ? problem.code
        : undefined,
    detail:
      typeof problem.detail === "string"
        ? problem.detail
        : typeof problem.title === "string"
          ? problem.title
          : "Request failed.",
    fieldErrors,
    traceId:
      typeof problem.traceId === "string"
        ? problem.traceId
        : undefined,
  };
}