# Telemetry Sandbox (.NET 8 Minimal API)

Azure App Service 上でログや Application Insights の挙動を短時間で確認するための検証用 API です。

## ローカル実行

```bash
dotnet run
```

起動後に `http://localhost:5000/` (または表示される URL) を開き、ボタンから各 API を呼び出します。

## 主要 URL

* `/` - テスト UI
* `/api/log/info`
* `/api/log/warn`
* `/api/log/error`
* `/api/exception/handled`
* `/api/exception/unhandled`
* `/api/http/404`
* `/api/slow/{seconds}` (1〜30)
* `/api/dependency`
* `/api/burst?lines=100` (1〜500)
* `/api/health`
* `/api/env`

## Azure App Service でのログ確認 (簡易)

1. App Service の「ログ」もしくは「診断ログ」を有効化します。
2. Application Insights を有効化すると、依存関係や例外のテレメトリが自動送信されます。
3. `Info/Warn/Error` ボタンや例外ボタンを押してログが出るか確認します。

## DEMO_API_KEY の使い方

`DEMO_API_KEY` を環境変数に設定すると、`/api/**` へのアクセスに `x-demo-key` ヘッダが必須になります。

```bash
export DEMO_API_KEY="your-key"
```

UI には API キー入力欄が表示されるので、そこに入力して実行してください。

## 備考

* すべての API 応答に `traceId` を含め、レスポンスヘッダにも `x-trace-id` を付与しています。
