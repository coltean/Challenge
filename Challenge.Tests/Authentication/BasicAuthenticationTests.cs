using Challenge.API.Data;
using Challenge.API.Models.Dto;
using Challenge.API.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace Challenge.Tests.Authentication
{
    /// <summary>
    /// Comprehensive tests for Basic Authentication mechanism.
    /// Tests valid/invalid credentials and role-based access control.
    /// </summary>
    public class BasicAuthenticationTests : IClassFixture<Challenge.Tests.Authentication.TestWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public BasicAuthenticationTests(TestWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        #region Valid Credentials Tests

        [Fact]
        public async Task WebhookEndpoint_WithValidCmsCredentials_ReturnsAccepted()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cms/events");
            var credentials = "cmswh_challenge:a1b2c3d4-e5f6-7890-abcd-ef1234567890";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            request.Content = new StringContent(
                "[{\"type\":\"publish\",\"id\":\"test\",\"version\":1,\"payload\":{},\"timestamp\":\"2024-01-28T10:00:00Z\"}]",
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        [Fact]
        public async Task GetEntitiesEndpoint_WithValidApiUserCredentials_ReturnsOk()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Invalid Credentials Tests

        [Fact]
        public async Task AnyEndpoint_WithMissingAuthorizationHeader_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            // No Authorization header

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task WebhookEndpoint_WithInvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cms/events");
            var credentials = "cmswh_challenge:wrong-password";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            request.Content = new StringContent("[]", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task WebhookEndpoint_WithInvalidUsername_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cms/events");
            var credentials = "nonexistent_user:a1b2c3d4-e5f6-7890-abcd-ef1234567890";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            request.Content = new StringContent("[]", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AnyEndpoint_WithIncorrectAuthScheme_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", base64Credentials); // Wrong scheme

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AnyEndpoint_WithMalformedBase64_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "not-valid-base64!@#$");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AnyEndpoint_WithMissingColon_InCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "apiuser_demo-no-colon-password"; // Missing colon separator
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Role-Based Access Control Tests

        [Fact]
        public async Task WebhookEndpoint_WithApiUserCredentials_ReturnsForbidden()
        {
            // Arrange - API user trying to access webhook (requires CMS_WEBHOOK role)
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cms/events");
            var credentials = "apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            request.Content = new StringContent("[]", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task WebhookEndpoint_WithAdminCredentials_ReturnsForbidden()
        {
            // Arrange - Admin trying to access webhook (requires CMS_WEBHOOK role)
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cms/events");
            var credentials = "admin:12345678-1234-1234-1234-123456789012";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            request.Content = new StringContent("[]", Encoding.UTF8, "application/json");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task AdminDisableEndpoint_WithApiUserCredentials_ReturnsForbidden()
        {
            // Arrange - Regular user trying to disable entity (requires ADMIN role)
            var request = new HttpRequestMessage(HttpMethod.Put, "/api/entities/test-id/disable");
            var credentials = "apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task AdminDisableEndpoint_WithAdminCredentials_ReturnsNotFound()
        {
            // Arrange - Admin user accessing disable endpoint (correct role, but entity doesn't exist)
            var request = new HttpRequestMessage(HttpMethod.Put, "/api/entities/nonexistent-id/disable");
            var credentials = "admin:12345678-1234-1234-1234-123456789012";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound); // Not Forbidden
        }

        #endregion

        #region Case Sensitivity Tests

        [Fact]
        public async Task Endpoint_CredentialUsername_IsCaseSensitive()
        {
            // Arrange - Username with different case
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "APIUSER_DEMO:f0e9d8c7-b6a5-4321-8765-fedcba987654"; // uppercase
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert - Should fail (case sensitive)
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Endpoint_AuthScheme_IsCaseSensitive()
        {
            // Arrange - Scheme with different case
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("basic", base64Credentials); // lowercase

            // Act
            var response = await _client.SendAsync(request);

            // Assert - Basic auth is case-insensitive for scheme typically, but implementation specific
            // The handler checks for "Basic", so lowercase "basic" might fail
            response.StatusCode.Should().NotBe(HttpStatusCode.OK); // Should either be 401 or 403
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public async Task Endpoint_WithEmptyPassword_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "apiuser_demo:"; // Empty password
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Endpoint_WithEmptyUsername_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = ":f0e9d8c7-b6a5-4321-8765-fedcba987654"; // Empty username
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Endpoint_WithWhitespaceInPassword_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "apiuser_demo:f0e9d8c7-b6a5-4321-8765-fedcba987654 "; // Extra space
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Multiple Colon Tests

        [Fact]
        public async Task Endpoint_WithMultipleColons_InCredentials_ParsesCorrectly()
        {
            // Arrange - Password contains colon (e.g., UUID with multiple separators concept)
            // The format should be username:password, so everything after first colon is password
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/entities");
            var credentials = "apiuser_demo:f0e9d8c7:b6a5:4321:8765:fedcba987654"; // Multiple colons in password
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            // Act
            var response = await _client.SendAsync(request);

            // Assert - Should fail because password doesn't match
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Credentials Test Data

        [Theory]
        [InlineData("cmswh_challenge", "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "/api/cms/events", true)]
        [InlineData("apiuser_demo", "f0e9d8c7-b6a5-4321-8765-fedcba987654", "/api/entities", true)]
        [InlineData("admin", "12345678-1234-1234-1234-123456789012", "/api/entities", true)]
        [InlineData("cmswh_challenge", "wrong-password", "/api/cms/events", false)]
        [InlineData("apiuser_demo", "wrong-password", "/api/entities", false)]
        public async Task Theory_CredentialsValidation(string username, string password, string endpoint, bool shouldSucceed)
        {
            // Arrange
            HttpMethod method = endpoint == "/api/cms/events" ? HttpMethod.Post : HttpMethod.Get;
            var request = new HttpRequestMessage(method, endpoint);
            var credentials = $"{username}:{password}";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            if (method == HttpMethod.Post)
            {
                request.Content = new StringContent(
                "[{\"type\":\"publish\",\"id\":\"test\",\"version\":1,\"payload\":{},\"timestamp\":\"2024-01-28T10:00:00Z\"}]",
                Encoding.UTF8,
                "application/json");
            }

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            if (shouldSucceed)
            {
                // Different endpoints return different success codes
                if (endpoint == "/api/cms/events")
                {
                    response.StatusCode.Should().Be(HttpStatusCode.Accepted);
                }
                else
                {
                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                }
            }
            else
            {
                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }
        }

        #endregion
    }

    public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                var hostedServices = services
                    .Where(descriptor => descriptor.ImplementationType == typeof(RabbitMqEventConsumer))
                    .ToList();

                foreach (var hostedService in hostedServices)
                {
                    services.Remove(hostedService);
                }

                services.RemoveAll<IEventQueueService>();
                services.AddSingleton<IEventQueueService, NoOpEventQueueService>();

                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ReadOnlyDbContext>>();

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("ChallengeTests"));

                services.AddDbContext<ReadOnlyDbContext>(options =>
                    options.UseInMemoryDatabase("ChallengeTests")
                        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
            });
        }
    }

    internal sealed class NoOpEventQueueService : IEventQueueService
    {
        public Task EnqueueEventsAsync(List<CmsEventDto> events) => Task.CompletedTask;
    }
}
