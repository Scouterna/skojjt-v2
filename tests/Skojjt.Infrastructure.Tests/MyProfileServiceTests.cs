using Microsoft.EntityFrameworkCore;
using Moq;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Services;

namespace Skojjt.Infrastructure.Tests;

[TestClass]
public class MyProfileServiceTests : IDisposable
{
    private readonly DbContextOptions<SkojjtDbContext> _options;
    private readonly Mock<IDbContextFactory<SkojjtDbContext>> _mockFactory;
    private readonly MyProfileService _service;

    private const int TestPersonId = 1001;
    private const int TestPerson2Id = 1002;
    private const int TestGroupId1 = 100;
    private const int TestGroupId2 = 200;
    private const int TestSemesterHt = 20251;
    private const int TestSemesterVt = 20250;
    private const int TestTroop1Id = 300;
    private const int TestTroop2Id = 301;

    public MyProfileServiceTests()
    {
        _options = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockFactory = new Mock<IDbContextFactory<SkojjtDbContext>>();
        _mockFactory.Setup(f => f.CreateDbContext()).Returns(() => new SkojjtDbContext(_options));
        _mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SkojjtDbContext(_options));

        _service = new MyProfileService(_mockFactory.Object);
    }

    private void SeedBaseData()
    {
        using var context = new SkojjtDbContext(_options);

        context.Persons.Add(new Person { Id = TestPersonId, FirstName = "Anna", LastName = "Svensson", BirthDate = new DateOnly(2010, 3, 15) });
        context.Persons.Add(new Person { Id = TestPerson2Id, FirstName = "Erik", LastName = "Johansson", BirthDate = new DateOnly(2011, 7, 22) });
        context.ScoutGroups.Add(new ScoutGroup { Id = TestGroupId1, Name = "Testscoutkåren" });
        context.ScoutGroups.Add(new ScoutGroup { Id = TestGroupId2, Name = "Andrakåren" });
        context.Semesters.Add(new Semester( TestSemesterHt, 2025, true));
        context.Semesters.Add(new Semester( TestSemesterVt, 2025, false));
        context.Troops.Add(new Troop { Id = TestTroop1Id, ScoutnetId = 1, ScoutGroupId = TestGroupId1, SemesterId = TestSemesterHt, Name = "Spårarna" });
        context.Troops.Add(new Troop { Id = TestTroop2Id, ScoutnetId = 2, ScoutGroupId = TestGroupId1, SemesterId = TestSemesterVt, Name = "Upptäckarna" });
        context.SaveChanges();
    }

    public void Dispose()
    {
        using var context = new SkojjtDbContext(_options);
        context.Database.EnsureDeleted();
    }

    // --- GetPersonAsync ---

    [TestMethod]
    public async Task GetPersonAsync_WithExistingPerson_ReturnsPerson()
    {
        SeedBaseData();

        var person = await _service.GetPersonAsync(TestPersonId);

        Assert.IsNotNull(person);
        Assert.AreEqual(TestPersonId, person.Id);
        Assert.AreEqual("Anna", person.FirstName);
        Assert.AreEqual("Svensson", person.LastName);
    }

    [TestMethod]
    public async Task GetPersonAsync_WithNonExistingPerson_ReturnsNull()
    {
        SeedBaseData();

        var person = await _service.GetPersonAsync(99999);

        Assert.IsNull(person);
    }

    // --- GetGroupMembershipsAsync ---

    [TestMethod]
    public async Task GetGroupMembershipsAsync_ReturnsActiveGroups()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = TestPersonId,
            ScoutGroupId = TestGroupId1,
            GroupRoles = "leader"
        });
        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = TestPersonId,
            ScoutGroupId = TestGroupId2,
            GroupRoles = "assistant_leader"
        });
        context.SaveChanges();

        var groups = await _service.GetGroupMembershipsAsync(TestPersonId);

        Assert.HasCount(2, groups);
        Assert.IsTrue(groups.Any(g => g.ScoutGroupId == TestGroupId1 && g.ScoutGroupName == "Testscoutkåren"));
        Assert.IsTrue(groups.Any(g => g.ScoutGroupId == TestGroupId2 && g.ScoutGroupName == "Andrakåren"));
    }

    [TestMethod]
    public async Task GetGroupMembershipsAsync_ExcludesNotInScoutnet()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = TestPersonId,
            ScoutGroupId = TestGroupId1,
            NotInScoutnet = false
        });
        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = TestPersonId,
            ScoutGroupId = TestGroupId2,
            NotInScoutnet = true // Removed from Scoutnet
        });
        context.SaveChanges();

        var groups = await _service.GetGroupMembershipsAsync(TestPersonId);

        Assert.HasCount(1, groups);
        Assert.AreEqual(TestGroupId1, groups[0].ScoutGroupId);
    }

    [TestMethod]
    public async Task GetGroupMembershipsAsync_ReturnsEmptyForPersonWithNoGroups()
    {
        SeedBaseData();

        var groups = await _service.GetGroupMembershipsAsync(TestPersonId);

        Assert.HasCount(0, groups);
    }

    [TestMethod]
    public async Task GetGroupMembershipsAsync_DoesNotReturnOtherPersonsGroups()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = TestPerson2Id,
            ScoutGroupId = TestGroupId1
        });
        context.SaveChanges();

        var groups = await _service.GetGroupMembershipsAsync(TestPersonId);

        Assert.HasCount(0, groups);
    }

    [TestMethod]
    public async Task GetGroupMembershipsAsync_IncludesRoles()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = TestPersonId,
            ScoutGroupId = TestGroupId1,
            GroupRoles = "leader,member_registrar"
        });
        context.SaveChanges();

        var groups = await _service.GetGroupMembershipsAsync(TestPersonId);

        Assert.HasCount(1, groups);
        Assert.AreEqual("leader,member_registrar", groups[0].Roles);
    }

    [TestMethod]
    public async Task GetGroupMembershipsAsync_HandlesNullRoles()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroupPersons.Add(new ScoutGroupPerson
        {
            PersonId = TestPersonId,
            ScoutGroupId = TestGroupId1,
            GroupRoles = null
        });
        context.SaveChanges();

        var groups = await _service.GetGroupMembershipsAsync(TestPersonId);

        Assert.HasCount(1, groups);
        Assert.AreEqual("", groups[0].Roles);
    }

    // --- GetAttendanceSummaryAsync ---

    [TestMethod]
    public async Task GetAttendanceSummaryAsync_ReturnsSummaryGroupedByTroopAndSemester()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);

        // Add 3 meetings to Troop1 (HT 2025), person attends 2
        for (int i = 1; i <= 3; i++)
        {
            context.Meetings.Add(new Meeting
            {
                Id = i,
                TroopId = TestTroop1Id,
                MeetingDate = new DateOnly(2025, 9, i),
                Name = $"Möte {i}"
            });
        }
        context.SaveChanges();

        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 1, PersonId = TestPersonId });
        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 2, PersonId = TestPersonId });
        context.SaveChanges();

        var summary = await _service.GetAttendanceSummaryAsync(TestPersonId);

        Assert.HasCount(1, summary);
        Assert.AreEqual("Spårarna", summary[0].TroopName);
        Assert.AreEqual(2025, summary[0].Year);
        Assert.IsTrue(summary[0].IsAutumn);
        Assert.AreEqual(2, summary[0].AttendedMeetings);
        Assert.AreEqual("HT 2025", summary[0].SemesterDisplayName);
    }

    [TestMethod]
    public async Task GetAttendanceSummaryAsync_ReturnsEmptyForNoAttendance()
    {
        SeedBaseData();

        var summary = await _service.GetAttendanceSummaryAsync(TestPersonId);

        Assert.HasCount(0, summary);
    }

    [TestMethod]
    public async Task GetAttendanceSummaryAsync_GroupsAcrossMultipleTroopsAndSemesters()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);

        // Troop1 (HT 2025) - 2 meetings attended
        context.Meetings.Add(new Meeting { Id = 10, TroopId = TestTroop1Id, MeetingDate = new DateOnly(2025, 9, 1), Name = "M1" });
        context.Meetings.Add(new Meeting { Id = 11, TroopId = TestTroop1Id, MeetingDate = new DateOnly(2025, 9, 8), Name = "M2" });
        // Troop2 (VT 2025) - 1 meeting attended
        context.Meetings.Add(new Meeting { Id = 12, TroopId = TestTroop2Id, MeetingDate = new DateOnly(2025, 3, 1), Name = "M3" });
        context.SaveChanges();

        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 10, PersonId = TestPersonId });
        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 11, PersonId = TestPersonId });
        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 12, PersonId = TestPersonId });
        context.SaveChanges();

        var summary = await _service.GetAttendanceSummaryAsync(TestPersonId);

        Assert.HasCount(2, summary);
        // Ordered by year desc, then isAutumn desc ? HT 2025 first, VT 2025 second
        Assert.AreEqual("HT 2025", summary[0].SemesterDisplayName);
        Assert.AreEqual(2, summary[0].AttendedMeetings);
        Assert.AreEqual("VT 2025", summary[1].SemesterDisplayName);
        Assert.AreEqual(1, summary[1].AttendedMeetings);
    }

    [TestMethod]
    public async Task GetAttendanceSummaryAsync_DoesNotIncludeOtherPersonsAttendance()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);

        context.Meetings.Add(new Meeting { Id = 20, TroopId = TestTroop1Id, MeetingDate = new DateOnly(2025, 9, 1), Name = "M1" });
        context.SaveChanges();

        // Person2 attends, not Person1
        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 20, PersonId = TestPerson2Id });
        context.SaveChanges();

        var summary = await _service.GetAttendanceSummaryAsync(TestPersonId);

        Assert.HasCount(0, summary);
    }

    [TestMethod]
    public async Task GetAttendanceSummaryAsync_OrdersNewestFirst()
    {
        SeedBaseData();
        using var context = new SkojjtDbContext(_options);

        // Add an older semester and troop
        context.Semesters.Add(new Semester(20241, 2024, true));
        context.Troops.Add(new Troop { Id = 400, ScoutnetId = 3, ScoutGroupId = TestGroupId1, SemesterId = 20241, Name = "Gamla avd" });

        context.Meetings.Add(new Meeting { Id = 30, TroopId = 400, MeetingDate = new DateOnly(2024, 10, 1), Name = "Old" });
        context.Meetings.Add(new Meeting { Id = 31, TroopId = TestTroop1Id, MeetingDate = new DateOnly(2025, 9, 1), Name = "New" });
        context.SaveChanges();

        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 30, PersonId = TestPersonId });
        context.MeetingAttendances.Add(new MeetingAttendance { MeetingId = 31, PersonId = TestPersonId });
        context.SaveChanges();

        var summary = await _service.GetAttendanceSummaryAsync(TestPersonId);

        Assert.HasCount(2, summary);
        Assert.AreEqual(2025, summary[0].Year, "Newest semester should come first");
        Assert.AreEqual(2024, summary[1].Year, "Older semester should come second");
    }
}
