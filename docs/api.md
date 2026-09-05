# API利用ガイド

## Phase 1で利用できる機能

- セットアップトークンで保護した初回利用者登録、JWTログイン、更新トークンのローテーション
- 商品の登録、編集、一覧、JANコード検索
- 商品・保管場所の論理削除、削除済み候補検索、復元
- 保管場所の登録、編集、一覧
- 期限・保管場所単位の入庫
- 期限が近いロットからの自動消費・廃棄（FEFO）
- ロット単位の棚卸調整
- 現在庫と入出庫履歴の取得
- 冪等な入庫・消費・廃棄・棚卸調整
- 補充点からの買い物リスト提案、手動追加、購入状態更新
- 完了した買い物リストの購入済み商品を実在庫へ一括入庫
- 買い物リストの印刷用データ取得
- ダッシュボード集計

Swaggerは開発環境の`/swagger`で確認できます。具体的なリクエストは[InventoryAPI.http](../WebAPI/InventoryAPI.http)にも収録しています。

## 初回登録と認証

`POST /api/users/register`は、APIへ直接接続するループバック要求、または環境変数`SETUP_TOKEN`と一致する`X-Setup-Token`ヘッダーを持つ要求だけを受け付けます。登録後はこの値を環境から削除または別のランダム値へ交換してください。ユーザーが存在する場合、追加登録は`409 Conflict`です。

`POST /api/auth/login`は1つの送信元IPにつき1分間5回までです。成功時は15分有効の`token`、一度だけ交換に使える`refreshToken`、アクセストークンの`expiresAt`を返します。制限超過時は`429 Too Many Requests`です。

`POST /api/auth/refresh`へ`refreshToken`を送ると、新しいトークン組を返して使用済みトークンを失効させます。更新トークンの有効期間は30日で、DBにはSHA-256ハッシュだけを保存します。

## 商品編集

`PATCH /api/products/{id}`へ商品名、JANコード、単位、補充点、目標在庫を送ると、商品マスターを更新します。このAPIは全編集項目を受け取ります。JANコードの登録を解除する場合は`barcode`に`null`を指定してください。別の商品が使用しているJANコードは指定できません。

## 保管場所編集

`PATCH /api/locations/{id}`へ名称と表示順を送ると、保管場所を更新します。同じ世帯内で既に使われている名称は指定できません。更新後の名称は既存の在庫ロットにも反映されます。

## マスターの削除と復元

`DELETE /api/products/{id}`と`DELETE /api/locations/{id}`は論理削除です。在庫ロットと移動履歴は保持され、通常の商品・保管場所一覧と新規入庫の選択肢から削除済み項目だけを除外します。

再登録時の候補は`GET /api/products/deleted-suggestions?name={name}`または`GET /api/locations/deleted-suggestions?name={name}`で取得します。2文字未満は候補を返さず、2文字以上で削除済み名称との同一・部分一致を検索します。復元する場合は`POST /api/products/{id}/restore`または`POST /api/locations/{id}/restore`へ更新後の項目を送ります。

## 在庫コマンド

`POST /api/inventory/receive`、`consume`、`discard`、`adjust`には`Idempotency-Key`ヘッダーが必須です。クライアントで操作ごとにUUIDを生成し、通信失敗時は同じキーで再送してください。別操作には同じキーを再利用しません。

```http
Idempotency-Key: 381bbbe6-42de-4b27-a51a-bd5b75c95246
```

同じキーを再送した場合は、数量を再計上せず最初の操作結果を返します。

## 買い物完了からの入庫

店内では購入した項目を`Purchased`へ更新し、`POST /api/shopping-lists/current/complete`で
買い物リストを完了します。帰宅後、次のAPIで購入済み商品を実在庫へ変換します。

```http
POST /api/shopping-lists/{shoppingListId}/receive
Idempotency-Key: 5f5513db-5dc2-48d3-abaf-5844b496c5bf
Content-Type: application/json

{
  "items": [
    {
      "itemId": "購入済み項目のID",
      "locationId": "保管場所のID",
      "expiresOn": "2027-01-31"
    }
  ]
}
```

数量は買い物項目の確定数量を使用します。購入済みかつ商品マスターに紐付く全項目について、
保管場所と任意の期限を一度ずつ指定してください。商品に紐付かない自由入力項目は実在庫へ
変換されません。変換全体を一つのトランザクションと入庫操作として記録し、成功後のリストは
`Received`になります。同じ`Idempotency-Key`の再送は二重計上せず、別のキーによる再変換は
`409 Conflict`になります。

## 買い物リストの印刷用データ

`GET /api/shopping-lists/current/print`は、現在有効な買い物リストから未購入の項目だけを返します。購入済み・却下済みの項目は含まれません。商品に紐付く項目には商品マスターの単位が入り、自由入力項目の`unit`は`null`になります。リストがまだ作成されていない場合は`404 Not Found`を返します。

```json
{
  "shoppingListId": "買い物リストのID",
  "createdAt": "2026-08-29T00:00:00+00:00",
  "generatedAt": "2026-08-29T01:00:00+00:00",
  "items": [
    {
      "itemId": "買い物項目のID",
      "name": "洗剤",
      "quantity": 2,
      "unit": "本",
      "source": "ReorderSuggestion"
    }
  ]
}
```

`generatedAt`は印刷用データを取得したUTC時刻です。APIは用紙幅やフォントなどのレイアウトを持たず、Windowsクライアントや将来のPrint Workerがこのデータを58mm・80mm等の出力形式へ整形します。

## 印刷ジョブ

`POST /api/shopping-lists/current/print-jobs`へ`{"paperWidth":"Mm80"}`（または`Mm58`）を送ると、未購入項目をreceiptline文書へ整形した不変のスナップショットを作成し、`202 Accepted`を返します。

`GET /api/print-jobs/{id}`で`Pending`、`Processing`、`Succeeded`、`Failed`の状態、試行回数、直近のエラーを確認できます。`POST /api/print-jobs/{id}/retry`は保存済みreceiptline文書を変更せずに再度キューへ入れるため、買い物リストが後から変わっても同じ内容を再印刷します。プリンタへの送信試行はジョブごとに最大3回で、上限到達後の再印刷要求には`409 Conflict`を返します。

Print Worker用エンドポイントは`X-Print-Worker-Key`で保護されます。Workerはreceiptlineの`command: 'escpos'`、`encoding: 'shiftjis'`、`asImage: false`を使い、生成されたESC/POSバイト列をプリンタのTCP 9100番へ直接送信します。画像データへの変換は行いません。

Workerは印刷直前に`receiptline-preview`イベントとして、ジョブID、用紙幅、receiptline文書をJSONログへ出力します。この文書は外部のReceiptLine Designerへ貼り付けてプレビューできます。ESC/POSバイナリはログへ記録しません。

Workerは`GET /api/print-jobs/worker/events`のSSE通知を待ち、接続時と`print-job`イベント受信時だけ`worker/claim`を呼び出します。定期ポーリングは行いません。SSE接続が切断された場合は再接続し、接続直後の通知で停止中に作成されたジョブも取得します。

## JANコード

JANコードは先頭ゼロを保持するため文字列で送ります。8桁または13桁とチェックディジットを検証します。JANコードを持たない商品は`barcode`を省略できます。同一世帯内では一意です。

## ダッシュボード一覧

ダッシュボードの集計値は`GET /api/dashboard`で取得します。集計カードから開く一覧は次のAPIを使用します。

```http
GET /api/dashboard/{category}?page=1&pageSize=20
```

`category`には`products`、`low-stock`、`expiring`、`shopping`を指定します。`pageSize`はAPI側で最大20件に制限され、21件目以降は「次へ」で表示します。

## 消費順

ロットを明示せず商品と数量を指定します。期限ありを期限なしより優先し、期限が近いロットから消費します。指定数量に満たない場合は`409 Conflict`を返し、在庫は変更しません。保管場所を指定すると、その場所だけから消費します。

廃棄も同じFEFO順で減算し、移動種別を`Discard`として記録します。棚卸調整は在庫一覧で取得した`lotId`と実数の`countedQuantity`を指定し、変更前との差分を`Adjust`として記録します。実数には0も指定できます。

## 在庫操作の取消し

`POST /api/inventory/operations/{operationId}/reverse`へ`Idempotency-Key`ヘッダーと任意の`note`を送ると、指定した操作を逆仕訳で取り消します。元の履歴は変更せず、各移動の数量を反転した`Reverse`履歴を追加し、`reversesMovementId`で元の移動を参照します。

同じ冪等キーでの再送は最初の結果を返します。別の冪等キーで同じ操作を再度取り消した場合、`Reverse`操作自体を指定した場合、または入庫取消しによって現在庫が負になる場合は`409 Conflict`を返し、在庫と履歴は変更しません。

## エラー

エラーはProblem Details（`application/problem+json`）で返します。

| HTTP | 意味 |
|---|---|
| 400 | 入力またはIdempotency-Keyが不正 |
| 401 | 未認証またはトークン期限切れ |
| 404 | 商品または保管場所が存在しない |
| 409 | JANコード重複、保管場所名重複、在庫不足、取消し競合 |
