using BlazorApp.Components;
using BlazorApp.Services;
using Microsoft.Azure.Cosmos;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthorization();

// Configure Cosmos DB
var cosmosConnectionString = builder.Configuration.GetConnectionString("CosmosDb") ?? 
                           builder.Configuration["CosmosDb:ConnectionString"];
var cosmosDatabaseName = builder.Configuration["CosmosDb:DatabaseName"];
var cosmosContainerName = builder.Configuration["CosmosDb:ContainerName"];

if (!string.IsNullOrEmpty(cosmosConnectionString))
{
    builder.Services.AddSingleton(serviceProvider =>
    {
        return new CosmosClient(cosmosConnectionString);
    });

    builder.Services.AddSingleton<CosmosDbService>(serviceProvider =>
    {
        var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
        return new CosmosDbService(cosmosClient, cosmosDatabaseName, cosmosContainerName);
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var headerValue))
    {
        try
        {
            var encodedPrincipal = headerValue.ToString();
            encodedPrincipal = encodedPrincipal.Replace('-', '+').Replace('_', '/');
            encodedPrincipal = encodedPrincipal.PadRight(encodedPrincipal.Length + (4 - encodedPrincipal.Length % 4) % 4, '=');

            var decodedBytes = Convert.FromBase64String(encodedPrincipal);
            var decodedJson = Encoding.UTF8.GetString(decodedBytes);

            using var principalDocument = JsonDocument.Parse(decodedJson);
            var claims = new List<Claim>();

            if (principalDocument.RootElement.TryGetProperty("claims", out var claimsElement))
            {
                foreach (var claimElement in claimsElement.EnumerateArray())
                {
                    var claimType = claimElement.GetProperty("typ").GetString();
                    var claimValue = claimElement.GetProperty("val").GetString();

                    if (!string.IsNullOrWhiteSpace(claimType) && claimValue is not null)
                    {
                        claims.Add(new Claim(claimType, claimValue));
                    }
                }
            }

            if (claims.Count > 0)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "AppServiceEasyAuth"));
            }
        }
        catch
        {
        }
    }

    await next();
});

app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
