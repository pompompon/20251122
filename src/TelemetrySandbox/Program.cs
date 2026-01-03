using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("dependency", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

var apiKey = Environment.GetEnvironmentVariable("DEMO_API_KEY");
var requireApiKey = !string.IsNullOrWhiteSpace(apiKey);

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["x-trace-id"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });

    await next();
});

if (requireApiKey)
{
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase),
        apiApp =>
        {
            apiApp.Use(async (context, next) =>
            {
                if (!context.Request.Headers.TryGetValue("x-demo-key", out var provided) || provided != apiKey)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        ok = false,
                        message = "Missing or invalid x-demo-key",
                        traceId = context.TraceIdentifier
                    });
                    return;
                }

                await next();
            });
        });
}

app.MapGet("/", (HttpContext context) =>
{
    var environmentInfo = new
    {
        environmentName = app.Environment.EnvironmentName,
        machineName = Environment.MachineName,
        instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? "n/a",
        processId = Environment.ProcessId,
        appVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
    };

    var envJson = JsonSerializer.Serialize(environmentInfo);
    var apiKeyInput = requireApiKey
        ? """
            <label>
              DEMO API Key
              <input id=\"api-key\" type=\"password\" placeholder=\"x-demo-key\" />
            </label>
          """
        : "";

    var html = $$"""
        <!doctype html>
        <html lang=\"ja\">
          <head>
            <meta charset=\"utf-8\" />
            <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
            <title>Telemetry Sandbox</title>
            <style>
              body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; margin: 2rem; background: #f7f7f8; color: #1b1b1b; }
              h1 { margin-bottom: 0.5rem; }
              .note { margin: 0.5rem 0 1.5rem; color: #a60000; font-weight: 600; }
              .panel { background: white; padding: 1rem 1.5rem; border-radius: 12px; box-shadow: 0 8px 20px rgba(0,0,0,0.08); margin-bottom: 1.5rem; }
              .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.75rem; }
              button { padding: 0.7rem 1rem; border: none; border-radius: 8px; background: #0b5fff; color: white; font-weight: 600; cursor: pointer; }
              button:hover { background: #094bcc; }
              label { display: flex; flex-direction: column; gap: 0.35rem; font-size: 0.9rem; }
              input { padding: 0.5rem; border-radius: 6px; border: 1px solid #ccc; }
              pre { background: #111827; color: #e5e7eb; padding: 1rem; border-radius: 10px; overflow-x: auto; }
              .meta { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.75rem; }
              .meta div { background: #f0f4ff; padding: 0.5rem 0.75rem; border-radius: 8px; font-size: 0.9rem; }
            </style>
          </head>
          <body>
            <h1>Telemetry Sandbox</h1>
            <p class=\"note\">注意：これは検証用アプリ。public公開しない想定</p>

            <section class=\"panel\">
              <h2>Environment</h2>
              <div class=\"meta\" id=\"env-info\"></div>
              <p>現在時刻: <span id=\"current-time\"></span></p>
              {{apiKeyInput}}
            </section>

            <section class=\"panel\">
              <h2>Actions</h2>
              <div class=\"grid\">
                <button data-endpoint=\"/api/log/info\">Info log</button>
                <button data-endpoint=\"/api/log/warn\">Warn log</button>
                <button data-endpoint=\"/api/log/error\">Error log</button>
                <button data-endpoint=\"/api/exception/handled\">Handled exception</button>
                <button data-endpoint=\"/api/exception/unhandled\">Unhandled exception</button>
                <button data-endpoint=\"/api/http/404\">Return 404</button>
                <button data-endpoint=\"/api/slow/2\">Slow response (2s)</button>
                <button data-endpoint=\"/api/slow/10\">Slow response (10s)</button>
                <button data-endpoint=\"/api/dependency\">Dependency call</button>
                <button data-endpoint=\"/api/burst?lines=100\">Burst logs (100 lines)</button>
              </div>
            </section>

            <section class=\"panel\">
              <h2>Result</h2>
              <div>Status: <span id=\"status\">-</span></div>
              <div>Elapsed: <span id=\"elapsed\">-</span> ms</div>
              <pre id=\"result\">クリックして結果を表示します。</pre>
            </section>

            <script>
              const envInfo = {{envJson}};
              const envContainer = document.getElementById("env-info");
              const currentTime = document.getElementById("current-time");
              const statusEl = document.getElementById("status");
              const elapsedEl = document.getElementById("elapsed");
              const resultEl = document.getElementById("result");
              const apiKeyInput = document.getElementById("api-key");

              function renderEnv() {
                envContainer.innerHTML = "";
                for (const [key, value] of Object.entries(envInfo)) {
                  const div = document.createElement("div");
                  div.textContent = `${key}: ${value}`;
                  envContainer.appendChild(div);
                }
              }

              function updateClock() {
                currentTime.textContent = new Date().toLocaleString();
              }

              function getHeaders() {
                const headers = { "Accept": "application/json" };
                if (apiKeyInput && apiKeyInput.value.trim().length > 0) {
                  headers["x-demo-key"] = apiKeyInput.value.trim();
                }
                return headers;
              }

              async function callApi(path) {
                statusEl.textContent = "...";
                elapsedEl.textContent = "...";
                resultEl.textContent = "Loading...";
                const start = performance.now();
                let response;
                try {
                  response = await fetch(path, { headers: getHeaders() });
                } catch (error) {
                  const elapsed = Math.round(performance.now() - start);
                  statusEl.textContent = "fetch error";
                  elapsedEl.textContent = elapsed;
                  resultEl.textContent = error.toString();
                  return;
                }

                const elapsed = Math.round(performance.now() - start);
                const status = response.status;
                let bodyText = await response.text();
                let bodyOutput = bodyText;
                try {
                  const json = JSON.parse(bodyText);
                  bodyOutput = JSON.stringify(json, null, 2);
                } catch {
                  if (bodyText.length === 0) {
                    bodyOutput = "(empty)";
                  }
                }

                statusEl.textContent = status;
                elapsedEl.textContent = elapsed;
                resultEl.textContent = bodyOutput;
              }

              document.querySelectorAll("button[data-endpoint]").forEach((button) => {
                button.addEventListener("click", () => callApi(button.dataset.endpoint));
              });

              renderEnv();
              updateClock();
              setInterval(updateClock, 1000);
            </script>
          </body>
        </html>
        """;

    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/api/log/info", (HttpContext context, ILogger<Program> logger) =>
{
    var traceId = context.TraceIdentifier;
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
    logger.LogInformation("TraceId {TraceId} Test Info log at {Time}", traceId, DateTimeOffset.UtcNow);
    return Results.Json(new { ok = true, type = "info", traceId });
});

app.MapGet("/api/log/warn", (HttpContext context, ILogger<Program> logger) =>
{
    var traceId = context.TraceIdentifier;
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
    logger.LogWarning("TraceId {TraceId} Test Warn log at {Time}", traceId, DateTimeOffset.UtcNow);
    return Results.Json(new { ok = true, type = "warn", traceId });
});

app.MapGet("/api/log/error", (HttpContext context, ILogger<Program> logger) =>
{
    var traceId = context.TraceIdentifier;
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
    logger.LogError("TraceId {TraceId} Test Error log at {Time}", traceId, DateTimeOffset.UtcNow);
    return Results.Json(new { ok = true, type = "error", traceId });
});

app.MapGet("/api/exception/handled", (HttpContext context, ILogger<Program> logger) =>
{
    var traceId = context.TraceIdentifier;
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });

    try
    {
        throw new InvalidOperationException("Telemetry sandbox handled exception");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "TraceId {TraceId} Handled exception", traceId);
        return Results.Json(new { ok = true, handled = true, traceId });
    }
});

app.MapGet("/api/exception/unhandled", (HttpContext context, ILogger<Program> logger) =>
{
    var traceId = context.TraceIdentifier;
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });
    logger.LogWarning("TraceId {TraceId} About to throw unhandled exception", traceId);
    throw new InvalidOperationException("Telemetry sandbox unhandled exception");
});

app.MapGet("/api/http/404", (HttpContext context) =>
{
    var traceId = context.TraceIdentifier;
    return Results.NotFound(new { ok = false, message = "Not Found", traceId });
});

app.MapGet("/api/slow/{seconds:int}", async (HttpContext context, int seconds) =>
{
    var traceId = context.TraceIdentifier;
    if (seconds < 1 || seconds > 30)
    {
        return Results.BadRequest(new { ok = false, message = "seconds must be between 1 and 30", traceId });
    }

    var stopwatch = Stopwatch.StartNew();
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    stopwatch.Stop();

    return Results.Json(new
    {
        ok = true,
        delaySeconds = seconds,
        elapsedMs = stopwatch.ElapsedMilliseconds,
        traceId
    });
});

app.MapGet("/api/dependency", async (HttpContext context, IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
{
    var traceId = context.TraceIdentifier;
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });

    var stopwatch = Stopwatch.StartNew();
    try
    {
        var client = httpClientFactory.CreateClient("dependency");
        using var response = await client.GetAsync("https://example.com/");
        stopwatch.Stop();

        logger.LogInformation("TraceId {TraceId} Dependency call completed with {StatusCode}", traceId, (int)response.StatusCode);

        return Results.Json(new
        {
            ok = true,
            statusCode = (int)response.StatusCode,
            elapsedMs = stopwatch.ElapsedMilliseconds,
            traceId
        });
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        logger.LogError(ex, "TraceId {TraceId} Dependency call failed", traceId);
        return Results.Json(new
        {
            ok = false,
            error = ex.Message,
            elapsedMs = stopwatch.ElapsedMilliseconds,
            traceId
        });
    }
});

app.MapGet("/api/burst", (HttpContext context, ILogger<Program> logger) =>
{
    var traceId = context.TraceIdentifier;
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });

    var lines = 100;
    if (int.TryParse(context.Request.Query["lines"], out var parsed))
    {
        lines = parsed;
    }

    if (lines < 1 || lines > 500)
    {
        return Results.BadRequest(new { ok = false, message = "lines must be between 1 and 500", traceId });
    }

    for (var i = 1; i <= lines; i++)
    {
        logger.LogInformation("TraceId {TraceId} Burst log {Line}/{Total}", traceId, i, lines);
    }

    return Results.Json(new { ok = true, lines, traceId });
});

app.MapGet("/api/health", (HttpContext context) =>
{
    var traceId = context.TraceIdentifier;
    return Results.Json(new { ok = true, traceId });
});

app.MapGet("/api/env", (HttpContext context) =>
{
    var traceId = context.TraceIdentifier;
    return Results.Json(new
    {
        environmentName = app.Environment.EnvironmentName,
        machineName = Environment.MachineName,
        processId = Environment.ProcessId,
        utcNow = DateTimeOffset.UtcNow,
        appVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
        traceId
    });
});

app.Run();
