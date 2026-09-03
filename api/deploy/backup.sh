#!/bin/sh
# Daily MongoDB backup (mongodump) on the production VPS.
# Call from a crontab entry, e.g.:
#   0 3 * * * /path/to/api/deploy/backup.sh >> /var/log/lexicall-backup.log 2>&1
#
# Needs root to reach the Docker socket — run this from root's crontab,
# or route the docker invocation through a privilege-escalation wrapper
# if the cron user isn't root.
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
BACKUP_DIR="${LEXICALL_BACKUP_DIR:-$SCRIPT_DIR/backups}"
RETENTION_DAYS="${LEXICALL_BACKUP_RETENTION_DAYS:-14}"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
ARCHIVE="$BACKUP_DIR/lexicall-$TIMESTAMP.archive.gz"

mkdir -p "$BACKUP_DIR"

cd "$SCRIPT_DIR"
MONGO_URI=$(grep -oP '(?<=^MONGO_URI=).*' .env)
MONGO_DB_NAME=$(grep -oP '(?<=^MONGO_DB_NAME=).*' .env)
sudo -n /usr/local/sbin/lexicall-mongo-backup "$MONGO_URI" "$MONGO_DB_NAME" > "$ARCHIVE"

find "$BACKUP_DIR" -name 'lexicall-*.archive.gz' -mtime "+$RETENTION_DAYS" -delete

echo "Backup written to $ARCHIVE"
