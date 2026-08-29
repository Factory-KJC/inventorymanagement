# Home Stock Print Worker

APIの印刷ジョブを取得し、保存済みreceiptline文書をESC/POSへ変換してネットワークプリンタへ送信します。`asImage: false`のため、文字はプリンタのデバイスフォントで印刷され、ラスター画像は送信しません。

Docker Composeでは`printing`プロファイルを指定します。

```powershell
docker compose --profile printing up --build
```

`.env`には推測困難な`PRINT_WORKER_API_KEY`と、家庭内LAN上の`PRINTER_HOST`を設定してください。既定ポートは`9100`です。紙切れや通信断で失敗したジョブはAPI上で`Failed`となり、再印刷APIから同一内容を再キュー投入できます。送信試行はジョブごとに最大3回です。

印刷直前には`event`が`receiptline-preview`のJSONログを出力します。`receiptLine`フィールドの文字列を外部のReceiptLine Designerへ貼り付けてプレビューできます。続く`escpos-generated`ログには生成したESC/POSデータのバイト数だけを記録し、バイナリ本体はログへ出しません。

```powershell
docker compose logs print-worker
```
