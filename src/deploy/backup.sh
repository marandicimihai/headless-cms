#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 1 ]]; then
  echo "Usage: bash src/deploy/backup.sh /absolute/backup/directory" >&2
  exit 2
fi
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
backup_dir="$1"
[[ "$backup_dir" = /* ]] || { echo "Backup directory must be absolute" >&2; exit 2; }
umask 077
mkdir -p "$backup_dir"
backup_name="cms-$(date -u +%Y%m%dT%H%M%SZ)-$$.dump"
docker compose --env-file "${CMS_ENV_FILE:-.env.production}" -f compose.prod.yaml \
  run --rm --no-deps --user "$(id -u):$(id -g)" -v "$backup_dir:/backups" db-tools \
  sh -ec 'umask 077; pg_dump --format=custom --no-owner --no-acl --file="/backups/$1.partial"; mv "/backups/$1.partial" "/backups/$1"' backup "$backup_name"
chmod 600 "$backup_dir/$backup_name"
echo "$backup_dir/$backup_name"
