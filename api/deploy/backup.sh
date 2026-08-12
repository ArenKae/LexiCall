#!/bin/sh
# Daily MongoDB backup (mongodump) on the production VPS.
# Call from a crontab entry, e.g.:
#   0 3 * * * /path/to/api/deploy/backup.sh >> /var/log/lexicall-backup.log 2>&1
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
BACKUP_DIR="${LEXICALL_BACKUP_DIR:-$SCRIPT_DIR/backups}"
RETENTION_DAYS="${LEXICALL_BACKUP_RETENTION_DAYS:-14}"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
ARCHIVE="$BACKUP_DIR/lexicall-$TIMESTAMP.archive.gz"

mkdir -p "$BACKUP_DIR"

cd "$SCRIPT_DIR"
# Root credentials, not the per-project lexicall_app user: this instance is
# meant to host other small projects too, and a full backup must cover every
# database, not just lexicall's.
ROOT_USER=$(grep -oP '(?<=^MONGO_ROOT_USER=).*' .env)
ROOT_PASSWORD=$(grep -oP '(?<=^MONGO_ROOT_PASSWORD=).*' .env)
docker compose -f docker-compose.prod.yml exec -T mongo \
    mongodump --archive --gzip --username "$ROOT_USER" --password "$ROOT_PASSWORD" --authenticationDatabase admin > "$ARCHIVE"

find "$BACKUP_DIR" -name 'lexicall-*.archive.gz' -mtime "+$RETENTION_DAYS" -delete

echo "Backup written to $ARCHIVE"
