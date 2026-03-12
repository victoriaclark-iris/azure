using BlazorApp.Components;
using BlazorApp.Features.Auth;
using BlazorApp.Features.Calendar;
using BlazorApp.Features.Messages;
using BlazorApp.Features.Todo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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
        options.LoginPath = "/.auth/login/okta";
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

    builder.Services.AddSingleton<TodoService>(serviceProvider =>
    {
        var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
        return new TodoService(cosmosClient, cosmosDatabaseName, cosmosContainerName);
    });

    builder.Services.AddSingleton<CalendarService>(serviceProvider =>
    {
        var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
        return new CalendarService(cosmosClient, cosmosDatabaseName, cosmosContainerName);
    });

    // In-memory message service - no Cosmos DB required
    builder.Services.AddSingleton<MessageService>();
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

app.UseEasyAuthPrincipal();

app.UseAuthorization();

app.MapAuthFeatureEndpoints(app.Environment.EnvironmentName);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
