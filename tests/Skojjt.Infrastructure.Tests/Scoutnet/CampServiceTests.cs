using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Scoutnet;

namespace Skojjt.Infrastructure.Tests.Scoutnet;

[TestClass]
public class CampServiceTests
{
    private DbContextOptions<SkojjtDbContext> _options = null!;
    private Mock<IDbContextFactory<SkojjtDbContext>> _mockFactory = null!;
    private Mock<IScoutnetApiClient> _mockApiClient = null!;
    private CampService _service = null!;

    private const int GroupId = 9999;
    private const int SemesterId = 20251;

    [TestInitialize]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _mockFactory = new Mock<IDbContextFactory<SkojjtDbContext>>();
        _mockFactory.Setup(f => f.CreateDbContext()).Returns(() => new SkojjtDbContext(_options));
        _mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SkojjtDbContext(_options));

        _mockApiClient = new Mock<IScoutnetApiClient>();

        var logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<CampService>();

        _service = new CampService(_mockFactory.Object, _mockApiClient.Object, logger);

        SeedGroup();
    }

    private void SeedGroup()
    {
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroups.Add(new ScoutGroup
        {
            Id = GroupId,
            Name = "Testscoutkåren",
            NextLocalTroopId = 250
        });
        context.Semesters.Add(new Semester(SemesterId));
        context.SaveChanges();
    }

    private void SeedPersons(params int[] ids)
    {
        using var context = new SkojjtDbContext(_options);
        foreach (var id in ids)
        {
            context.Persons.Add(new Person
            {
                Id = id,
                FirstName = $"First{id}",
                LastName = $"Last{id}"
            });
        }
        context.SaveChanges();
    }

    #region PreviewParticipantsAsync

    [TestMethod]
    public async Task PreviewParticipantsAsync_ReturnsParticipantsWithExistenceFlags()
    {
        SeedPersons(3001, 3002);

        _mockApiClient
            .Setup(c => c.GetProjectParticipantsAsync(1190, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateParticipantResponse(
                (3001, "Anna", "Svensson", true, false),
                (3002, "Erik", "Nilsson", true, false),
                (3003, "Unknown", "Person", true, false)));

        var result = await _service.PreviewParticipantsAsync(1190, "test-key");

        Assert.IsTrue(result.Success);
        Assert.HasCount(3, result.Participants);

        var anna = result.Participants.First(p => p.MemberNo == 3001);
        Assert.IsTrue(anna.ExistsInDatabase);
        Assert.IsFalse(anna.Cancelled);

        var unknown = result.Participants.First(p => p.MemberNo == 3003);
        Assert.IsFalse(unknown.ExistsInDatabase);
    }

    [TestMethod]
    public async Task PreviewParticipantsAsync_ApiError_ReturnsFailure()
    {
        _mockApiClient
            .Setup(c => c.GetProjectParticipantsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScoutnetApiException("Unauthorized"));

        var result = await _service.PreviewParticipantsAsync(1190, "bad-key");

        Assert.IsFalse(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage!);
    }

    #endregion

    #region CreateCampAsync

    [TestMethod]
    public async Task CreateCampAsync_CreatesCorrectTroop()
    {
        var result = await _service.CreateCampAsync(
            GroupId, SemesterId, "Sommarläger", "Bullaren",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 13));

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Troop);
        Assert.AreEqual(TroopType.Camp, result.Troop.TroopType);
        Assert.AreEqual("Sommarläger", result.Troop.Name);
        Assert.AreEqual(new DateOnly(2025, 7, 10), result.Troop.CampStartDate);
        Assert.AreEqual(new DateOnly(2025, 7, 13), result.Troop.CampEndDate);
        Assert.AreEqual("Bullaren", result.Troop.DefaultMeetingLocation);
    }

    [TestMethod]
    public async Task CreateCampAsync_GeneratesMeetingsForEachDay()
    {
        var result = await _service.CreateCampAsync(
            GroupId, SemesterId, "Sommarläger", "Bullaren",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 13));

        Assert.IsTrue(result.Success);
        Assert.AreEqual(4, result.MeetingsCreated);

        using var context = new SkojjtDbContext(_options);
        var meetings = await context.Set<Meeting>()
            .Where(m => m.TroopId == result.Troop!.Id)
            .OrderBy(m => m.MeetingDate)
            .ToListAsync();

        Assert.HasCount(4, meetings);
        Assert.AreEqual(new DateOnly(2025, 7, 10), meetings[0].MeetingDate);
        Assert.AreEqual(new DateOnly(2025, 7, 13), meetings[3].MeetingDate);
        Assert.IsTrue(meetings.All(m => m.IsHike));
        Assert.IsTrue(meetings.All(m => m.DurationMinutes == 1440));
        Assert.AreEqual("Sommarläger dag 1", meetings[0].Name);
        Assert.AreEqual("Sommarläger dag 4", meetings[3].Name);
    }

    [TestMethod]
    public async Task CreateCampAsync_AllocatesLocalTroopId()
    {
        await _service.CreateCampAsync(
            GroupId, SemesterId, "Läger 1", "Plats",
            new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 2));

        using var context = new SkojjtDbContext(_options);
        var group = await context.ScoutGroups.FindAsync(GroupId);
        Assert.AreEqual(251, group!.NextLocalTroopId);
    }

    [TestMethod]
    public async Task CreateCampAsync_RejectsEndDateBeforeStartDate()
    {
        var result = await _service.CreateCampAsync(
            GroupId, SemesterId, "Ogiltigt", "Plats",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 5));

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateCampAsync_SingleDayCreatesOneMeeting()
    {
        var result = await _service.CreateCampAsync(
            GroupId, SemesterId, "Dagläger", "Plats",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 10));

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.MeetingsCreated);

        using var context = new SkojjtDbContext(_options);
        var meeting = await context.Set<Meeting>().FirstAsync(m => m.TroopId == result.Troop!.Id);
        Assert.AreEqual("Dagläger", meeting.Name); // No "dag 1" suffix for single day
    }

    [TestMethod]
    public async Task CreateCampAsync_StoresScoutnetProjectId()
    {
        var result = await _service.CreateCampAsync(
            GroupId, SemesterId, "Scoutnet-läger", "Plats",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12),
            scoutnetProjectId: 1190);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1190, result.Troop!.ScoutnetProjectId);
    }

    [TestMethod]
    public async Task CreateCampAsync_PreventsDuplicateProjectImport()
    {
        await _service.CreateCampAsync(
            GroupId, SemesterId, "Läger 1", "Plats",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12),
            scoutnetProjectId: 1190);

        var result = await _service.CreateCampAsync(
            GroupId, SemesterId, "Läger 2", "Plats",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12),
            scoutnetProjectId: 1190);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("1190", result.ErrorMessage);
    }

    #endregion

    #region ImportFromScoutnetAsync

    [TestMethod]
    public async Task ImportFromScoutnetAsync_ImportsExistingPersons()
    {
        SeedPersons(3001, 3002);

        _mockApiClient
            .Setup(c => c.GetProjectParticipantsAsync(1190, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateParticipantResponse(
                (3001, "Anna", "Svensson", true, false),
                (3002, "Erik", "Nilsson", true, false),
                (3003, "Unknown", "Person", true, false))); // Not in DB

        var result = await _service.ImportFromScoutnetAsync(
            GroupId, SemesterId, 1190, "test-key", "Sommarläger", "Bullaren",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12));

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.ParticipantsImported);
        Assert.AreEqual(1, result.ParticipantsSkipped);
        Assert.Contains("Unknown Person", result.SkippedNames);
    }

    [TestMethod]
    public async Task ImportFromScoutnetAsync_SkipsCancelledParticipants()
    {
        SeedPersons(3001, 3002);

        _mockApiClient
            .Setup(c => c.GetProjectParticipantsAsync(1190, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateParticipantResponse(
                (3001, "Anna", "Svensson", true, false),
                (3002, "Erik", "Nilsson", true, true))); // Cancelled

        var result = await _service.ImportFromScoutnetAsync(
            GroupId, SemesterId, 1190, "test-key", "Sommarläger", "Bullaren",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12));

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.ParticipantsImported);
    }

    [TestMethod]
    public async Task ImportFromScoutnetAsync_ApiError_ReturnsFailure()
    {
        _mockApiClient
            .Setup(c => c.GetProjectParticipantsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScoutnetApiException("Unauthorized"));

        var result = await _service.ImportFromScoutnetAsync(
            GroupId, SemesterId, 1190, "bad-key", "Sommarläger", "Bullaren",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12));

        Assert.IsFalse(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task ImportFromScoutnetAsync_StoresCheckinApiKey()
    {
        SeedPersons(3001);

        _mockApiClient
            .Setup(c => c.GetProjectParticipantsAsync(1190, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateParticipantResponse(
                (3001, "Anna", "Svensson", true, false)));

        var result = await _service.ImportFromScoutnetAsync(
            GroupId, SemesterId, 1190, "test-key", "Sommarläger", "Bullaren",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12),
            checkinApiKey: "checkin-key-123");

        Assert.IsTrue(result.Success);

        using var context = new SkojjtDbContext(_options);
        var troop = await context.Troops.FindAsync(result.CampResult!.Troop!.Id);
        Assert.AreEqual("checkin-key-123", troop!.ScoutnetCheckinApiKey);
    }

    #endregion

    #region PushCheckinAsync

    [TestMethod]
    public async Task PushCheckinAsync_SendsCheckinToScoutnet()
    {
        SeedPersons(3001, 3002);

        _mockApiClient
            .Setup(c => c.GetProjectParticipantsAsync(1190, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateParticipantResponse(
                (3001, "Anna", "Svensson", true, false),
                (3002, "Erik", "Nilsson", true, false)));

        var importResult = await _service.ImportFromScoutnetAsync(
            GroupId, SemesterId, 1190, "test-key", "Sommarläger", "Bullaren",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12),
            checkinApiKey: "checkin-key-123");

        Assert.IsTrue(importResult.Success);

        _mockApiClient
            .Setup(c => c.CheckinParticipantsAsync(1190, "checkin-key-123",
                It.IsAny<Dictionary<int, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectCheckinResult
            {
                Success = true,
                CheckedIn = [3001],
                Unchanged = [3002]
            });

        var checkinResult = await _service.PushCheckinAsync(
            importResult.CampResult!.Troop!.Id,
            [(3001, true), (3002, true)]);

        Assert.IsTrue(checkinResult.Success);
        Assert.AreEqual(1, checkinResult.CheckedInCount);
        Assert.AreEqual(1, checkinResult.UnchangedCount);
    }

    [TestMethod]
    public async Task PushCheckinAsync_MissingApiKey_ReturnsError()
    {
        var campResult = await _service.CreateCampAsync(
            GroupId, SemesterId, "Manual Camp", "Plats",
            new DateOnly(2025, 7, 10), new DateOnly(2025, 7, 12));

        Assert.IsTrue(campResult.Success);

        var result = await _service.PushCheckinAsync(
            campResult.Troop!.Id,
            [(3001, true)]);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
    }

    #endregion

    private static ScoutnetProjectParticipantsResponse CreateParticipantResponse(
        params (int MemberNo, string FirstName, string LastName, bool Confirmed, bool Cancelled)[] participants)
    {
        var response = new ScoutnetProjectParticipantsResponse();
        foreach (var (memberNo, firstName, lastName, confirmed, cancelled) in participants)
        {
            response.Participants[memberNo.ToString()] = new ScoutnetProjectParticipant
            {
                MemberNo = memberNo,
                FirstName = firstName,
                LastName = lastName,
                Confirmed = confirmed,
                Cancelled = cancelled,
                MemberStatus = 2
            };
        }
        return response;
    }
}
