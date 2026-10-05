using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.WinGet.RestSource.Server.Middleware;
using Microsoft.WinGet.RestSource.Server.Services;
using Microsoft.WinGet.RestSource.Sqlite;
using Microsoft.WinGet.RestSource.Utils.Common;
using Microsoft.WinGet.RestSource.Utils.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Set ServerIdentifier from config/environment
var serverIdentifier = builder.Configuration["ServerIdentifier"] ?? "winget-restsource-self-hosted";
Environment.SetEnvironmentVariable(ApiConstants.ServerIdentifierEnvName, serverIdentifier);

// Database
var dbPath = builder.Configuration["Database:Path"] ?? "data/winget.db";
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
{
    Directory.CreateDirectory(dbDir);
}

builder.Services.AddSingleton<IApiDataStore>(sp =>
    new SqliteDataStore(
        sp.GetRequiredService<ILogger<SqliteDataStore>>(),
        dbPath));

builder.Services.AddScoped<PackageCatalogService>();

// API key auth filter
builder.Services.AddScoped<ApiKeyAuthFilter>();

// Controllers with Newtonsoft JSON
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        options.SerializerSettings.ContractResolver = new DefaultContractResolver();
        options.SerializerSettings.Formatting = Formatting.None;
    });

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<Microsoft.WinGet.RestSource.Server.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", async (IApiDataStore store) =>
{
    await store.Count();
    return Results.Ok(new { status = "ok" });
});

app.Run();

/// <summary>
/// Exposes the implicit Program type to WebApplicationFactory tests.
/// </summary>
public partial class Program
{
}
