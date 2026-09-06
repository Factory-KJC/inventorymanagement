#!/bin/sh
set -eu

backup_directory=${BACKUP_DIRECTORY:-./backups}
retention_days=${BACKUP_RETENTION_DAYS:-14}
database_name=${POSTGRES_DB:-homestock}
database_user=${POSTGRES_USER:-homestock}
timestamp=$(date -u +%Y%m%dT%H%M%SZ)
backup_path="${backup_directory}/homestock-${timestamp}.dump"

mkdir -p "$backup_directory"
docker compose exec -T db pg_dump --username "$database_user" --dbname "$database_name" --format custom > "$backup_path"
pg_dump_size=$(wc -c < "$backup_path")
if [ "$pg_dump_size" -eq 0 ]; then
    echo "Backup is empty: $backup_path" >&2
    exit 1
fi

find "$backup_directory" -type f -name 'homestock-*.dump' -mtime "+$retention_days" -delete
echo "$backup_path"
