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
docker compose -f docker-compose.prod.yml exec -T mongo \
    mongodump --archive --gzip > "$ARCHIVE"

find "$BACKUP_DIR" -name 'lexicall-*.archive.gz' -mtime "+$RETENTION_DAYS" -delete

echo "Backup written to $ARCHIVE"
