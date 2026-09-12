#!/bin/sh
set -eu

nas_mount_point=${NAS_MOUNT_POINT:-/mnt/terastation-home-stock}
backup_subdirectory=${BACKUP_SUBDIRECTORY:-home-stock}
retention_days=${BACKUP_RETENTION_DAYS:-30}
database_name=${POSTGRES_DB:-homestock}
database_user=${POSTGRES_USER:-homestock}
gpg_recipient=${BACKUP_GPG_RECIPIENT:?set BACKUP_GPG_RECIPIENT to the backup encryption key fingerprint}
timestamp=$(date -u +%Y%m%dT%H%M%SZ)
backup_directory="${nas_mount_point}/${backup_subdirectory}"
backup_name="homestock-${timestamp}.dump.gpg"
backup_path="${backup_directory}/${backup_name}"
temporary_backup=$(mktemp "${TMPDIR:-/tmp}/homestock-backup.XXXXXX.dump")
temporary_encrypted="${backup_path}.partial"
lock_file=${BACKUP_LOCK_FILE:-/tmp/home-stock-backup.lock}

cleanup() {
    rm -f "$temporary_backup" "$temporary_encrypted"
}
trap cleanup EXIT HUP INT TERM

case "$retention_days" in
    ''|*[!0-9]*) echo "BACKUP_RETENTION_DAYS must be a non-negative integer" >&2; exit 2 ;;
esac

if ! mountpoint -q "$nas_mount_point"; then
    echo "NAS is not mounted at $nas_mount_point; refusing to write to the local disk" >&2
    exit 1
fi

if [ ! -d "$backup_directory" ] || [ ! -w "$backup_directory" ]; then
    echo "Backup directory is missing or not writable: $backup_directory" >&2
    exit 1
fi

exec 9>"$lock_file"
if ! flock -n 9; then
    echo "Another Home Stock backup is already running" >&2
    exit 1
fi

docker compose exec -T db pg_dump \
    --username "$database_user" \
    --dbname "$database_name" \
    --format custom \
    --no-password > "$temporary_backup"

if [ ! -s "$temporary_backup" ]; then
    echo "PostgreSQL produced an empty backup" >&2
    exit 1
fi

# pg_restore can parse the archive before it is encrypted and copied to the NAS.
docker compose exec -T db pg_restore --list < "$temporary_backup" > /dev/null

gpg --batch --yes --trust-model always \
    --recipient "$gpg_recipient" \
    --output "$temporary_encrypted" \
    --encrypt "$temporary_backup"

if [ ! -s "$temporary_encrypted" ]; then
    echo "Encryption produced an empty backup" >&2
    exit 1
fi

mv "$temporary_encrypted" "$backup_path"
sha256sum "$backup_path" > "${backup_path}.sha256"

# Delete only files created by this script, and only after a new backup succeeds.
find "$backup_directory" -maxdepth 1 -type f \
    \( -name 'homestock-*.dump.gpg' -o -name 'homestock-*.dump.gpg.sha256' \) \
    -mtime "+$retention_days" -delete

echo "$backup_path"
