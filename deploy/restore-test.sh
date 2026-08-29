#!/bin/sh
set -eu

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 BACKUP.dump" >&2
    exit 2
fi

backup_path=$1
database_user=${POSTGRES_USER:-homestock}
restore_database="homestock_restore_test_$(date -u +%Y%m%d%H%M%S)"

case "$restore_database" in
    homestock_restore_test_*) ;;
    *) echo "Unsafe restore database name" >&2; exit 1 ;;
esac

cleanup() {
    docker compose exec -T db dropdb --username "$database_user" --if-exists "$restore_database"
}
trap cleanup EXIT

docker compose exec -T db createdb --username "$database_user" "$restore_database"
docker compose exec -T db pg_restore --username "$database_user" --dbname "$restore_database" --exit-on-error < "$backup_path"
table_count=$(docker compose exec -T db psql --username "$database_user" --dbname "$restore_database" --tuples-only --no-align --command "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'inventorymanagement';")
if [ "$table_count" -lt 1 ]; then
    echo "Restore verification found no application tables" >&2
    exit 1
fi

echo "Restore verified with $table_count application tables"
