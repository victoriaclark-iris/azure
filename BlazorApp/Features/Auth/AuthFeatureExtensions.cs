using System.Security.Claims;

namespace BlazorApp.Features.Auth;

public static class AuthFeatureExtensions
{
    public static IApplicationBuilder UseEasyAuthPrincipal(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.User = EasyAuthPrincipalFactory.CreateFromHeaders(context.Request.Headers);
            await next();
        });

        return app;
    }

    public static IEndpointRouteBuilder MapAuthFeatureEndpoints(this IEndpointRouteBuilder endpoints, string environmentName)
    {
        endpoints.MapGet("/custom-logout", (HttpContext httpContext) =>
        {
            foreach (var cookie in httpContext.Request.Cookies)
            {
                if (cookie.Key.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                    cookie.Key.Contains("AppServiceAuth", StringComparison.OrdinalIgnoreCase))
                {
                    httpContext.Response.Cookies.Delete(cookie.Key);
                }
            }

            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            return Results.Redirect("/signed-out");
        });

        endpoints.MapGet("/debug/auth", (HttpContext httpContext) =>
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
                Environment = environmentName
            };

            return Results.Json(authInfo);
        });

        return endpoints;
    }
}
