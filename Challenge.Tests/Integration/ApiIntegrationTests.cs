using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Challenge.API;
using Challenge.API.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Challenge.Tests.Integration
{
    /// <summary>
    /// Integration tests for the complete API workflow.
    /// Tests webhook ingestion, entity retrieval, and admin operations.
    /// </summary>
    public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly WebApplicationFactory<Program> _factory;

        private const string CmsUsername = "cmswh_challenge";
        private const string CmsPassword = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        private const string ApiUsername = "apiuser_demo";
        private const string ApiPassword = "f0e9d8c7-b6a5-4321-8765-fedcba987654";
        private const string AdminUsername = "admin";
        private const string AdminPassword = "12345678-1234-1234-1234-123456789012";

        public ApiIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        #region Webhook Event Ingestion Tests

        [Fact]
        public async Task SendPublishEvent_CreatesEntity_AndIsRetrievableByUser()
        {
            // Arrange
            var eventBatch = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = "integration-test-1",
                    Version = 1,
                    Payload = new { title = "Integration Test Product", price = 99.99 },
                    Timestamp = DateTime.UtcNow
                }
            };

            // Act - Send event via webhook
            var webhookRequest = CreateWebhookRequest(eventBatch);
            var webhookResponse = await _client.SendAsync(webhookRequest);

            // Assert - Event accepted
            webhookResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

            // Act - Retrieve entity
            var getRequest = CreateApiRequest(HttpMethod.Get, "/api/entities/integration-test-1", ApiUsername, ApiPassword);
            await Task.Delay(100); // Small delay to ensure processing
            var getResponse = await _client.SendAsync(getRequest);

            // Assert - Entity retrieved
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await getResponse.Content.ReadAsStringAsync();
            content.Should().Contain("integration-test-1");
            content.Should().Contain("Integration Test Product");
        }

        [Fact]
        public async Task SendVersionUpdate_UpdatesEntityVersion_AndRetrievesLatest()
        {
            // Arrange
            var testId = "integration-test-update";

            // Publish v1
            var publishV1 = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "Original Title", price = 100 },
                    Timestamp = DateTime.UtcNow
                }
            };

            var webhookRequest1 = CreateWebhookRequest(publishV1);
            await _client.SendAsync(webhookRequest1);
            await Task.Delay(100);

            // Act - Publish v2
            var publishV2 = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 2,
                    Payload = new { title = "Updated Title", price = 90 },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                }
            };

            var webhookRequest2 = CreateWebhookRequest(publishV2);
            await _client.SendAsync(webhookRequest2);
            await Task.Delay(100);

            // Act - Retrieve entity
            var getRequest = CreateApiRequest(HttpMethod.Get, $"/api/entities/{testId}", ApiUsername, ApiPassword);
            var getResponse = await _client.SendAsync(getRequest);

            // Assert
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await getResponse.Content.ReadAsStringAsync();
            content.Should().Contain("Updated Title");
            content.Should().Contain("\"currentPublishedVersion\": 2");
        }

        [Fact]
        public async Task SendUnpublishEvent_DisablesVersion_EntityStillVisible()
        {
            // Arrange
            var testId = "integration-test-unpublish";

            // Publish v1
            var publishV1 = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "Will be unpublished" },
                    Timestamp = DateTime.UtcNow
                }
            };

            var webhookRequest1 = CreateWebhookRequest(publishV1);
            await _client.SendAsync(webhookRequest1);
            await Task.Delay(100);

            // Act - Unpublish v1
            var unpublish = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "unPublish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "Will be unpublished" },
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                }
            };

            var webhookRequest2 = CreateWebhookRequest(unpublish);
            await _client.SendAsync(webhookRequest2);
            await Task.Delay(100);

            // Act - Try to retrieve entity
            var getRequest = CreateApiRequest(HttpMethod.Get, $"/api/entities/{testId}", ApiUsername, ApiPassword);
            var getResponse = await _client.SendAsync(getRequest);

            // Assert - Entity not visible to user
            getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // But admin should see it
            var adminGetRequest = CreateApiRequest(HttpMethod.Get, $"/api/entities/{testId}", AdminUsername, AdminPassword);
            var adminGetResponse = await _client.SendAsync(adminGetRequest);
            adminGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SendDeleteEvent_RemovesEntity_NotRetrievable()
        {
            // Arrange
            var testId = "integration-test-delete";

            // Publish entity
            var publish = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "Will be deleted" },
                    Timestamp = DateTime.UtcNow
                }
            };

            var publishRequest = CreateWebhookRequest(publish);
            await _client.SendAsync(publishRequest);
            await Task.Delay(100);

            // Act - Delete entity
            var delete = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "delete",
                    Id = testId,
                    Timestamp = DateTime.UtcNow.AddSeconds(1)
                }
            };

            var deleteRequest = CreateWebhookRequest(delete);
            await _client.SendAsync(deleteRequest);
            await Task.Delay(100);

            // Act - Try to retrieve entity
            var getRequest = CreateApiRequest(HttpMethod.Get, $"/api/entities/{testId}", ApiUsername, ApiPassword);
            var getResponse = await _client.SendAsync(getRequest);

            // Assert - Entity not found
            getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

        #region User Access Control Tests

        [Fact]
        public async Task ApiUser_CanViewPublishedEntities()
        {
            // Arrange
            var testId = "integration-test-user-access";

            // Publish entity
            var publish = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "User Access Test" },
                    Timestamp = DateTime.UtcNow
                }
            };

            var publishRequest = CreateWebhookRequest(publish);
            await _client.SendAsync(publishRequest);
            await Task.Delay(100);

            // Act
            var getRequest = CreateApiRequest(HttpMethod.Get, "/api/entities", ApiUsername, ApiPassword);
            var getResponse = await _client.SendAsync(getRequest);

            // Assert
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await getResponse.Content.ReadAsStringAsync();
            content.Should().Contain(testId);
        }

        [Fact]
        public async Task ApiUser_CannotAccessAdminDisableEndpoint()
        {
            // Arrange
            var testId = "integration-test-admin-block";

            // Create entity first
            var publish = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "Admin Block Test" },
                    Timestamp = DateTime.UtcNow
                }
            };

            var publishRequest = CreateWebhookRequest(publish);
            await _client.SendAsync(publishRequest);
            await Task.Delay(100);

            // Act
            var disableRequest = CreateApiRequest(HttpMethod.Put, $"/api/entities/{testId}/disable", ApiUsername, ApiPassword);
            var disableResponse = await _client.SendAsync(disableRequest);

            // Assert
            disableResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        #endregion

        #region Admin Operations Tests

        [Fact]
        public async Task Admin_CanDisableEntity_MakingItInvisibleToUsers()
        {
            // Arrange
            var testId = "integration-test-admin-disable";

            // Create entity
            var publish = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "Admin Disable Test" },
                    Timestamp = DateTime.UtcNow
                }
            };

            var publishRequest = CreateWebhookRequest(publish);
            await _client.SendAsync(publishRequest);
            await Task.Delay(100);

            // Act - Admin disables
            var disableRequest = CreateApiRequest(HttpMethod.Put, $"/api/entities/{testId}/disable", AdminUsername, AdminPassword);
            var disableResponse = await _client.SendAsync(disableRequest);

            // Assert
            disableResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Verify user cannot see
            await Task.Delay(100);
            var userGetRequest = CreateApiRequest(HttpMethod.Get, $"/api/entities/{testId}", ApiUsername, ApiPassword);
            var userGetResponse = await _client.SendAsync(userGetRequest);
            userGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Verify admin can still see
            var adminGetRequest = CreateApiRequest(HttpMethod.Get, $"/api/entities/{testId}", AdminUsername, AdminPassword);
            var adminGetResponse = await _client.SendAsync(adminGetRequest);
            adminGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await adminGetResponse.Content.ReadAsStringAsync();
            content.Should().Contain("\"isDisabledByAdmin\": true");
        }

        [Fact]
        public async Task Admin_CanEnableDisabledEntity_MakingItVisibleAgain()
        {
            // Arrange
            var testId = "integration-test-admin-enable";

            // Create and disable entity
            var publish = new List<CmsEventDto>
            {
                new CmsEventDto
                {
                    Type = "publish",
                    Id = testId,
                    Version = 1,
                    Payload = new { title = "Admin Enable Test" },
                    Timestamp = DateTime.UtcNow
                }
            };

            var publishRequest = CreateWebhookRequest(publish);
            await _client.SendAsync(publishRequest);
            await Task.Delay(100);

            var disableRequest = CreateApiRequest(HttpMethod.Put, $"/api/entities/{testId}/disable", AdminUsername, AdminPassword);
            await _client.SendAsync(disableRequest);
            await Task.Delay(100);

            // Act - Admin enables
            var enableRequest = CreateApiRequest(HttpMethod.Put, $"/api/entities/{testId}/enable", AdminUsername, AdminPassword);
            var enableResponse = await _client.SendAsync(enableRequest);

            // Assert
            enableResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Verify user can see again
            await Task.Delay(100);
            var userGetRequest = CreateApiRequest(HttpMethod.Get, $"/api/entities/{testId}", ApiUsername, ApiPassword);
            var userGetResponse = await _client.SendAsync(userGetRequest);
            userGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Webhook_WithInvalidBatch_ReturnsBadRequest()
        {
            // Arrange
            var invalidBatch = "[{\"type\":\"invalid\"}]"; // Missing required fields

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cms/events");
            var credentials = $"{CmsUsername}:{CmsPassword}";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
            request.Content = new StringContent(invalidBatch, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetEntity_ForNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var getRequest = CreateApiRequest(HttpMethod.Get, "/api/entities/nonexistent-id-12345", ApiUsername, ApiPassword);

            // Act
            var response = await _client.SendAsync(getRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

        #region Helper Methods

        private HttpRequestMessage CreateWebhookRequest(List<CmsEventDto> events)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cms/events");
            var credentials = $"{CmsUsername}:{CmsPassword}";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            var json = JsonSerializer.Serialize(events);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return request;
        }

        private HttpRequestMessage CreateApiRequest(HttpMethod method, string endpoint, string username, string password)
        {
            var request = new HttpRequestMessage(method, endpoint);
            var credentials = $"{username}:{password}";
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

            return request;
        }

        #endregion
    }
}
