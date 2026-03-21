using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Scoutnet;

namespace Skojjt.Infrastructure.Tests.Scoutnet;

[TestClass]
public class ScoutnetImportServiceTests : IDisposable
{
	private readonly ILogger<ScoutnetImportService> _logger;
	private readonly SkojjtDbContext _context;
    private readonly Mock<IScoutnetApiClient> _mockApiClient;
    private readonly ScoutnetImportService _service;
    private const int TestScoutGroupId = 9999;
    private const string TestScoutGroupName = "Testscoutkåren";

    public ScoutnetImportServiceTests()
    {
		// Create a logger
		var loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.SetMinimumLevel(LogLevel.Debug);
		});
		_logger = loggerFactory.CreateLogger<ScoutnetImportService>();


		var options = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new SkojjtDbContext(options);
        _mockApiClient = new Mock<IScoutnetApiClient>();
        _service = new ScoutnetImportService(_context, _mockApiClient.Object, _logger);

        // Seed initial data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var scoutGroup = new ScoutGroup
        {
            Id = TestScoutGroupId,
            Name = TestScoutGroupName,
            ApiKeyAllMembers = "test-api-key"
        };
        _context.ScoutGroups.Add(scoutGroup);

        var semester = new Semester
        (
            Semester.GenerateId(2025, true),
            2025,
            true
        );
        _context.Semesters.Add(semester);

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Basic Import Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_CreatesNewPersons()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(5, TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(5, result.PersonsCreated);
        Assert.AreEqual(0, result.PersonsUpdated);

        var persons = await _context.Persons.ToListAsync();
        Assert.HasCount(5, persons);
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_UpdatesExistingPersons()
    {
        // Arrange - Create existing person
        var existingPerson = new Person
        {
            Id = 1000,
            FirstName = "Old",
            LastName = "Name",
			BirthDate = new DateOnly(2010, 1, 1),
			Email = "old@example.com"
        };
        _context.Persons.Add(existingPerson);
        _context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = 1000,
            ScoutGroupId = TestScoutGroupId
        });
        await _context.SaveChangesAsync();

        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(1, TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.PersonsCreated);
        Assert.AreEqual(1, result.PersonsUpdated);

        var updatedPerson = await _context.Persons.FindAsync(1000);
        Assert.IsNotNull(updatedPerson);
        Assert.AreNotEqual("Old", updatedPerson.FirstName); // Name should be updated
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_MarksPersonsAsNotInScoutnet()
    {
        // Arrange - Create existing person that won't be in import
        var existingPerson = new Person
        {
            Id = 9999,
            FirstName = "Will",
            LastName = "BeRemoved",
			BirthDate = new DateOnly(2010, 1, 1),
			Removed = false
        };
        _context.Persons.Add(existingPerson);
        var scoutGroupPerson = new ScoutGroupPerson
        {
            PersonId = 9999,
            ScoutGroupId = TestScoutGroupId,
            NotInScoutnet = false
        };
        _context.ScoutGroupPersons.Add(scoutGroupPerson);
        await _context.SaveChangesAsync();

        // Import with different persons
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(3, TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.PersonsRemoved);

        var removedScoutGroupPerson = await _context.ScoutGroupPersons
            .FirstOrDefaultAsync(sgp => sgp.PersonId == 9999 && sgp.ScoutGroupId == TestScoutGroupId);
        Assert.IsNotNull(removedScoutGroupPerson);
        Assert.IsTrue(removedScoutGroupPerson.NotInScoutnet);
    }

    #endregion

    #region Troop Assignment Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_CreatesTroopsAndAssignments()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMultiTroopResponse(TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsGreaterThanOrEqualTo(1, result.TroopsProcessed); // At least one troop created
        Assert.IsGreaterThan(0, result.TroopMembershipsCreated);

        var troops = await _context.Troops.Where(t => t.ScoutGroupId == TestScoutGroupId).ToListAsync();
        Assert.IsGreaterThanOrEqualTo(1, troops.Count);
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_SetsLeaderFlag()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(5, TestScoutGroupId, includeLeaders: true);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        
        var leaderMemberships = await _context.TroopPersons.Where(tp => tp.IsLeader).ToListAsync();
        Assert.IsNotEmpty(leaderMemberships);
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_SetsPatrol()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(5, TestScoutGroupId, includeLeaders: false);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        
        var membershipsWithPatrol = await _context.TroopPersons
            .Where(tp => !string.IsNullOrEmpty(tp.Patrol))
            .ToListAsync();
        Assert.IsNotEmpty(membershipsWithPatrol);
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_HandlesLeaderWithMultipleTroopRoles()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateLeaderWithMultipleTroopRolesResponse(TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        
        // The leader should be assigned to both troops as a leader
        var leaderMemberships = await _context.TroopPersons
            .Where(tp => tp.PersonId == 1001 && tp.IsLeader)
            .ToListAsync();
        
        // Should be leader in at least one troop (might be both depending on import logic)
        Assert.IsGreaterThanOrEqualTo(1, leaderMemberships.Count);
    }

    #endregion

    #region Multi-Group Support Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_UpdatesGroupRolesOnScoutGroupPerson()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(3, TestScoutGroupId, includeLeaders: true);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        
        var scoutGroupPersonsWithRoles = await _context.ScoutGroupPersons
            .Where(sgp => sgp.ScoutGroupId == TestScoutGroupId && !string.IsNullOrEmpty(sgp.GroupRoles))
            .ToListAsync();
        
        // Leaders should have group roles set
        Assert.IsNotEmpty(scoutGroupPersonsWithRoles);
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_PersonCanExistInMultipleGroups()
    {
        // Arrange - Add a second scout group
        var secondGroupId = 1138;
        _context.ScoutGroups.Add(new ScoutGroup
        {
            Id = secondGroupId,
            Name = "Andra Scoutkåren",
            ApiKeyAllMembers = "test-api-key-2"
        });
        await _context.SaveChangesAsync();

        // Import to first group
        var response1 = ScoutnetTestDataGenerator.CreateMemberListResponse(3, TestScoutGroupId);
        await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response1);

        // Create a member that exists in both groups
        var memberId = 1000; // Same member ID as first group
        var response2 = new ScoutnetMemberListResponse();
        response2.Data[memberId.ToString()] = ScoutnetTestDataGenerator.CreateMember(
            memberId, secondGroupId, "Andra Scoutkåren");

        // Act - Import to second group
        var result = await _service.ImportFromResponseAsync(secondGroupId, Semester.GenerateId(2025, true), response2);

        // Assert
        Assert.IsTrue(result.Success);
        
        var personMemberships = await _context.ScoutGroupPersons
            .Where(sgp => sgp.PersonId == memberId)
            .ToListAsync();
        
        Assert.HasCount(2, personMemberships); // Person in two groups
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_RemovalOnlyAffectsSpecificGroup()
    {
        // Arrange - Add a person to two groups
        var secondGroupId = 1138;
        _context.ScoutGroups.Add(new ScoutGroup
        {
            Id = secondGroupId,
            Name = "Andra Scoutkåren",
            ApiKeyAllMembers = "test-api-key-2"
        });

        var person = new Person { Id = 5000, FirstName = "Multi", LastName = "Group", BirthDate = new DateOnly(2010, 1, 1) };
        _context.Persons.Add(person);
        _context.ScoutGroupPersons.AddRange(
            new ScoutGroupPerson { PersonId = 5000, ScoutGroupId = TestScoutGroupId, NotInScoutnet = false },
            new ScoutGroupPerson { PersonId = 5000, ScoutGroupId = secondGroupId, NotInScoutnet = false }
        );
        await _context.SaveChangesAsync();

        // Import to first group without this person
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(2, TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        
        var firstGroupMembership = await _context.ScoutGroupPersons
            .FirstOrDefaultAsync(sgp => sgp.PersonId == 5000 && sgp.ScoutGroupId == TestScoutGroupId);
        var secondGroupMembership = await _context.ScoutGroupPersons
            .FirstOrDefaultAsync(sgp => sgp.PersonId == 5000 && sgp.ScoutGroupId == secondGroupId);

        Assert.IsNotNull(firstGroupMembership);
        Assert.IsTrue(firstGroupMembership.NotInScoutnet); // Marked as removed in first group
        
        Assert.IsNotNull(secondGroupMembership);
        Assert.IsFalse(secondGroupMembership.NotInScoutnet); // Still active in second group
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_ReturnsErrorForMissingScoutGroup()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(1, TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(1234, Semester.GenerateId(2025, Semester.Season.HT), response);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("1234") ?? false);
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_ReturnsErrorForMissingSemester()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(1, TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, 9999, response);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("9999") ?? false);
    }

    [TestMethod]
    public async Task ImportMembersAsync_ReturnsErrorWhenApiKeyMissing()
    {
        // Arrange - Create group without API key
        var groupWithoutKey = new ScoutGroup { Id = 9998, Name = "No Key Group" };
        _context.ScoutGroups.Add(groupWithoutKey);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ImportMembersAsync(9998, Semester.GenerateId(2025, true));

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("API key") ?? false);
    }

    #endregion

    #region Large Data Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_HandlesLargeMemberList()
    {
        // Arrange - Create a large member list
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(100, TestScoutGroupId, troopCount: 5);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(100, result.PersonsCreated);
        
        var totalPersons = await _context.Persons.CountAsync();
        Assert.AreEqual(100, totalPersons);
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_ReportsProgress()
    {
        // Arrange
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(60, TestScoutGroupId);
        var progressMessages = new List<string>();
        // Use a synchronous IProgress implementation to avoid race conditions.
        // Progress<T> posts callbacks to the thread pool asynchronously, which can
        // cause the assertion to run before the callbacks have executed on CI.
        var progress = new SynchronousProgress<string>(msg => progressMessages.Add(msg));

        // Act
        var result = await _service.ImportFromResponseAsync(
            TestScoutGroupId, 
            Semester.GenerateId(2025, true), 
            response, 
            progress);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotEmpty(progressMessages);
        Assert.IsTrue(progressMessages.Any(m => m.Contains("Processing") || m.Contains("members")));
    }

    #endregion

    #region Stub/Migration Person Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_UpdatesRemovedStubPerson()
    {
        // Arrange - Create a stub person as the migration does (Removed=true, NotInScoutnet=true)
        var stubPerson = new Person
        {
            Id = 1000,
            FirstName = "Okänd",
            LastName = "medlem 1000",
            Removed = true
        };
        _context.Persons.Add(stubPerson);
        _context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = 1000,
            ScoutGroupId = TestScoutGroupId,
            NotInScoutnet = true
        });
        await _context.SaveChangesAsync();

        // Create a Scoutnet response that contains this member with real names
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(1, TestScoutGroupId);

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(0, result.PersonsCreated);
        Assert.AreEqual(1, result.PersonsUpdated);

        var updatedPerson = await _context.Persons.FindAsync(1000);
        Assert.IsNotNull(updatedPerson);
        Assert.AreNotEqual("Okänd", updatedPerson.FirstName, "FirstName should have been updated from Scoutnet");
        Assert.AreNotEqual("medlem 1000", updatedPerson.LastName, "LastName should have been updated from Scoutnet");
        Assert.IsFalse(updatedPerson.Removed, "Removed flag should be cleared");

        var sgp = await _context.ScoutGroupPersons
            .FirstOrDefaultAsync(s => s.PersonId == 1000 && s.ScoutGroupId == TestScoutGroupId);
        Assert.IsNotNull(sgp);
        Assert.IsFalse(sgp.NotInScoutnet, "NotInScoutnet should be cleared");
    }

    [TestMethod]
    public async Task ImportFromResponseAsync_UpdatesNameFromDictionaryKeyWhenMemberNoMissing()
    {
        // Arrange - Create a stub person
        var stubPerson = new Person
        {
            Id = 1000,
            FirstName = "Okänd",
            LastName = "medlem 1000",
            Removed = true
        };
        _context.Persons.Add(stubPerson);
        _context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = 1000,
            ScoutGroupId = TestScoutGroupId,
            NotInScoutnet = true
        });
        await _context.SaveChangesAsync();

        // Create a response where member_no field is missing (null) but dictionary key IS the member ID
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(1, TestScoutGroupId);
        // Remove the member_no field from the member data to simulate API not returning it
        var member = response.Data.Values.First();
        member.MemberNo = null;

        // Act
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(0, result.PersonsCreated);
        Assert.AreEqual(1, result.PersonsUpdated);

        var updatedPerson = await _context.Persons.FindAsync(1000);
        Assert.IsNotNull(updatedPerson);
        Assert.AreNotEqual("Okänd", updatedPerson.FirstName, "FirstName should have been updated from Scoutnet");
        Assert.IsFalse(updatedPerson.Removed, "Removed flag should be cleared");
    }

    #endregion

    #region Patrol Update Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_UpdatesPatrolOnExistingTroopPerson()
    {
        // Arrange - Simulate migration: create troop + TroopPerson without patrol
        var semesterId = Semester.GenerateId(2025, true);
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(3, TestScoutGroupId, includeLeaders: false);

        // Pre-create the troop as the migration would
        var firstMember = response.Data.Values.First();
        var troopScoutnetId = firstMember.GetUnitId()!.Value;
        var troop = new Troop
        {
            ScoutnetId = troopScoutnetId,
            ScoutGroupId = TestScoutGroupId,
            SemesterId = semesterId,
            Name = firstMember.GetUnitName()!
        };
        _context.Troops.Add(troop);

        // Pre-create TroopPerson WITHOUT patrol (as migration does)
        var personId = firstMember.GetMemberNo();
        var person = new Person { Id = personId, FirstName = "Test", LastName = "Person" };
        _context.Persons.Add(person);
        _context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = personId,
            ScoutGroupId = TestScoutGroupId
        });
        _context.TroopPersons.Add(new TroopPerson
        {
            Troop = troop,
            PersonId = personId,
            IsLeader = false,
            Patrol = null // Migration didn't include patrol
        });
        await _context.SaveChangesAsync();

        // Verify patrol is null before import
        var before = await _context.TroopPersons.FirstAsync(tp => tp.PersonId == personId);
        Assert.IsNull(before.Patrol, "Patrol should be null before import (simulating migration)");

        // Act - Run Scoutnet import which should update the patrol
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, semesterId, response);

        // Assert
        Assert.IsTrue(result.Success, result.ErrorMessage);

        var updated = await _context.TroopPersons.FirstAsync(tp => tp.PersonId == personId);
        Assert.IsNotNull(updated.Patrol, "Patrol should be set after Scoutnet import");
    }

    #endregion

    #region Duplicate Handling Tests

    [TestMethod]
    public async Task ImportFromResponseAsync_DoesNotDuplicateTroopPersons()
    {
        // Arrange - Run import twice
        var response = ScoutnetTestDataGenerator.CreateMemberListResponse(5, TestScoutGroupId);
        
        await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Act - Run again with same data
        var result = await _service.ImportFromResponseAsync(TestScoutGroupId, Semester.GenerateId(2025, true), response);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.PersonsCreated); // No new persons
        Assert.AreEqual(5, result.PersonsUpdated); // All updated
        Assert.AreEqual(0, result.TroopMembershipsCreated); // No new memberships (already exist)
    }

    #endregion
}

/// <summary>
/// A synchronous <see cref="IProgress{T}"/> implementation for use in tests.
/// Unlike <see cref="Progress{T}"/>, which posts callbacks to the thread pool,
/// this invokes the callback inline on the reporting thread.
/// </summary>
file sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
