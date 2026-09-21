#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then
  echo "Usage: bash src/deploy/restore.sh /absolute/backup.dump existing_target_database" >&2
  exit 2
fi
backup_file="$1"
target_database="$2"
[[ "$backup_file" = /* && -f "$backup_file" && "$target_database" =~ ^[a-zA-Z][a-zA-Z0-9_]*$ ]] ||
  { echo "Supply an existing absolute dump path and a simple database name" >&2; exit 2; }
echo "Restore will overwrite objects in database '$target_database' on BACKUP_PGHOST." >&2
read -r -p "Type '$target_database' to confirm: " confirmation
[[ "$confirmation" == "$target_database" ]] || { echo "Restore cancelled" >&2; exit 1; }
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
docker compose --env-file "${CMS_ENV_FILE:-.env.production}" -f compose.prod.yaml \
  run --rm --no-deps --user "$(id -u):$(id -g)" -v "$backup_file:/restore.dump:ro" db-tools \
  pg_restore --clean --if-exists --exit-on-error --single-transaction \
  --no-owner --no-acl --dbname="$target_database" /restore.dump
echo "Restored database '$target_database'."
