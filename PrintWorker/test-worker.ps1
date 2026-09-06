$ErrorActionPreference = 'Stop'

$imageName = 'home-stock-print-worker-test:local'
docker build --target test --tag $imageName $PSScriptRoot
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

docker run --rm $imageName
exit $LASTEXITCODE
