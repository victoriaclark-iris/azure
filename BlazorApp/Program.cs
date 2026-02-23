using BlazorApp.Components;
using BlazorApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// Simple authentication setup - Easy Auth only
builder.Services.AddAuthentication()
    .AddCookie(options =>
    {
        options.Cookie.Name = "AppAuth";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
    });

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
app.UseAuthentication();

// Easy Auth middleware for all environments
app.Use(async (context, next) =>
{
    var hasPrincipalHeader = context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var headerValue);
    var hasPrincipalName = context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL-NAME", out var principalName) &&
                          !string.IsNullOrWhiteSpace(principalName.ToString());

    Console.WriteLine($"Easy Auth Debug: hasPrincipalHeader={hasPrincipalHeader}, hasPrincipalName={hasPrincipalName}");
    Console.WriteLine($"Principal Name Header: {principalName}");

    if (hasPrincipalHeader && hasPrincipalName)
    {
        try
        {
            Console.WriteLine("Processing Easy Auth headers...");
            var encodedPrincipal = headerValue.ToString();
            encodedPrincipal = encodedPrincipal.Replace('-', '+').Replace('_', '/');
            encodedPrincipal = encodedPrincipal.PadRight(encodedPrincipal.Length + (4 - encodedPrincipal.Length % 4) % 4, '=');

            var decodedBytes = Convert.FromBase64String(encodedPrincipal);
            var decodedJson = Encoding.UTF8.GetString(decodedBytes);
            Console.WriteLine($"Decoded JSON: {decodedJson}");

            using var principalDocument = JsonDocument.Parse(decodedJson);
            var claims = new List<Claim>();

            // Add a basic name claim from the header
            claims.Add(new Claim("name", principalName.ToString()));
            claims.Add(new Claim("sub", principalName.ToString()));

            if (principalDocument.RootElement.TryGetProperty("claims", out var claimsElement))
            {
                foreach (var claimElement in claimsElement.EnumerateArray())
                {
                    if (claimElement.TryGetProperty("typ", out var typeProperty) && 
                        claimElement.TryGetProperty("val", out var valueProperty))
                    {
                        var claimType = typeProperty.GetString();
                        var claimValue = valueProperty.GetString();

                        if (!string.IsNullOrWhiteSpace(claimType) && claimValue is not null)
                        {
                            claims.Add(new Claim(claimType, claimValue));
                        }
                    }
                }
            }

            Console.WriteLine($"Created {claims.Count} claims");
            var identity = new ClaimsIdentity(claims, "EasyAuth");
            context.User = new ClaimsPrincipal(identity);
            Console.WriteLine($"Set user identity: {context.User.Identity.IsAuthenticated}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing Easy Auth: {ex.Message}");
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
    else
    {
        Console.WriteLine("No Easy Auth headers found, setting anonymous user");
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    await next();
});

app.UseAuthorization();

// Debug endpoint to check authentication status
app.MapGet("/debug/auth", (HttpContext httpContext) =>
{
    var authInfo = new
    {
        IsAuthenticated = httpContext.User.Identity?.IsAuthenticated,
        Name = httpContext.User.Identity?.Name,
        AuthenticationType = httpContext.User.Identity?.AuthenticationType,
        Claims = httpContext.User.Claims.Select(c => new { c.Type, c.Value }).ToArray(),
        Headers = httpContext.Request.Headers
            .Where(h => h.Key.StartsWith("X-MS-CLIENT"))
            .ToDictionary(h => h.Key, h => h.Value.ToString()),
        Environment = app.Environment.EnvironmentName
    };
    
    return Results.Json(authInfo);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
