export type FieldErrors = Record<string, string[]>;

export type ApiError = {
  status: number;
  code?: string;
  detail: string;
  fieldErrors: FieldErrors;
  traceId?: string;
}

export type ApiResult<T> = 
  | { ok: true, data: T } 
  | { ok : false, error: ApiError}
