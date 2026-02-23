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

var useLocalOidc = builder.Environment.IsDevelopment() &&
                   !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Okta:Authority"]) &&
                   !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Okta:ClientId"]) &&
                   !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Okta:ClientSecret"]);

if (useLocalOidc)
{
    // Development mode - use Cookie + OIDC
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.LogoutPath = "/account/logout";
        });

    builder.Services.AddAuthentication()
        .AddOpenIdConnect("oidc", options =>
        {
            options.Authority = builder.Configuration["Authentication:Okta:Authority"];
            options.ClientId = builder.Configuration["Authentication:Okta:ClientId"];
            options.ClientSecret = builder.Configuration["Authentication:Okta:ClientSecret"];
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.SaveTokens = true;
            options.CallbackPath = builder.Configuration["Authentication:Okta:CallbackPath"] ?? "/signin-oidc";
            options.SignedOutCallbackPath = "/signout-callback-oidc";
            options.GetClaimsFromUserInfoEndpoint = true;

            // Simplified sign-out configuration
            options.SignedOutRedirectUri = "/signed-out";
            
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            // Simplified event handling with logging
            options.Events = new OpenIdConnectEvents
            {
                OnRemoteFailure = context =>
                {
                    Console.WriteLine($"OIDC Remote Failure: {context.Failure?.Message}");
                    context.Response.Redirect("/signed-out");
                    context.HandleResponse();
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"OIDC Auth Failed: {context.Exception?.Message}");
                    context.Response.Redirect("/signed-out");
                    context.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    // Production mode - let Easy Auth handle everything
    builder.Services.AddAuthentication()
        .AddCookie(options =>
        {
            // Remove custom paths - let Easy Auth handle login/logout completely
            options.Cookie.Name = "AppAuth";
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            // Don't set LoginPath or LogoutPath at all
        });
}

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

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var hasPrincipalHeader = context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var headerValue);
        var hasPrincipalName = context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL-NAME", out var principalName) &&
                              !string.IsNullOrWhiteSpace(principalName.ToString());

        if (hasPrincipalHeader && hasPrincipalName)
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
                else
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity());
                }
            }
            catch
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }
        else
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        await next();
    });
}

app.UseAuthorization();

app.MapGet("/account/login", async (HttpContext httpContext, string? returnUrl) =>
{
    var safePath = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/')
        ? returnUrl
        : "/";

    if (useLocalOidc)
    {
        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = safePath },
            new[] { "oidc" });
    }

    // For production with Easy Auth, redirect directly to Easy Auth login
    var absoluteReturnUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{safePath}";
    var encodedReturnUrl = Uri.EscapeDataString(absoluteReturnUrl);
    return Results.Redirect($"/.auth/login/okta?post_login_redirect_uri={encodedReturnUrl}");
});

app.MapGet("/account/logout", async (HttpContext httpContext) =>
{
    if (useLocalOidc)
    {
        // For development, do a simple local sign-out
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/signed-out");
    }

    // For production, redirect straight to Easy Auth logout - no custom logic
    return Results.Redirect("/.auth/logout");
});

// Simple direct logout for production troubleshooting
app.MapGet("/logout", (HttpContext httpContext) =>
{
    return Results.Redirect("/.auth/logout");
});

// Add a backup logout endpoint for troubleshooting
app.MapGet("/account/logout-direct", async (HttpContext httpContext) =>
{
    // Clear any local authentication
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    
    if (!app.Environment.IsDevelopment())
    {
        // In production, redirect to Easy Auth logout
        var redirectUri = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/signed-out";
        var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
        return Results.Redirect($"/.auth/logout?post_logout_redirect_uri={encodedRedirectUri}");
    }
    
    return Results.Redirect("/signed-out");
});

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
        Environment = app.Environment.EnvironmentName,
        UseLocalOidc = useLocalOidc
    };
    
    return Results.Json(authInfo);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
