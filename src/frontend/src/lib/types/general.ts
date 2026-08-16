import { ApiError } from "../api/problem-details"

export type ApiResult<T> = 
  | { ok: true, data: T } 
  | { ok : false, error: ApiError}