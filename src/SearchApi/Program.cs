using System;
var keywords = new[] { "apple", "banana", "grape", "orange", "pineapple" };
var app = WebApplication.CreateBuilder(args).Build();
app.MapGet("/", () => "Keyword search API");
app.MapGet("/search", (string query) =>
{
    var matches = keywords.Where(k => k.Contains(query, StringComparison.OrdinalIgnoreCase));
    return Results.Json(matches);
});
app.MapGet("/test", () =>
{
    const string html = """
        <!doctype html>
        <html lang="ja">
          <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>Search API テスト</title>
            <style>
              body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; margin: 2rem; }
              form { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
              input { flex: 1; padding: 0.5rem; font-size: 1rem; }
              button { padding: 0.5rem 1rem; font-size: 1rem; }
              ul { padding-left: 1.25rem; }
              .hint { color: #555; font-size: 0.9rem; }
            </style>
          </head>
          <body>
            <h1>Search API テスト</h1>
            <p class="hint">キーワードを入力して検索結果を確認できます。</p>
            <form id="search-form">
              <input id="query" name="query" placeholder="例: app" autocomplete="off" />
              <button type="submit">検索</button>
            </form>
            <ul id="results"></ul>
            <script>
              const form = document.getElementById("search-form");
              const queryInput = document.getElementById("query");
              const results = document.getElementById("results");

              async function runSearch(query) {
                results.innerHTML = "";
                if (!query) {
                  return;
                }
                const response = await fetch(`/search?query=${encodeURIComponent(query)}`);
                const data = await response.json();
                if (data.length === 0) {
                  results.innerHTML = "<li>該当なし</li>";
                  return;
                }
                for (const item of data) {
                  const li = document.createElement("li");
                  li.textContent = item;
                  results.appendChild(li);
                }
              }

              form.addEventListener("submit", (event) => {
                event.preventDefault();
                runSearch(queryInput.value.trim());
              });
            </script>
          </body>
        </html>
        """;
    return Results.Content(html, "text/html; charset=utf-8");
});
app.Run();
