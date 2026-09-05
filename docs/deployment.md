# Docker開発・Debian配備

WindowsとDebianで同じ`WebAPI/Dockerfile`を使用します。OSごとの差はComposeの環境変数と、本番だけ追加するCaddyに限定します。

## 前提

- Windows: Docker Desktop（Linux containersモード）とDocker Compose v2
- Debian: Docker EngineとDocker Compose plugin
- 本番: Debianホストへ到達するDNS名と、80/443番ポート

ARM64のDebianでもMicrosoft .NET、PostgreSQL、Caddyのマルチアーキテクチャイメージを利用できます。独自のネイティブライブラリを追加する場合は別途確認します。

## Windowsで開発する

PowerShellでリポジトリ直下から実行します。

```powershell
Copy-Item .env.example .env
# .envのPOSTGRES_PASSWORDとJWT_KEYを開発用の値へ変更する
docker compose up --build
```

- API: `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger`
- Liveness: `http://localhost:8080/health/live`
- Readiness: `http://localhost:8080/health/ready`

PostgreSQLは開発ホストのloopbackだけにポートを公開します。DBコンテナはAPI用の内部ネットワークと、loopback公開を成立させる独立した管理ネットワークへ接続します。LANやインターネットからは接続できません。DBを直接調査する場合は次を使用します。

```powershell
docker compose exec db psql -U homestock -d homestock
```

### pgAdmin 4から開発DBを確認する

Windows上のpgAdmin 4から接続する場合、開発用Composeを起動します。

```powershell
docker compose up -d db api
```

pgAdmin 4で「Register」→「Server」を選び、次の値を入力します。

| 項目 | 値 |
| --- | --- |
| Name | `Home Stock Development`（任意） |
| Host name/address | `localhost` |
| Port | `.env`の`POSTGRES_PORT`。未指定時は`5432` |
| Maintenance database | `.env`の`POSTGRES_DB`。既定値は`homestock` |
| Username | `.env`の`POSTGRES_USER`。既定値は`homestock` |
| Password | `.env`の`POSTGRES_PASSWORD` |

登録後、`Databases` → `homestock` → `Schemas` → `public` → `Tables`からテーブルを開き、「View/Edit Data」→「All Rows」で内容を確認できます。

本番ではloopbackを含めてDBポートを不要にする場合、ホストのファイアウォールとCompose設定で閉じてください。少なくとも5432番ポートをLANやインターネットへ公開してはいけません。

停止は`docker compose down`です。DBデータは名前付きVolumeに残ります。DBも消す`docker compose down --volumes`は開発データを破棄するため、明示的に初期化するときだけ使用します。

## Debianへ配置する

リポジトリをDebianへ配置し、`.env.example`から`.env`を作成します。本番では少なくとも以下を変更します。

```dotenv
ASPNETCORE_ENVIRONMENT=Production
TZ=Asia/Tokyo
POSTGRES_PASSWORD=<十分に長いランダム値>
JWT_KEY=<32バイト以上のランダム値>
SETUP_TOKEN=<JWT_KEYとは異なる十分に長いランダム値>
PUBLIC_HOST=stock.example.com
WEB_ORIGIN=https://stock.example.com
```

`TZ`はレシートへ印字する日時のタイムゾーンです。変更後はAPIコンテナを再作成してください。

秘密値は次のように生成できます。

```bash
openssl rand -base64 48
```

`.env`に`$`を含む値を直接書く場合、Composeの変数展開を避けるため値全体をシングルクォートで囲んでください。上記のBase64生成値は通常この問題を避けられます。

起動します。

```bash
docker compose -f compose.yaml -f compose.production.yaml up --build -d
docker compose -f compose.yaml -f compose.production.yaml ps
docker compose -f compose.yaml -f compose.production.yaml logs --tail=100 api caddy
```

CaddyがLet's Encrypt等からTLS証明書を自動取得します。ルーターまたはクラウド側では80/443だけをDebianへ通し、5432と8080は公開しません。`8080`はホストのloopbackにだけバインドされます。

起動後は初回ユーザー登録画面へ`SETUP_TOKEN`を入力します。登録が終わったら`.env`の値を新しいランダム値へ交換し、APIコンテナを再作成してください。ログインは送信元IPごとに1分5回へ制限されています。

## 更新

```bash
git pull --ff-only
docker compose -f compose.yaml -f compose.production.yaml up --build -d
docker image prune -f
```

更新前にPostgreSQLのバックアップを取得します。DBスキーマが正式なMigration管理へ移行するまでは破壊的なモデル変更を行いません。

## DB Migration

起動時に`Database__AutoMigrate=true`で未適用のEF Core Migrationを適用します。APIを複数台へ増やす場合は同時Migrationを避けるため、配備ジョブから一度だけ適用する方式へ変更します。

Migrationを追加する場合はリポジトリルートで実行します。

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> --project WebAPI/InventoryAPI.csproj --startup-project WebAPI/InventoryAPI.csproj --output-dir Data/Migrations
```

## バックアップと復元試験

```bash
BACKUP_DIRECTORY=/srv/backups/home-stock BACKUP_RETENTION_DAYS=14 sh ./deploy/backup.sh
sh ./deploy/restore-test.sh /srv/backups/home-stock/homestock-<timestamp>.dump
```

`backup.sh`はcustom形式のdumpを作成し、既定で14日を超えたdumpを削除します。`restore-test.sh`は一時DBへ復元し、アプリケーションテーブルを確認して一時DBを削除します。本番DBを復元先には使用しません。バックアップファイルはDebianホストだけに置き続けず、暗号化したうえで別媒体へ転送してください。少なくとも月1回、復元試験を実行します。

PostgreSQL統合テストは専用の空DBへの接続文字列を指定して実行します。このテストはMigrationを適用するため、本番DBを指定しないでください。

```bash
HOMESTOCK_POSTGRES_TEST_CONNECTION='Host=localhost;Database=homestock_test;Username=homestock;Password=...' dotnet test WebAPI.Tests/InventoryAPI.Tests.csproj
```

## 配備スモークテスト

本番Compose起動後、HTTPS、セキュリティヘッダー、相関ID、liveness/readiness、5432/8080の非公開を確認します。

```bash
sh ./deploy/smoke-test.sh https://stock.example.com
docker compose -f compose.yaml -f compose.production.yaml logs --tail=100 api caddy
```

本番APIログはJSON形式で相関IDを含み、Composeのログローテーションは1ファイル10MB、最大5ファイルです。
