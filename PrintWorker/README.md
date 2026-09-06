# Home Stock Print Worker

APIの印刷ジョブを取得し、保存済みreceiptline文書をESC/POSへ変換してネットワークプリンタへ送信します。`asImage: false`のため、文字はプリンタのデバイスフォントで印刷され、ラスター画像は送信しません。

Docker Composeでは`printing`プロファイルを指定します。

```powershell
docker compose --profile printing up --build
```

## Dockerでのテスト

ホストへNode.jsやnpmをインストールせず、Dockerfileの`test`ステージでWorkerの全テストを実行できます。テスト用イメージと実運用の`runtime`ステージは分離されているため、実運用イメージにテストコードは含まれません。

Windows PowerShell:

```powershell
./PrintWorker/test-worker.ps1
```

Linux:

```sh
./PrintWorker/test-worker.sh
```

スクリプトを使わない場合:

```sh
docker build --target test --tag home-stock-print-worker-test:local PrintWorker
docker run --rm home-stock-print-worker-test:local
```

`.env`には推測困難な`PRINT_WORKER_API_KEY`と、家庭内LAN上の`PRINTER_HOST`を設定してください。既定ポートは`9100`です。紙切れや通信断で失敗したジョブはAPI上で`Failed`となり、再印刷APIから同一内容を再キュー投入できます。送信試行はジョブごとに最大3回です。

印刷直前には`event`が`receiptline-preview`のJSONログを出力します。`receiptLine`フィールドの文字列を外部のReceiptLine Designerへ貼り付けてプレビューできます。続く`escpos-generated`ログには生成したESC/POSデータのバイト数だけを記録し、バイナリ本体はログへ出しません。

WorkerはAPIのServer-Sent Events（SSE）へ常時接続し、印刷ジョブの追加通知を受けたときだけキューを取得します。接続直後にもキューを一度取得するため、APIやWorkerの再起動中に登録されたジョブも処理されます。定期ポーリングは行いません。

```powershell
docker compose logs print-worker
```
