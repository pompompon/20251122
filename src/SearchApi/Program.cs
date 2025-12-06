using System;
var keywords = new[] { "apple", "banana", "grape", "orange", "pineapple" };
var app = WebApplication.CreateBuilder(args).Build();
app.MapGet("/", () => "Keyword search API");
app.MapGet("/search", (string query) =>
{
    var matches = keywords.Where(k => k.Contains(query, StringComparison.OrdinalIgnoreCase));
    return Results.Json(matches);
});
app.Run();
