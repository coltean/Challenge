using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace Challenge.API.Authentication
{
    /// <summary>
    /// Custom Basic Authentication options.
    /// Stores user-to-password and user-to-role mappings.
    /// </summary>
    public class BasicAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        /// Dictionary mapping username ? password
        /// </summary>
        public Dictionary<string, string> Users { get; set; } = new();

        /// <summary>
        /// Dictionary mapping username ? role
        /// Supported roles: "CMS_WEBHOOK", "API_USER", "ADMIN"
        /// </summary>
        public Dictionary<string, string> Roles { get; set; } = new();
    }

    /// <summary>
    /// Implementation of Basic Authentication handler.
    /// Parses Authorization header and validates credentials.
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
    {
        public BasicAuthenticationHandler(
            IOptionsMonitor<BasicAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                // Check if Authorization header exists
                if (!Request.Headers.ContainsKey("Authorization"))
                {
                    Logger.LogWarning("Authentication failed: Missing Authorization header");
                    return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
                }

                // Parse Authorization header
                var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);

                // Check scheme is Basic
                if (authHeader.Scheme != "Basic")
                {
                    Logger.LogWarning($"Authentication failed: Invalid scheme '{authHeader.Scheme}', expected 'Basic'");
                    return Task.FromResult(AuthenticateResult.Fail("Invalid authorization scheme"));
                }

                // Decode credentials
                var credentialBytes = Convert.FromBase64String(authHeader.Parameter ?? "");
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':');

                if (credentials.Length != 2)
                {
                    Logger.LogWarning("Authentication failed: Invalid credentials format");
                    return Task.FromResult(AuthenticateResult.Fail("Invalid credentials format"));
                }

                var username = credentials[0];
                var password = credentials[1];

                // Validate username and password
                if (!Options.Users.ContainsKey(username))
                {
                    Logger.LogWarning($"Authentication failed: Unknown username '{username}'");
                    return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));
                }

                if (Options.Users[username] != password)
                {
                    Logger.LogWarning($"Authentication failed: Invalid password for username '{username}'");
                    return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));
                }

                // Authentication successful - create claims principal
                var role = Options.Roles.GetValueOrDefault(username, "API_USER");

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, username),
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, role)
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                Logger.LogInformation($"Authentication successful for user '{username}' with role '{role}'");

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (FormatException ex)
            {
                Logger.LogWarning($"Authentication failed: Invalid Base64 encoding - {ex.Message}");
                return Task.FromResult(AuthenticateResult.Fail("Invalid credentials encoding"));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Authentication failed with unexpected error: {ex.Message}");
                return Task.FromResult(AuthenticateResult.Fail("Authentication failed"));
            }
        }
    }
}
