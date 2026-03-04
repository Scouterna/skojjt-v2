using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;
using System.Security.Claims;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Extension methods to configure SimpleSAML-based ScoutID (SAML 2.0) authentication.
/// Reads configuration from the "ScoutIdSaml" section in appsettings.json.
///
/// The SimpleSAML-based ScoutID is the current production version used by Scouterna.
/// It provides SAML attributes (uid, email, displayName, role, group_id, etc.) that
/// are normalized by <see cref="SamlClaimsNormalizer"/> so the downstream
/// <see cref="ScoutIdClaimsTransformation"/> works identically to the OIDC path.
/// </summary>
public static class SamlAuthenticationExtensions
{
    /// <summary>
    /// The authentication scheme name used for SAML 2.0 authentication.
    /// </summary>
    public const string Saml2Scheme = "Saml2";

    /// <summary>
    /// Adds SAML 2.0 authentication configured for a SimpleSAML-based ScoutID IdP.
    /// </summary>
    public static AuthenticationBuilder AddScoutIdSaml(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        var samlSection = configuration.GetSection("ScoutIdSaml");

        builder.AddSaml2(Saml2Scheme, options =>
        {
            // Service Provider entity ID
            var spEntityId = samlSection["SpEntityId"]
                ?? throw new InvalidOperationException("ScoutIdSaml:SpEntityId is required when SAML is enabled");
            options.SPOptions.EntityId = new EntityId(spEntityId);
            options.SPOptions.ReturnUrl = new Uri("/", UriKind.Relative);

            // Identity Provider configuration
            var idpEntityId = samlSection["IdpEntityId"]
                ?? throw new InvalidOperationException("ScoutIdSaml:IdpEntityId is required when SAML is enabled");
            var idpSsoUrl = samlSection["IdpSsoUrl"]
                ?? throw new InvalidOperationException("ScoutIdSaml:IdpSsoUrl is required when SAML is enabled");

            var idp = new IdentityProvider(
                new EntityId(idpEntityId),
                options.SPOptions)
            {
                SingleSignOnServiceUrl = new Uri(idpSsoUrl),
                AllowUnsolicitedAuthnResponse = true,
                Binding = Saml2BindingType.HttpRedirect
            };

            // Load IdP metadata (contains the signing certificate)
            var metadataUrl = samlSection["IdpMetadataUrl"];
            if (!string.IsNullOrEmpty(metadataUrl))
            {
                idp.MetadataLocation = metadataUrl;
                idp.LoadMetadata = true;
            }

            // Single Logout Service URL
            var sloUrl = samlSection["IdpSloUrl"];
            if (!string.IsNullOrEmpty(sloUrl))
            {
                idp.SingleLogoutServiceUrl = new Uri(sloUrl);
            }

            options.IdentityProviders.Add(idp);

            // After the SAML response is processed, normalize claims so
            // ScoutIdClaimsTransformation works identically to the OIDC path.
            options.Notifications.AcsCommandResultCreated = (result, _) =>
            {
                if (result.Principal?.Identity is ClaimsIdentity identity)
                {
                    SamlClaimsNormalizer.Normalize(identity);
                }
            };
        });

        return builder;
    }
}
