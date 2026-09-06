#!/bin/sh
set -eu

public_url=${1:?Usage: smoke-test.sh https://stock.example.com}
api_bind_address=${API_BIND_ADDRESS:?set API_BIND_ADDRESS}
api_port=${API_PORT:-8080}
curl_options="--fail --silent --show-error"
if [ "${CURL_INSECURE:-false}" = "true" ]; then
    curl_options="$curl_options --insecure"
fi
case "$public_url" in
    https://*) ;;
    *) echo "Public URL must use HTTPS" >&2; exit 2 ;;
esac

# shellcheck disable=SC2086
curl $curl_options "$public_url/health/live" >/dev/null
# shellcheck disable=SC2086
curl $curl_options "$public_url/health/ready" >/dev/null
# shellcheck disable=SC2086
headers=$(curl $curl_options --head "$public_url/")
echo "$headers" | grep -qi '^strict-transport-security:'
echo "$headers" | grep -qi '^x-content-type-options: nosniff'
echo "$headers" | grep -qi '^x-correlation-id:'

published_ports=$(docker compose -f compose.yaml -f compose.production.yaml ps --format json)
if echo "$published_ports" | grep -Eq '(^|[^0-9])5432([^0-9]|$).*0\.0\.0\.0'; then
    echo "Database port is publicly bound" >&2
    exit 1
fi
echo "$published_ports" | grep -Fq "$api_bind_address:$api_port"

echo "External HTTPS proxy, security headers, health checks, and API bind address verified"
