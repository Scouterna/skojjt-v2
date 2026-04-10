using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Core.Authentication;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Sensus;

namespace Skojjt.Infrastructure.Tests.Sensus;

[TestClass]
public class SensusSyncServiceTests
{
    private Mock<ICurrentUserService> _mockCurrentUser = null!;
    private Mock<IMeetingRepository> _mockMeetingRepo = null!;
    private Mock<ITroopRepository> _mockTroopRepo = null!;
    private ILogger<SensusSyncService> _logger = null!;

    private static readonly SensusCredentials TestCredentials = new("testuser", "testpass");

    private const string LoginSuccessResponse = """{"user":"testuser","errormessage":null}""";
    private const string LoginSessionCookie = "SensusSession=abc123; Path=/; HttpOnly";

    [TestInitialize]
    public void Setup()
    {
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockMeetingRepo = new Mock<IMeetingRepository>();
        _mockTroopRepo = new Mock<ITroopRepository>();
        _logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<SensusSyncService>();
    }

    // =========================================================================
    // GetArrangemangAsync — parsing
    // =========================================================================

    [TestMethod]
    public async Task GetArrangemangAsync_ParsesFullApiResponse()
    {
        var json = await LoadTestDataAsync("arrangemang-list.json");
        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs", json);

        var service = CreateService(handler);

        var result = await service.GetArrangemangAsync(TestCredentials);

        Assert.HasCount(1, result);
        Assert.AreEqual(100001, result[0].Id);
        Assert.AreEqual("Scout - Björkdalen Utmanare 15-18 år patrull Örnen VT-26", result[0].Name);
        Assert.AreEqual(18, result[0].SchemaCount);
    }

    [TestMethod]
    public async Task GetArrangemangAsync_EmptyResult_ReturnsEmptyList()
    {
        const string json = """{"totalCount":0,"totalPages":0,"result":[]}""";
        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs", json);

        var service = CreateService(handler);

        var result = await service.GetArrangemangAsync(TestCredentials);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetArrangemangAsync_MultipleArrangemang_ReturnsAll()
    {
        const string json = """
            {
              "totalCount": 2,
              "totalPages": 1,
              "result": [
                { "id": 100001, "namn": "Arrangemang A", "antalSchema": 10 },
                { "id": 100002, "namn": "Arrangemang B", "antalSchema": 5 }
              ]
            }
            """;
        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs", json);

        var service = CreateService(handler);

        var result = await service.GetArrangemangAsync(TestCredentials);

        Assert.HasCount(2, result);
        Assert.AreEqual("Arrangemang A", result[0].Name);
        Assert.AreEqual("Arrangemang B", result[1].Name);
    }

    [TestMethod]
    public async Task GetArrangemangAsync_UsesNamnOverName()
    {
        const string json = """
            {
              "totalCount": 1,
              "totalPages": 1,
              "result": [
                { "id": 1, "namn": "Svenska namnet", "name": "English name", "antalSchema": 3 }
              ]
            }
            """;
        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs", json);

        var service = CreateService(handler);

        var result = await service.GetArrangemangAsync(TestCredentials);

        Assert.AreEqual("Svenska namnet", result[0].Name);
    }

    [TestMethod]
    public async Task GetArrangemangAsync_FallsBackToNameWhenNamnNull()
    {
        const string json = """
            {
              "totalCount": 1,
              "totalPages": 1,
              "result": [
                { "id": 1, "namn": null, "name": "Fallback name", "antalSchema": 3 }
              ]
            }
            """;
        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs", json);

        var service = CreateService(handler);

        var result = await service.GetArrangemangAsync(TestCredentials);

        Assert.AreEqual("Fallback name", result[0].Name);
    }

    // =========================================================================
    // GetArrangemangAsync — login
    // =========================================================================

    [TestMethod]
    public async Task GetArrangemangAsync_LoginFailed_ThrowsInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetupPost("/api/account/login", HttpStatusCode.Unauthorized, "{}");

        var service = CreateService(handler);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GetArrangemangAsync(TestCredentials));

        Assert.Contains("Sensus-inloggning misslyckades", ex.Message);
    }

    [TestMethod]
    public async Task GetArrangemangAsync_LoginRejected_ThrowsWithCleanedErrorMessage()
    {
        const string loginResponse = """{"user":null,"errormessage":"Felaktigt användarnamn eller lösenord.[support]"}""";
        var handler = new MockHttpMessageHandler();
        handler.SetupPost("/api/account/login", HttpStatusCode.OK, loginResponse);

        var service = CreateService(handler);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GetArrangemangAsync(TestCredentials));

        Assert.Contains("Felaktigt användarnamn eller lösenord.", ex.Message);
        Assert.DoesNotContain("[support]", ex.Message);
    }

    [TestMethod]
    public async Task GetArrangemangAsync_SessionExpiredOnGet_ThrowsInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs", HttpStatusCode.Unauthorized, "{}");

        var service = CreateService(handler);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GetArrangemangAsync(TestCredentials));

        Assert.Contains("Sensus-sessionen har gått ut", ex.Message);
    }

    [TestMethod]
    public async Task GetArrangemangAsync_ForwardsSessionCookies()
    {
        const string json = """{"totalCount":0,"totalPages":0,"result":[]}""";
        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs", json);

        var service = CreateService(handler);

        await service.GetArrangemangAsync(TestCredentials);

        // The GET request should include the session cookie from login
        var getRequest = handler.SentRequests.First(r => r.Method == HttpMethod.Get);
        Assert.IsTrue(getRequest.Headers.Contains("Cookie"));
        var cookieHeader = getRequest.Headers.GetValues("Cookie").First();
        Assert.Contains("SensusSession=abc123", cookieHeader);
    }

    // =========================================================================
    // SyncAttendanceAsync — access control
    // =========================================================================

    [TestMethod]
    public async Task SyncAttendanceAsync_TroopNotFound_ThrowsInvalidOperationException()
    {
        _mockTroopRepo.Setup(r => r.GetWithMembersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Troop?)null);

        var handler = new MockHttpMessageHandler();
        var service = CreateService(handler);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.SyncAttendanceAsync(TestCredentials, troopId: 999, arrangemangId: 1));

        Assert.Contains("hittades inte", ex.Message);
    }

    [TestMethod]
    public async Task SyncAttendanceAsync_NoTroopAccess_ThrowsUnauthorizedAccessException()
    {
        var troop = CreateTestTroop();
        _mockTroopRepo.Setup(r => r.GetWithMembersAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(troop);
        _mockCurrentUser.Setup(u => u.HasTroopAccess(troop.ScoutGroupId, troop.ScoutnetId))
            .Returns(false);

        var handler = new MockHttpMessageHandler();
        var service = CreateService(handler);

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 1));
    }

    // =========================================================================
    // SyncAttendanceAsync — empty Sensus data
    // =========================================================================

    [TestMethod]
    public async Task SyncAttendanceAsync_NoSensusDeltagare_ReturnsZeroCounts()
    {
        var troop = CreateTestTroop(withMembers: true);
        SetupTroopAccess(troop);

        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs/1/arrdeltagares", """{"totalCount":0,"totalPages":0,"result":[]}""");
        handler.SetupGet("/api/arrangemangs/1/schema", "[]");

        _mockMeetingRepo.Setup(r => r.GetByTroopWithAttendanceAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Meeting>());

        var service = CreateService(handler);

        var result = await service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 1);

        Assert.AreEqual(0, result.SyncedCount);
        Assert.AreEqual(0, result.MatchedPersons);
        Assert.IsTrue(result.LogMessages.Any(m => m.Contains("Inga deltagare")));
    }

    // =========================================================================
    // SyncAttendanceAsync — date matching & sync
    // =========================================================================

    [TestMethod]
    public async Task SyncAttendanceAsync_MatchesMeetingsByDate()
    {
        var troop = CreateTestTroop(withMembers: true);
        SetupTroopAccess(troop);

        var meetingDate = new DateOnly(2026, 1, 14);
        var meetings = new List<Meeting>
        {
            new()
            {
                Id = 1,
                TroopId = troop.Id,
                MeetingDate = meetingDate,
                Name = "Möte 1",
                Attendances = new List<MeetingAttendance>
                {
                    new() { MeetingId = 1, PersonId = 1001 },
                },
            },
        };

        _mockMeetingRepo.Setup(r => r.GetByTroopWithAttendanceAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meetings);

        var deltagareJson = """
            {
              "totalCount": 1,
              "totalPages": 1,
              "result": [
                { "id": 5001, "namn": null, "person": { "id": 5001, "fornamn": "Anna", "efternamn": "Svensson" } }
              ]
            }
            """;
        var schemaJson = """
            [
              { "id": 9001, "datum": "2026-01-14", "signerad": false, "redigerbar": true, "narvaros": [], "signeratAntalStudieTimmar": 1.0 }
            ]
            """;

        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs/100/arrdeltagares", deltagareJson);
        handler.SetupGet("/api/arrangemangs/100/schema", schemaJson);
        handler.SetupPut("/api/arrangemangs/100/schemas/9001", HttpStatusCode.OK, "{}");

        var service = CreateService(handler);

        var result = await service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 100);

        Assert.AreEqual(1, result.SyncedCount);
        Assert.AreEqual(1, result.MatchedPersons);
        Assert.AreEqual(1, result.TotalPersons);
    }

    [TestMethod]
    public async Task SyncAttendanceAsync_SkipsSignedSchemas()
    {
        var troop = CreateTestTroop(withMembers: true);
        SetupTroopAccess(troop);

        var meetings = new List<Meeting>
        {
            new() { Id = 1, TroopId = troop.Id, MeetingDate = new DateOnly(2026, 1, 14), Name = "Möte", Attendances = [] },
        };
        _mockMeetingRepo.Setup(r => r.GetByTroopWithAttendanceAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meetings);

        var deltagareJson = """{"totalCount":1,"totalPages":1,"result":[{"id":5001,"namn":"Anna Svensson","person":null}]}""";
        var schemaJson = """[{"id":9001,"datum":"2026-01-14","signerad":true,"redigerbar":true,"narvaros":[],"signeratAntalStudieTimmar":1}]""";

        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs/100/arrdeltagares", deltagareJson);
        handler.SetupGet("/api/arrangemangs/100/schema", schemaJson);

        var service = CreateService(handler);

        var result = await service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 100);

        Assert.AreEqual(0, result.SyncedCount);
        Assert.AreEqual(1, result.SkippedCount);
        Assert.IsTrue(result.LogMessages.Any(m => m.Contains("redan signerad")));
    }

    [TestMethod]
    public async Task SyncAttendanceAsync_SkipsNonEditableSchemas()
    {
        var troop = CreateTestTroop(withMembers: true);
        SetupTroopAccess(troop);

        var meetings = new List<Meeting>
        {
            new() { Id = 1, TroopId = troop.Id, MeetingDate = new DateOnly(2026, 2, 5), Name = "Möte", Attendances = [] },
        };
        _mockMeetingRepo.Setup(r => r.GetByTroopWithAttendanceAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meetings);

        var deltagareJson = """{"totalCount":1,"totalPages":1,"result":[{"id":5001,"namn":"Anna Svensson","person":null}]}""";
        var schemaJson = """[{"id":9002,"datum":"2026-02-05","signerad":false,"redigerbar":false,"narvaros":[],"signeratAntalStudieTimmar":1}]""";

        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs/100/arrdeltagares", deltagareJson);
        handler.SetupGet("/api/arrangemangs/100/schema", schemaJson);

        var service = CreateService(handler);

        var result = await service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 100);

        Assert.AreEqual(0, result.SyncedCount);
        Assert.AreEqual(1, result.SkippedCount);
        Assert.IsTrue(result.LogMessages.Any(m => m.Contains("inte redigerbar")));
    }

    [TestMethod]
    public async Task SyncAttendanceAsync_NoMatchingDate_CountsAsNoMatch()
    {
        var troop = CreateTestTroop(withMembers: true);
        SetupTroopAccess(troop);

        var meetings = new List<Meeting>
        {
            new() { Id = 1, TroopId = troop.Id, MeetingDate = new DateOnly(2026, 3, 1), Name = "Möte", Attendances = [] },
        };
        _mockMeetingRepo.Setup(r => r.GetByTroopWithAttendanceAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meetings);

        var deltagareJson = """{"totalCount":1,"totalPages":1,"result":[{"id":5001,"namn":"Anna Svensson","person":null}]}""";
        var schemaJson = """[{"id":9001,"datum":"2026-01-14","signerad":false,"redigerbar":true,"narvaros":[]}]""";

        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs/100/arrdeltagares", deltagareJson);
        handler.SetupGet("/api/arrangemangs/100/schema", schemaJson);

        var service = CreateService(handler);

        var result = await service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 100);

        Assert.AreEqual(0, result.SyncedCount);
        Assert.AreEqual(1, result.NoMatchCount);
        Assert.IsTrue(result.LogMessages.Any(m => m.Contains("inget matchande Sensus-schema")));
    }

    // =========================================================================
    // SyncAttendanceAsync — name matching
    // =========================================================================

    [TestMethod]
    public async Task SyncAttendanceAsync_MatchesReversedNames()
    {
        // Sensus has "Svensson Anna" but Skojjt has "Anna Svensson"
        var troop = CreateTestTroop(withMembers: true);
        SetupTroopAccess(troop);

        var meetingDate = new DateOnly(2026, 1, 14);
        var meetings = new List<Meeting>
        {
            new()
            {
                Id = 1, TroopId = troop.Id, MeetingDate = meetingDate, Name = "Möte",
                Attendances = new List<MeetingAttendance> { new() { MeetingId = 1, PersonId = 1001 } },
            },
        };
        _mockMeetingRepo.Setup(r => r.GetByTroopWithAttendanceAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meetings);

        var deltagareJson = """
            {
              "totalCount": 1,
              "totalPages": 1,
              "result": [
                { "id": 5001, "namn": null, "person": { "id": 5001, "fornamn": "Svensson", "efternamn": "Anna" } }
              ]
            }
            """;
        var schemaJson = $$"""[{"id":9001,"datum":"{{meetingDate:yyyy-MM-dd}}","signerad":false,"redigerbar":true,"narvaros":[]}]""";

        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs/100/arrdeltagares", deltagareJson);
        handler.SetupGet("/api/arrangemangs/100/schema", schemaJson);
        handler.SetupPut("/api/arrangemangs/100/schemas/9001", HttpStatusCode.OK, "{}");

        var service = CreateService(handler);

        var result = await service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 100);

        Assert.AreEqual(1, result.MatchedPersons, "Should match reversed name");
        Assert.AreEqual(1, result.SyncedCount);
    }

    [TestMethod]
    public async Task SyncAttendanceAsync_MatchesPartialNames()
    {
        // Sensus has "Johansson Anna Svensson" — "anna svensson" is a substring of that
        var person = new Person { Id = 1001, FirstName = "Anna", LastName = "Svensson" };
        var troop = CreateTestTroop();
        troop.TroopPersons = new List<TroopPerson>
        {
            new() { TroopId = troop.Id, PersonId = person.Id, Person = person },
        };
        SetupTroopAccess(troop);

        var meetingDate = new DateOnly(2026, 1, 14);
        var meetings = new List<Meeting>
        {
            new()
            {
                Id = 1, TroopId = troop.Id, MeetingDate = meetingDate, Name = "Möte",
                Attendances = new List<MeetingAttendance> { new() { MeetingId = 1, PersonId = 1001 } },
            },
        };
        _mockMeetingRepo.Setup(r => r.GetByTroopWithAttendanceAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(meetings);

        var deltagareJson = """
            {
              "totalCount": 1,
              "totalPages": 1,
              "result": [
                { "id": 5001, "namn": null, "person": { "id": 5001, "fornamn": "Johansson Anna", "efternamn": "Svensson" } }
              ]
            }
            """;
        var schemaJson = $$"""[{"id":9001,"datum":"{{meetingDate:yyyy-MM-dd}}","signerad":false,"redigerbar":true,"narvaros":[]}]""";

        var handler = new MockHttpMessageHandler();
        handler.SetupLogin(LoginSuccessResponse, LoginSessionCookie);
        handler.SetupGet("/api/arrangemangs/100/arrdeltagares", deltagareJson);
        handler.SetupGet("/api/arrangemangs/100/schema", schemaJson);
        handler.SetupPut("/api/arrangemangs/100/schemas/9001", HttpStatusCode.OK, "{}");

        var service = CreateService(handler);

        var result = await service.SyncAttendanceAsync(TestCredentials, troop.Id, arrangemangId: 100);

        Assert.AreEqual(1, result.MatchedPersons, "Should match via partial name (substring)");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private SensusSyncService CreateService(MockHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://e-tjanst.sensus.se") };
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(SensusSyncService.HttpClientName)).Returns(client);

        return new SensusSyncService(
            mockFactory.Object,
            _mockCurrentUser.Object,
            _mockMeetingRepo.Object,
            _mockTroopRepo.Object,
            _logger);
    }

    private Troop CreateTestTroop(bool withMembers = false)
    {
        var troop = new Troop
        {
            Id = 200,
            ScoutnetId = 1,
            ScoutGroupId = 100,
            SemesterId = 20261,
            Name = "Testpatrullen",
        };

        if (withMembers)
        {
            var person = new Person { Id = 1001, FirstName = "Anna", LastName = "Svensson" };
            troop.TroopPersons = new List<TroopPerson>
            {
                new() { TroopId = troop.Id, PersonId = person.Id, Person = person },
            };
        }

        return troop;
    }

    private void SetupTroopAccess(Troop troop)
    {
        _mockTroopRepo.Setup(r => r.GetWithMembersAsync(troop.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(troop);
        _mockCurrentUser.Setup(u => u.HasTroopAccess(troop.ScoutGroupId, troop.ScoutnetId))
            .Returns(true);
    }

    private static async Task<string> LoadTestDataAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Sensus", "TestData", fileName);
        return await File.ReadAllTextAsync(path);
    }

    // =========================================================================
    // Mock HTTP handler
    // =========================================================================

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<RouteEntry> _routes = [];

        public List<HttpRequestMessage> SentRequests { get; } = [];

        public void SetupLogin(string responseBody, params string[] setCookieHeaders)
        {
            _routes.Add(new RouteEntry(HttpMethod.Post, "/api/account/login",
                HttpStatusCode.OK, responseBody, setCookieHeaders));
        }

        public void SetupGet(string pathContains, string responseBody)
        {
            _routes.Add(new RouteEntry(HttpMethod.Get, pathContains,
                HttpStatusCode.OK, responseBody, []));
        }

        public void SetupGet(string pathContains, HttpStatusCode statusCode, string responseBody)
        {
            _routes.Add(new RouteEntry(HttpMethod.Get, pathContains,
                statusCode, responseBody, []));
        }

        public void SetupPost(string pathContains, HttpStatusCode statusCode, string responseBody, params string[] setCookieHeaders)
        {
            _routes.Add(new RouteEntry(HttpMethod.Post, pathContains,
                statusCode, responseBody, setCookieHeaders));
        }

        public void SetupPut(string pathContains, HttpStatusCode statusCode, string responseBody)
        {
            _routes.Add(new RouteEntry(HttpMethod.Put, pathContains,
                statusCode, responseBody, []));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            foreach (var route in _routes)
            {
                if (request.Method == route.Method &&
                    path.Contains(route.PathContains, StringComparison.OrdinalIgnoreCase))
                {
                    var response = new HttpResponseMessage(route.StatusCode)
                    {
                        Content = new StringContent(route.ResponseBody, Encoding.UTF8, "application/json"),
                    };
                    foreach (var cookie in route.SetCookieHeaders)
                    {
                        response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
                    }
                    return Task.FromResult(response);
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private sealed record RouteEntry(
            HttpMethod Method,
            string PathContains,
            HttpStatusCode StatusCode,
            string ResponseBody,
            string[] SetCookieHeaders);
    }
}
