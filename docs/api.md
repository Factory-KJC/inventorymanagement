# API利用ガイド

## Phase 1で利用できる機能

- 初回利用者の登録とJWTログイン
- 商品の登録、編集、一覧、JANコード検索
- 保管場所の登録、一覧
- 期限・保管場所単位の入庫
- 期限が近いロットからの自動消費・廃棄（FEFO）
- ロット単位の棚卸調整
- 現在庫と入出庫履歴の取得
- 冪等な入庫・消費・廃棄・棚卸調整
- 補充点からの買い物リスト提案、手動追加、購入状態更新
- 完了した買い物リストの購入済み商品を実在庫へ一括入庫
- ダッシュボード集計

Swaggerは開発環境の`/swagger`で確認できます。具体的なリクエストは[InventoryAPI.http](../WebAPI/InventoryAPI.http)にも収録しています。

## 商品編集

`PATCH /api/products/{id}`へ商品名、JANコード、単位、補充点、目標在庫を送ると、商品マスターを更新します。このAPIは全編集項目を受け取ります。JANコードの登録を解除する場合は`barcode`に`null`を指定してください。別の商品が使用しているJANコードは指定できません。

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

## エラー

エラーはProblem Details（`application/problem+json`）で返します。

| HTTP | 意味 |
|---|---|
| 400 | 入力またはIdempotency-Keyが不正 |
| 401 | 未認証またはトークン期限切れ |
| 404 | 商品または保管場所が存在しない |
| 409 | JANコード重複、保管場所名重複、在庫不足 |
