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

PostgreSQLはホストへポート公開していません。DBを直接調査する場合は次を使用します。

```powershell
docker compose exec db psql -U homestock -d homestock
```

停止は`docker compose down`です。DBデータは名前付きVolumeに残ります。DBも消す`docker compose down --volumes`は開発データを破棄するため、明示的に初期化するときだけ使用します。

## Debianへ配置する

リポジトリをDebianへ配置し、`.env.example`から`.env`を作成します。本番では少なくとも以下を変更します。

```dotenv
ASPNETCORE_ENVIRONMENT=Production
POSTGRES_PASSWORD=<十分に長いランダム値>
JWT_KEY=<32バイト以上のランダム値>
PUBLIC_HOST=stock.example.com
WEB_ORIGIN=https://stock.example.com
```

秘密値は次のように生成できます。

```bash
openssl rand -base64 48
```

起動します。

```bash
docker compose -f compose.yaml -f compose.production.yaml up --build -d
docker compose -f compose.yaml -f compose.production.yaml ps
docker compose -f compose.yaml -f compose.production.yaml logs --tail=100 api caddy
```

CaddyがLet's Encrypt等からTLS証明書を自動取得します。ルーターまたはクラウド側では80/443だけをDebianへ通し、5432と8080は公開しません。`8080`はホストのloopbackにだけバインドされます。

## 更新

```bash
git pull --ff-only
docker compose -f compose.yaml -f compose.production.yaml up --build -d
docker image prune -f
```

更新前にPostgreSQLのバックアップを取得します。DBスキーマが正式なMigration管理へ移行するまでは破壊的なモデル変更を行いません。

## 現段階のDB初期化

現在の旧試作APIにはEF Core Migrationが存在しないため、`Database__EnsureCreated=true`で初回テーブルを作成します。新ドメインモデルを実装するPhase 1でMigrationへ切り替え、その時点で本番の`EnsureCreated`を無効化します。これは移行期間中の暫定措置です。

## バックアップ例

```bash
docker compose exec -T db pg_dump -U homestock -d homestock -Fc > homestock.dump
```

バックアップファイルはDebianホストだけに置き続けず、暗号化したうえで別媒体へ転送します。
