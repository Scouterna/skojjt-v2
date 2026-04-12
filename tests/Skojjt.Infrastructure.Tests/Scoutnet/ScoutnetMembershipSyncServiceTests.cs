using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Scoutnet;

namespace Skojjt.Infrastructure.Tests.Scoutnet;

[TestClass]
public class ScoutnetMembershipSyncServiceTests
{
    private DbContextOptions<SkojjtDbContext> _options = null!;
    private Mock<IDbContextFactory<SkojjtDbContext>> _mockFactory = null!;
    private Mock<IScoutnetApiClient> _mockApiClient = null!;
    private ScoutnetMembershipSyncService _service = null!;

    private const int GroupId = 9999;
    private const string GroupName = "Testscoutkåren";
    private const string ApiKeyAll = "test-api-key-all";
    private const string ApiKeyUpdate = "test-api-key-update";
    private const int SemesterId = 20251; // HT 2025

    // Scoutnet troop IDs (real Scoutnet IDs, well above 1000)
    private const int ScoutnetTroopA = 9355;
    private const int ScoutnetTroopB = 9359;
    private const int LocalTroopId = 250; // locally created, not in Scoutnet

    // Patrol IDs
    private const int PatrolOrnen = 12001;
    private const int PatrolFalken = 12002;

    // Person IDs (Scoutnet member numbers)
    private const int Person1 = 3001;
    private const int Person2 = 3002;
    private const int Person3 = 3003;
    private const int Leader1 = 3100;

    [TestInitialize]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockFactory = new Mock<IDbContextFactory<SkojjtDbContext>>();
        _mockFactory.Setup(f => f.CreateDbContext()).Returns(() => new SkojjtDbContext(_options));
        _mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SkojjtDbContext(_options));

        _mockApiClient = new Mock<IScoutnetApiClient>();

        var logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<ScoutnetMembershipSyncService>();

        _service = new ScoutnetMembershipSyncService(_mockFactory.Object, _mockApiClient.Object, logger);
    }

    private void SeedGroup()
    {
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroups.Add(new ScoutGroup
        {
            Id = GroupId,
            Name = GroupName,
            ApiKeyAllMembers = ApiKeyAll,
            ApiKeyUpdateMembership = ApiKeyUpdate
        });
        context.Semesters.Add(new Semester(SemesterId));
        context.SaveChanges();
    }

    private void SeedPersons(params int[] personIds)
    {
        using var context = new SkojjtDbContext(_options);
        foreach (var id in personIds)
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

    private void SeedTroopWithMembers(int scoutnetTroopId, string troopName,
        (int PersonId, bool IsLeader, string? Patrol, int? PatrolId)[] members)
    {
        using var context = new SkojjtDbContext(_options);
        var troop = new Troop
        {
            ScoutnetId = scoutnetTroopId,
            ScoutGroupId = GroupId,
            SemesterId = SemesterId,
            Name = troopName
        };
        context.Troops.Add(troop);
        context.SaveChanges();

        foreach (var (personId, isLeader, patrol, patrolId) in members)
        {
            context.Set<TroopPerson>().Add(new TroopPerson
            {
                TroopId = troop.Id,
                PersonId = personId,
                IsLeader = isLeader,
                Patrol = patrol,
                PatrolId = patrolId
            });
        }
        context.SaveChanges();
    }

    private ScoutnetMemberListResponse CreateScoutnetResponse(
        params (int MemberNo, int TroopId, string TroopName, int? PatrolId, string? PatrolName)[] members)
    {
        var response = new ScoutnetMemberListResponse();
        foreach (var (memberNo, troopId, troopName, patrolId, patrolName) in members)
        {
            response.Data[memberNo.ToString()] = new ScoutnetMember
            {
                MemberNo = new ScoutnetValue { Value = memberNo.ToString() },
                FirstName = new ScoutnetValue { Value = $"First{memberNo}" },
                LastName = new ScoutnetValue { Value = $"Last{memberNo}" },
                Unit = new ScoutnetValue { RawValue = troopId.ToString(), Value = troopName },
                Patrol = patrolId.HasValue
                    ? new ScoutnetValue { RawValue = patrolId.Value.ToString(), Value = patrolName }
                    : null,
                Status = new ScoutnetValue { Value = "Aktiv" }
            };
        }
        return response;
    }

    private void SetupScoutnetApi(ScoutnetMemberListResponse response)
    {
        _mockApiClient
            .Setup(c => c.GetMemberListAsync(GroupId, ApiKeyAll, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    #region PreviewChangesAsync — No Changes

    [TestMethod]
    public async Task PreviewChangesAsync_NoChanges_ReturnsEmptyPreview()
    {
        SeedGroup();
        SeedPersons(Person1, Person2);
        SeedTroopWithMembers(ScoutnetTroopA, "Troop A",
        [
            (Person1, false, null, null),
            (Person2, false, null, null)
        ]);

        SetupScoutnetApi(CreateScoutnetResponse(
            (Person1, ScoutnetTroopA, "Troop A", null, null),
            (Person2, ScoutnetTroopA, "Troop A", null, null)));

        var preview = await _service.PreviewChangesAsync(GroupId, SemesterId);

        Assert.AreEqual(0, preview.TotalChanges);
        Assert.IsEmpty(preview.TroopChanges);
        Assert.IsEmpty(preview.PatrolChanges);
    }

    [TestMethod]
    public async Task PreviewChangesAsync_MissingApiKey_ReturnsEmptyPreview()
    {
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroups.Add(new ScoutGroup
        {
            Id = GroupId,
            Name = GroupName,
            ApiKeyAllMembers = null // No API key
        });
        context.SaveChanges();

        var preview = await _service.PreviewChangesAsync(GroupId, SemesterId);

        Assert.AreEqual(0, preview.TotalChanges);
    }

    #endregion

    #region PreviewChangesAsync — Troop Changes

    [TestMethod]
    public async Task PreviewChangesAsync_DetectsTroopChange()
    {
        SeedGroup();
        SeedPersons(Person1, Person2);

        // Person1 is in Troop B in Skojjt
        SeedTroopWithMembers(ScoutnetTroopB, "Troop B",
            [(Person1, false, null, null)]);

        // But Scoutnet has Person1 in Troop A (and Person2 in Troop B so it's recognized)
        SetupScoutnetApi(CreateScoutnetResponse(
            (Person1, ScoutnetTroopA, "Troop A", null, null),
            (Person2, ScoutnetTroopB, "Troop B", null, null)));

        var preview = await _service.PreviewChangesAsync(GroupId, SemesterId);

        Assert.HasCount(1, preview.TroopChanges);
        var change = preview.TroopChanges[0];
        Assert.AreEqual(Person1, change.MemberNo);
        Assert.AreEqual(ScoutnetTroopB, change.NewTroopId);
        Assert.AreEqual("Troop B", change.NewTroopName);
        Assert.AreEqual("Troop A", change.CurrentTroopName);
    }

    [TestMethod]
    public async Task PreviewChangesAsync_SkipsLeadersForTroopChanges()
    {
        SeedGroup();
        SeedPersons(Leader1, Person2);

        // Leader is in Troop B in Skojjt
        SeedTroopWithMembers(ScoutnetTroopB, "Troop B",
            [(Leader1, true, null, null)]);

        // But Scoutnet has them in Troop A (Person2 in Troop B so it's recognized)
        SetupScoutnetApi(CreateScoutnetResponse(
            (Leader1, ScoutnetTroopA, "Troop A", null, null),
            (Person2, ScoutnetTroopB, "Troop B", null, null)));

        var preview = await _service.PreviewChangesAsync(GroupId, SemesterId);

        Assert.IsEmpty(preview.TroopChanges);
    }

    #endregion

    #region PreviewChangesAsync — Patrol Changes

    [TestMethod]
    public async Task PreviewChangesAsync_DetectsPatrolChange()
    {
        SeedGroup();
        SeedPersons(Person1);

        // Person1 is in patrol Örnen in Skojjt
        SeedTroopWithMembers(ScoutnetTroopA, "Troop A",
            [(Person1, false, "Örnen", PatrolOrnen)]);

        // But Scoutnet has them in Falken
        SetupScoutnetApi(CreateScoutnetResponse(
            (Person1, ScoutnetTroopA, "Troop A", PatrolFalken, "Falken")));

        var preview = await _service.PreviewChangesAsync(GroupId, SemesterId);

        Assert.HasCount(1, preview.PatrolChanges);
        var change = preview.PatrolChanges[0];
        Assert.AreEqual(Person1, change.MemberNo);
        Assert.AreEqual(PatrolOrnen, change.NewPatrolId);
        Assert.AreEqual("Örnen", change.NewPatrolName);
        Assert.AreEqual("Falken", change.CurrentPatrolName);
        // Patrol changes must include troop_id
        Assert.AreEqual(ScoutnetTroopA, change.NewTroopId);
    }

    [TestMethod]
    public async Task PreviewChangesAsync_UnmappedPatrol_AddsWarning()
    {
        SeedGroup();
        SeedPersons(Person1);

        // Person1 has a patrol name but no PatrolId
        SeedTroopWithMembers(ScoutnetTroopA, "Troop A",
            [(Person1, false, "Björnen", null)]);

        // Scoutnet has them in a different patrol
        SetupScoutnetApi(CreateScoutnetResponse(
            (Person1, ScoutnetTroopA, "Troop A", PatrolFalken, "Falken")));

        var preview = await _service.PreviewChangesAsync(GroupId, SemesterId);

        Assert.IsEmpty(preview.PatrolChanges);
        Assert.HasCount(1, preview.UnmappedPatrolWarnings);
        Assert.Contains("Björnen", preview.UnmappedPatrolWarnings[0]);
    }

    #endregion

    #region PreviewChangesAsync — Local Troops

    [TestMethod]
    public async Task PreviewChangesAsync_SkipsLocalTroops()
    {
        SeedGroup();
        SeedPersons(Person1, Person2);

        // Person1 in a local troop (ScoutnetId not in Scoutnet data)
        SeedTroopWithMembers(LocalTroopId, "Lokala avdelningen",
            [(Person1, false, null, null)]);

        // Person2 in a real Scoutnet troop, same troop in both
        SeedTroopWithMembers(ScoutnetTroopA, "Troop A",
            [(Person2, false, null, null)]);

        SetupScoutnetApi(CreateScoutnetResponse(
            (Person1, ScoutnetTroopA, "Troop A", null, null),
            (Person2, ScoutnetTroopA, "Troop A", null, null)));

        var preview = await _service.PreviewChangesAsync(GroupId, SemesterId);

        Assert.IsEmpty(preview.TroopChanges);
        Assert.HasCount(1, preview.SkippedLocalTroopMembers);
        Assert.Contains("Lokala avdelningen", preview.SkippedLocalTroopMembers[0]);
    }

    #endregion

    #region PushChangesAsync

    [TestMethod]
    public async Task PushChangesAsync_EmptyChanges_ReturnsSuccess()
    {
        var result = await _service.PushChangesAsync(GroupId, []);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.UpdatedCount);
    }

    [TestMethod]
    public async Task PushChangesAsync_MissingUpdateApiKey_ReturnsError()
    {
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroups.Add(new ScoutGroup
        {
            Id = GroupId,
            Name = GroupName,
            ApiKeyAllMembers = ApiKeyAll,
            ApiKeyUpdateMembership = null // Missing!
        });
        context.SaveChanges();

        var changes = new List<MembershipChange>
        {
            new(Person1, "Test", ScoutnetTroopA, "Troop A", null, null, "Troop B", null)
        };

        var result = await _service.PushChangesAsync(GroupId, changes);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("API-nyckeln", result.ErrorMessage);
    }

    [TestMethod]
    public async Task PushChangesAsync_TroopChange_IncludesStatusConfirmed()
    {
        SeedGroup();

        Dictionary<int, MembershipUpdate>? capturedUpdates = null;
        _mockApiClient
            .Setup(c => c.UpdateMembershipAsync(GroupId, ApiKeyUpdate,
                It.IsAny<Dictionary<int, MembershipUpdate>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<int, MembershipUpdate>, CancellationToken>(
                (_, _, updates, _) => capturedUpdates = updates)
            .ReturnsAsync(new MembershipUpdateResult
            {
                Success = true,
                UpdatedMemberNumbers = [Person1]
            });

        var changes = new List<MembershipChange>
        {
            new(Person1, "First Last", ScoutnetTroopA, "Troop A", null, null, "Troop B", null)
        };

        var result = await _service.PushChangesAsync(GroupId, changes);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedUpdates);
        Assert.IsTrue(capturedUpdates.ContainsKey(Person1));
        Assert.AreEqual(ScoutnetTroopA, capturedUpdates[Person1].TroopId);
        Assert.AreEqual(ScoutnetMembershipStatus.Confirmed, capturedUpdates[Person1].Status);
    }

    [TestMethod]
    public async Task PushChangesAsync_PatrolChange_IncludesPatrolId()
    {
        SeedGroup();

        Dictionary<int, MembershipUpdate>? capturedUpdates = null;
        _mockApiClient
            .Setup(c => c.UpdateMembershipAsync(GroupId, ApiKeyUpdate,
                It.IsAny<Dictionary<int, MembershipUpdate>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<int, MembershipUpdate>, CancellationToken>(
                (_, _, updates, _) => capturedUpdates = updates)
            .ReturnsAsync(new MembershipUpdateResult
            {
                Success = true,
                UpdatedMemberNumbers = [Person1]
            });

        var changes = new List<MembershipChange>
        {
            new(Person1, "First Last", ScoutnetTroopA, "Troop A", PatrolOrnen, "Örnen", "Troop A", "Falken")
        };

        var result = await _service.PushChangesAsync(GroupId, changes);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedUpdates);
        var update = capturedUpdates[Person1];
        Assert.AreEqual(PatrolOrnen, update.PatrolId);
        // Patrol changes carry troop_id too, so status must be set
        Assert.AreEqual(ScoutnetTroopA, update.TroopId);
        Assert.AreEqual(ScoutnetMembershipStatus.Confirmed, update.Status);
    }

    [TestMethod]
    public async Task PushChangesAsync_MergesTroopAndPatrolForSameMember()
    {
        SeedGroup();

        Dictionary<int, MembershipUpdate>? capturedUpdates = null;
        _mockApiClient
            .Setup(c => c.UpdateMembershipAsync(GroupId, ApiKeyUpdate,
                It.IsAny<Dictionary<int, MembershipUpdate>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<int, MembershipUpdate>, CancellationToken>(
                (_, _, updates, _) => capturedUpdates = updates)
            .ReturnsAsync(new MembershipUpdateResult
            {
                Success = true,
                UpdatedMemberNumbers = [Person1]
            });

        // Two separate changes for the same member: troop + patrol
        var changes = new List<MembershipChange>
        {
            new(Person1, "First Last", ScoutnetTroopB, "Troop B", null, null, "Troop A", null),
            new(Person1, "First Last", ScoutnetTroopB, "Troop B", PatrolOrnen, "Örnen", "Troop A", "Falken")
        };

        var result = await _service.PushChangesAsync(GroupId, changes);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedUpdates);
        Assert.HasCount(1, capturedUpdates); // Merged into one update
        var update = capturedUpdates[Person1];
        Assert.AreEqual(ScoutnetTroopB, update.TroopId);
        Assert.AreEqual(PatrolOrnen, update.PatrolId);
        Assert.AreEqual(ScoutnetMembershipStatus.Confirmed, update.Status);
    }

    [TestMethod]
    public async Task PushChangesAsync_ApiRejectsUpdate_ReturnsErrorWithDetails()
    {
        SeedGroup();

        _mockApiClient
            .Setup(c => c.UpdateMembershipAsync(GroupId, ApiKeyUpdate,
                It.IsAny<Dictionary<int, MembershipUpdate>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MembershipUpdateResult
            {
                Success = false,
                Errors = new Dictionary<string, Dictionary<string, string>>
                {
                    ["3001"] = new() { ["patrol_id"] = "Invalid troop_id/patrol_id combination" }
                }
            });

        var changes = new List<MembershipChange>
        {
            new(Person1, "First Last", ScoutnetTroopA, "Troop A", PatrolOrnen, "Örnen", "Troop A", "Falken")
        };

        var result = await _service.PushChangesAsync(GroupId, changes);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Scoutnet avvisade uppdateringen.", result.ErrorMessage);
        Assert.HasCount(1, result.Details);
        Assert.Contains("patrol_id", result.Details[0]);
    }

    [TestMethod]
    public async Task PushChangesAsync_ApiThrows_ReturnsErrorMessage()
    {
        SeedGroup();

        _mockApiClient
            .Setup(c => c.UpdateMembershipAsync(GroupId, ApiKeyUpdate,
                It.IsAny<Dictionary<int, MembershipUpdate>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScoutnetApiException("Connection refused"));

        var changes = new List<MembershipChange>
        {
            new(Person1, "First Last", ScoutnetTroopA, "Troop A", null, null, "Troop B", null)
        };

        var result = await _service.PushChangesAsync(GroupId, changes);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Connection refused", result.ErrorMessage);
    }

    #endregion
}
