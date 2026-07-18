using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Authentication;

public sealed class ProviderCredentialAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ProviderCredentialAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                GenerationApiRoutes.ProviderApiKeyHeaderName,
                out Microsoft.Extensions.Primitives.StringValues values))
        {
            Logger.LogDebug(
                "Provider credential header is missing.");

            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string? providerCredential = values.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providerCredential))
        {
            Logger.LogWarning(
                "Provider credential header was provided without a value.");

            return Task.FromResult(AuthenticateResult.Fail("Provider credential header is empty."));
        }

        Logger.LogDebug(
            "Provider credential header was accepted without retaining its value.");

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, "provider-credential")
        ];
        ClaimsIdentity identity = new(
            claims,
            ProviderCredentialAuthenticationDefaults.SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(
            principal,
            ProviderCredentialAuthenticationDefaults.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
