#!/bin/sh
set -eu

image_name="home-stock-print-worker-test:local"
script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

docker build --target test --tag "$image_name" "$script_dir"
docker run --rm "$image_name"
