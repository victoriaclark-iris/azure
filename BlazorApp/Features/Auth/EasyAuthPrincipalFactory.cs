using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace BlazorApp.Features.Auth;

internal static class EasyAuthPrincipalFactory
{
    internal static ClaimsPrincipal CreateFromHeaders(IHeaderDictionary headers)
    {
        var hasPrincipalHeader = headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var headerValue);
        var hasPrincipalName = headers.TryGetValue("X-MS-CLIENT-PRINCIPAL-NAME", out var principalName) &&
                               !string.IsNullOrWhiteSpace(principalName.ToString());

        if (!hasPrincipalHeader || !hasPrincipalName)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        try
        {
            var encodedPrincipal = headerValue.ToString();
            encodedPrincipal = encodedPrincipal.Replace('-', '+').Replace('_', '/');
            encodedPrincipal = encodedPrincipal.PadRight(encodedPrincipal.Length + (4 - encodedPrincipal.Length % 4) % 4, '=');

            var decodedBytes = Convert.FromBase64String(encodedPrincipal);
            var decodedJson = Encoding.UTF8.GetString(decodedBytes);

            using var principalDocument = JsonDocument.Parse(decodedJson);
            var claims = new List<Claim>
            {
                new("name", principalName.ToString()),
                new("sub", principalName.ToString())
            };

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

            var identity = new ClaimsIdentity(claims, "EasyAuth");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
