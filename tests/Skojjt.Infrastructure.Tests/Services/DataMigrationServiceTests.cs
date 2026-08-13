using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Services;

namespace Skojjt.Infrastructure.Tests.Services;

/// <summary>
/// Regression tests for importing v1 badges into a database shared by several scout
/// groups. v1 numbers badges from 1 per instance, so an export's badge IDs routinely
/// collide with badges another group already owns (Scouterna/skojjt-v2#16).
/// </summary>
[TestClass]
public class DataMigrationServiceTests : IDisposable
{
    private const int OurGroupId = 740;
    private const int OtherGroupId = 868;
    private const int SemesterId = 20261;
    private const int TroopScoutnetId = 10415;
    private const int TroopId = 500;
    private const int OurPersonId = 3227215;

    // The badge the other scout group already owns at the ID our export also uses.
    private const int CollidingBadgeId = 1;
    private const string OtherBadgeName = "Lägerbål";
    private const string OurBadgeName = "Första Repmärket";

    private DbContextOptions<SkojjtDbContext> _options = null!;
    private string _importDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _importDir = Path.Combine(Path.GetTempPath(), "skojjt-import-" + Guid.NewGuid());
        Directory.CreateDirectory(_importDir);

        using var context = new SkojjtDbContext(_options);
        context.ScoutGroups.Add(new ScoutGroup { Id = OurGroupId, Name = "Sjöscoutkåren S:t Göran" });
        context.ScoutGroups.Add(new ScoutGroup { Id = OtherGroupId, Name = "Annan kår" });
        context.Semesters.Add(new Semester(SemesterId, 2026, true));
        context.Troops.Add(new Troop
        {
            Id = TroopId,
            ScoutnetId = TroopScoutnetId,
            ScoutGroupId = OurGroupId,
            SemesterId = SemesterId,
            Name = "Drakdräparna"
        });
        context.Persons.Add(new Person { Id = OurPersonId, FirstName = "Anna", LastName = "Svensson" });

        // The other group's badge, sitting at the ID our export reuses.
        context.Badges.Add(new Badge
        {
            Id = CollidingBadgeId,
            ScoutGroupId = OtherGroupId,
            Name = OtherBadgeName,
            PartsScoutShort = ["Deras del"],
            PartsScoutLong = ["Deras långa del"]
        });
        context.SaveChanges();

        WriteExportFiles();
    }

    private void WriteExportFiles()
    {
        Write("badges.json", new[]
        {
            new
            {
                Id = CollidingBadgeId,
                ScoutGroupId = OurGroupId,
                Name = OurBadgeName,
                Description = "Vårt märke",
                PartsScoutShort = new[] { "Första examination", "Andra examination" },
                PartsScoutLong = new[] { "Knopat upp en gång", "Knopat upp två gånger" },
                PartsAdminShort = new[] { "Märke" },
                PartsAdminLong = new[] { "Märke erhållet" }
            }
        });

        Write("troop_badges.json", new[]
        {
            new
            {
                ScoutnetTroopId = TroopScoutnetId,
                ScoutGroupId = OurGroupId,
                SemesterId,
                BadgeId = CollidingBadgeId,
                SortOrder = (int?)null
            }
        });

        Write("badge_parts_done.json", new[]
        {
            new
            {
                PersonId = OurPersonId,
                BadgeId = CollidingBadgeId,
                PartIndex = 0,
                IsScoutPart = true,
                ExaminerName = "ledare@example.com",
                CompletedDate = "2026-05-29"
            }
        });

        Write("badges_completed.json", new[]
        {
            new
            {
                PersonId = OurPersonId,
                BadgeId = CollidingBadgeId,
                Examiner = "ledare@example.com",
                CompletedDate = "2026-06-01"
            }
        });
    }

    private void Write(string fileName, object content) =>
        File.WriteAllText(Path.Combine(_importDir, fileName), JsonSerializer.Serialize(content));

    private async Task ImportAsync()
    {
        await using var context = new SkojjtDbContext(_options);
        var logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<DataMigrationService>();
        var service = new DataMigrationService(context, logger);
        await service.ImportAllAsync(_importDir);
    }

    [TestMethod]
    public async Task ImportAll_WhenBadgeIdIsTakenByAnotherGroup_LeavesThatBadgeUntouched()
    {
        await ImportAsync();

        await using var context = new SkojjtDbContext(_options);
        var collided = await context.Badges.SingleAsync(b => b.Id == CollidingBadgeId);

        Assert.AreEqual(OtherGroupId, collided.ScoutGroupId, "the other group's badge changed owner");
        Assert.AreEqual(OtherBadgeName, collided.Name, "the other group's badge was overwritten");
    }

    [TestMethod]
    public async Task ImportAll_WhenBadgeIdIsTakenByAnotherGroup_ImportsOurBadgeUnderANewId()
    {
        await ImportAsync();

        await using var context = new SkojjtDbContext(_options);
        var ours = await context.Badges.SingleAsync(b => b.ScoutGroupId == OurGroupId);

        Assert.AreNotEqual(CollidingBadgeId, ours.Id, "our badge reused the colliding ID");
        Assert.AreEqual(OurBadgeName, ours.Name);
        Assert.AreEqual(3, await context.BadgeParts.CountAsync(p => p.BadgeId == ours.Id),
            "expected two scout parts and one admin part");
    }

    [TestMethod]
    public async Task ImportAll_PointsDependentRowsAtOurBadgeNotTheStrangers()
    {
        await ImportAsync();

        await using var context = new SkojjtDbContext(_options);
        var ourBadgeId = (await context.Badges.SingleAsync(b => b.ScoutGroupId == OurGroupId)).Id;

        var troopBadge = await context.Set<TroopBadge>().SingleAsync();
        var partDone = await context.Set<BadgePartDone>().SingleAsync();
        var completed = await context.Set<BadgeCompleted>().SingleAsync();

        Assert.AreEqual(ourBadgeId, troopBadge.BadgeId, "troop badge points at the wrong badge");
        Assert.AreEqual(ourBadgeId, partDone.BadgeId, "badge part done points at the wrong badge");
        Assert.AreEqual(ourBadgeId, completed.BadgeId, "badge completed points at the wrong badge");
    }

    [TestMethod]
    public async Task ImportAll_AddsNothingToTheOtherGroupsBadge()
    {
        await ImportAsync();

        await using var context = new SkojjtDbContext(_options);

        Assert.AreEqual(0, await context.Set<TroopBadge>().CountAsync(t => t.BadgeId == CollidingBadgeId));
        Assert.AreEqual(0, await context.Set<BadgePartDone>().CountAsync(p => p.BadgeId == CollidingBadgeId));
        Assert.AreEqual(0, await context.Set<BadgeCompleted>().CountAsync(c => c.BadgeId == CollidingBadgeId));
    }

    [TestMethod]
    public async Task ImportAll_RunTwice_DoesNotDuplicateBadgesOrProgress()
    {
        await ImportAsync();
        await ImportAsync();

        await using var context = new SkojjtDbContext(_options);

        Assert.AreEqual(1, await context.Badges.CountAsync(b => b.ScoutGroupId == OurGroupId),
            "re-importing duplicated the badge");
        Assert.AreEqual(1, await context.Set<TroopBadge>().CountAsync());
        Assert.AreEqual(1, await context.Set<BadgePartDone>().CountAsync());
        Assert.AreEqual(1, await context.Set<BadgeCompleted>().CountAsync());
    }

    public void Dispose()
    {
        using var context = new SkojjtDbContext(_options);
        context.Database.EnsureDeleted();

        if (Directory.Exists(_importDir))
            Directory.Delete(_importDir, recursive: true);
    }
}
