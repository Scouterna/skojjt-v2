using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Services;

namespace Skojjt.Infrastructure.Tests;

[TestClass]
public class BadgeServiceTests : IDisposable
{
    private readonly DbContextOptions<SkojjtDbContext> _options;
    private readonly Mock<IDbContextFactory<SkojjtDbContext>> _mockFactory;
    private readonly BadgeService _service;

    private const int TestGroupId = 100;
    private const int TestTroopId = 200;
    private const int TestSemesterId = 20251;
    private const int TestPerson1Id = 1001;
    private const int TestPerson2Id = 1002;

    public BadgeServiceTests()
    {
        _options = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockFactory = new Mock<IDbContextFactory<SkojjtDbContext>>();
        _mockFactory.Setup(f => f.CreateDbContext()).Returns(() => new SkojjtDbContext(_options));
        _mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SkojjtDbContext(_options));

        var logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<BadgeService>();
        _service = new BadgeService(_mockFactory.Object, logger);

        SeedTestData();
    }

    private void SeedTestData()
    {
        using var context = new SkojjtDbContext(_options);

        context.ScoutGroups.Add(new ScoutGroup { Id = TestGroupId, Name = "Testscoutkåren" });
        context.Semesters.Add(new Semester { Id = TestSemesterId, Year = 2025, IsAutumn = true });
        context.Troops.Add(new Troop { Id = TestTroopId, ScoutnetId = 1, ScoutGroupId = TestGroupId, SemesterId = TestSemesterId, Name = "Testpatrullen" });
        context.Persons.Add(new Person { Id = TestPerson1Id, FirstName = "Anna", LastName = "Svensson", BirthDate = new DateOnly(2010, 3, 15) });
        context.Persons.Add(new Person { Id = TestPerson2Id, FirstName = "Erik", LastName = "Johansson", BirthDate = new DateOnly(2011, 7, 22) });
        context.TroopPersons.Add(new TroopPerson { TroopId = TestTroopId, PersonId = TestPerson1Id });
        context.TroopPersons.Add(new TroopPerson { TroopId = TestTroopId, PersonId = TestPerson2Id });
        context.SaveChanges();
    }

    private Badge CreateTestBadgeWithParts(SkojjtDbContext context, string name = "Eldmärket", int scoutParts = 2, int adminParts = 1)
    {
        var badge = new Badge
        {
            ScoutGroupId = TestGroupId,
            Name = name,
            Description = "Testmärke"
        };
        context.Badges.Add(badge);
        context.SaveChanges();

        int sortOrder = 0;
        for (int i = 0; i < scoutParts; i++)
        {
            context.BadgeParts.Add(new BadgePart
            {
                BadgeId = badge.Id,
                SortOrder = sortOrder++,
                IsAdminPart = false,
                ShortDescription = $"Scoutdel {i + 1}",
                LongDescription = $"Lång beskrivning scoutdel {i + 1}"
            });
        }
        for (int i = 0; i < adminParts; i++)
        {
            context.BadgeParts.Add(new BadgePart
            {
                BadgeId = badge.Id,
                SortOrder = sortOrder++,
                IsAdminPart = true,
                ShortDescription = $"Admindel {i + 1}",
                LongDescription = $"Lång beskrivning admindel {i + 1}"
            });
        }
        context.SaveChanges();
        return badge;
    }

    public void Dispose()
    {
        using var context = new SkojjtDbContext(_options);
        context.Database.EnsureDeleted();
    }

    // --- GetBadgeWithPartsAsync ---

    [TestMethod]
    public async Task GetBadgeWithPartsAsync_ReturnsBadgeWithParts()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);

        var result = await _service.GetBadgeWithPartsAsync(badge.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(badge.Id, result.Id);
        Assert.HasCount(3, result.Parts);
    }

    [TestMethod]
    public async Task GetBadgeWithPartsAsync_ReturnsNullForMissingBadge()
    {
        var result = await _service.GetBadgeWithPartsAsync(999);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetBadgeWithPartsAsync_PartsOrderedBySortOrder()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);

        var result = await _service.GetBadgeWithPartsAsync(badge.Id);

        var parts = result!.Parts.ToList();
        for (int i = 1; i < parts.Count; i++)
        {
            Assert.IsGreaterThan(parts[i - 1].SortOrder, parts[i].SortOrder,
                $"Part at index {i} should have higher SortOrder than part at index {i - 1}");
        }
    }

    // --- GetBadgesForGroupAsync ---

    [TestMethod]
    public async Task GetBadgesForGroupAsync_ReturnsBadgesForGroup()
    {
        using var ctx = new SkojjtDbContext(_options);
        CreateTestBadgeWithParts(ctx, "Alfa");
        CreateTestBadgeWithParts(ctx, "Beta");

        var result = await _service.GetBadgesForGroupAsync(TestGroupId);

        Assert.HasCount(2, result);
        Assert.AreEqual("Alfa", result[0].Name);
        Assert.AreEqual("Beta", result[1].Name);
    }

    [TestMethod]
    public async Task GetBadgesForGroupAsync_ExcludesArchivedByDefault()
    {
        using var ctx = new SkojjtDbContext(_options);
        CreateTestBadgeWithParts(ctx, "Active");
        var archived = CreateTestBadgeWithParts(ctx, "Archived");
        archived.IsArchived = true;
        ctx.SaveChanges();

        var result = await _service.GetBadgesForGroupAsync(TestGroupId);

        Assert.HasCount(1, result);
        Assert.AreEqual("Active", result[0].Name);
    }

    [TestMethod]
    public async Task GetBadgesForGroupAsync_IncludesArchivedWhenRequested()
    {
        using var ctx = new SkojjtDbContext(_options);
        CreateTestBadgeWithParts(ctx, "Active");
        var archived = CreateTestBadgeWithParts(ctx, "Archived");
        archived.IsArchived = true;
        ctx.SaveChanges();

        var result = await _service.GetBadgesForGroupAsync(TestGroupId, includeArchived: true);

        Assert.HasCount(2, result);
    }

    // --- TogglePartAsync ---

    [TestMethod]
    public async Task TogglePartAsync_MarksPartAsDone()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 2, adminParts: 0);
        var partId = ctx.BadgeParts.First(p => p.BadgeId == badge.Id).Id;

        var result = await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");

        Assert.IsTrue(result.IsDone);
        Assert.IsFalse(result.BadgeCompleted);
    }

    [TestMethod]
    public async Task TogglePartAsync_UndoesPart()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 2, adminParts: 0);
        var partId = ctx.BadgeParts.First(p => p.BadgeId == badge.Id).Id;

        // First toggle: mark done
        await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");
        // Second toggle: undo
        var result = await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");

        Assert.IsFalse(result.IsDone);
    }

    [TestMethod]
    public async Task TogglePartAsync_UndoSetsUndoneAtTimestamp()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 1, adminParts: 0);
        var partId = ctx.BadgeParts.First(p => p.BadgeId == badge.Id).Id;

        await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");
        await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");

        using var verifyCtx = new SkojjtDbContext(_options);
        var record = await verifyCtx.BadgePartsDone
            .FirstAsync(pd => pd.PersonId == TestPerson1Id && pd.BadgeId == badge.Id && pd.BadgePartId == partId);
        Assert.IsNotNull(record.UndoneAt);
    }

    [TestMethod]
    public async Task TogglePartAsync_AutoCompletesBadgeWhenAllPartsDone()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 2, adminParts: 0);
        var parts = ctx.BadgeParts.Where(p => p.BadgeId == badge.Id).OrderBy(p => p.SortOrder).ToList();

        // Complete first part
        var result1 = await _service.TogglePartAsync(badge.Id, parts[0].Id, TestPerson1Id, "Examiner");
        Assert.IsFalse(result1.BadgeCompleted);

        // Complete second (last) part
        var result2 = await _service.TogglePartAsync(badge.Id, parts[1].Id, TestPerson1Id, "Examiner");
        Assert.IsTrue(result2.BadgeCompleted);

        // Verify BadgeCompleted record exists
        using var verifyCtx = new SkojjtDbContext(_options);
        var completion = await verifyCtx.BadgesCompleted
            .FirstOrDefaultAsync(bc => bc.PersonId == TestPerson1Id && bc.BadgeId == badge.Id);
        Assert.IsNotNull(completion);
        Assert.AreEqual("Examiner", completion.Examiner);
    }

    [TestMethod]
    public async Task TogglePartAsync_UncompletesWhenPartUndone()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 2, adminParts: 0);
        var parts = ctx.BadgeParts.Where(p => p.BadgeId == badge.Id).OrderBy(p => p.SortOrder).ToList();

        // Complete all parts
        await _service.TogglePartAsync(badge.Id, parts[0].Id, TestPerson1Id, "Examiner");
        await _service.TogglePartAsync(badge.Id, parts[1].Id, TestPerson1Id, "Examiner");

        // Undo one part
        var result = await _service.TogglePartAsync(badge.Id, parts[0].Id, TestPerson1Id, "Examiner");

        Assert.IsFalse(result.IsDone);
        Assert.IsTrue(result.BadgeUncompleted);

        // Verify BadgeCompleted record removed
        using var verifyCtx = new SkojjtDbContext(_options);
        var completion = await verifyCtx.BadgesCompleted
            .FirstOrDefaultAsync(bc => bc.PersonId == TestPerson1Id && bc.BadgeId == badge.Id);
        Assert.IsNull(completion);
    }

    [TestMethod]
    public async Task TogglePartAsync_ThrowsForInvalidBadgePart()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.TogglePartAsync(badge.Id, 99999, TestPerson1Id, "Examiner"));
    }

    [TestMethod]
    public async Task TogglePartAsync_ReDoAfterUndo()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 1, adminParts: 0);
        var partId = ctx.BadgeParts.First(p => p.BadgeId == badge.Id).Id;

        // Done ? Undo ? Done again
        await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");
        await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");
        var result = await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");

        Assert.IsTrue(result.IsDone);
        Assert.IsTrue(result.BadgeCompleted); // single part badge, so completing it finishes
    }

    // --- CreateFromTemplateAsync ---

    [TestMethod]
    public async Task CreateFromTemplateAsync_CreatesBadgeWithCopiedParts()
    {
        using var ctx = new SkojjtDbContext(_options);
        var template = new BadgeTemplate
        {
            Name = "Friluftsmärket",
            Description = "Friluftsliv"
        };
        ctx.BadgeTemplates.Add(template);
        ctx.SaveChanges();
        ctx.BadgeParts.Add(new BadgePart
        {
            BadgeTemplateId = template.Id,
            SortOrder = 0,
            IsAdminPart = false,
            ShortDescription = "Tälta"
        });
        ctx.BadgeParts.Add(new BadgePart
        {
            BadgeTemplateId = template.Id,
            SortOrder = 1,
            IsAdminPart = true,
            ShortDescription = "Godkänd"
        });
        ctx.SaveChanges();

        var badge = await _service.CreateFromTemplateAsync(template.Id, TestGroupId);

        Assert.IsNotNull(badge);
        Assert.AreEqual("Friluftsmärket", badge.Name);
        Assert.AreEqual(template.Id, badge.TemplateId);
        Assert.AreEqual(TestGroupId, badge.ScoutGroupId);

        // Verify parts were copied
        using var verifyCtx = new SkojjtDbContext(_options);
        var parts = await verifyCtx.BadgeParts
            .Where(p => p.BadgeId == badge.Id)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        Assert.HasCount(2, parts);
        Assert.AreEqual("Tälta", parts[0].ShortDescription);
        Assert.IsFalse(parts[0].IsAdminPart);
        Assert.AreEqual("Godkänd", parts[1].ShortDescription);
        Assert.IsTrue(parts[1].IsAdminPart);
    }

    [TestMethod]
    public async Task CreateFromTemplateAsync_ThrowsForMissingTemplate()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.CreateFromTemplateAsync(99999, TestGroupId));
    }

    // --- CreateBadgeAsync ---

    [TestMethod]
    public async Task CreateBadgeAsync_CreatesEmptyBadge()
    {
        var badge = await _service.CreateBadgeAsync(TestGroupId, "Nytt märke", "Beskrivning", "/img/test.png");

        Assert.IsNotNull(badge);
        Assert.AreEqual("Nytt märke", badge.Name);
        Assert.AreEqual(TestGroupId, badge.ScoutGroupId);

        using var verifyCtx = new SkojjtDbContext(_options);
        var persisted = await verifyCtx.Badges.FindAsync(badge.Id);
        Assert.IsNotNull(persisted);
        Assert.AreEqual("Beskrivning", persisted.Description);
        Assert.AreEqual("/img/test.png", persisted.ImageUrl);
    }

    // --- SetArchivedAsync ---

    [TestMethod]
    public async Task SetArchivedAsync_ArchivesBadge()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);

        await _service.SetArchivedAsync(badge.Id, true);

        using var verifyCtx = new SkojjtDbContext(_options);
        var persisted = await verifyCtx.Badges.FindAsync(badge.Id);
        Assert.IsTrue(persisted!.IsArchived);
    }

    [TestMethod]
    public async Task SetArchivedAsync_UnarchivesBadge()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);
        badge.IsArchived = true;
        ctx.SaveChanges();

        await _service.SetArchivedAsync(badge.Id, false);

        using var verifyCtx = new SkojjtDbContext(_options);
        var persisted = await verifyCtx.Badges.FindAsync(badge.Id);
        Assert.IsFalse(persisted!.IsArchived);
    }

    [TestMethod]
    public async Task SetArchivedAsync_ThrowsForMissingBadge()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SetArchivedAsync(99999, true));
    }

    // --- GetTroopProgressAsync ---

    [TestMethod]
    public async Task GetTroopProgressAsync_ReturnsProgressMatrix()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 2, adminParts: 0);
        var parts = ctx.BadgeParts.Where(p => p.BadgeId == badge.Id).OrderBy(p => p.SortOrder).ToList();

        // Person1 completes first part
        await _service.TogglePartAsync(badge.Id, parts[0].Id, TestPerson1Id, "Examiner");

        var progress = await _service.GetTroopProgressAsync(badge.Id, TestTroopId);

        Assert.AreEqual(badge.Id, progress.Badge.Id);
        Assert.HasCount(2, progress.Parts);
        Assert.HasCount(2, progress.PersonProgress);

        var person1Progress = progress.PersonProgress.First(p => p.Person.Id == TestPerson1Id);
        Assert.Contains(parts[0].Id, person1Progress.CompletedPartIds);
        Assert.DoesNotContain(parts[1].Id, person1Progress.CompletedPartIds);
        Assert.IsFalse(person1Progress.IsCompleted);

        var person2Progress = progress.PersonProgress.First(p => p.Person.Id == TestPerson2Id);
        Assert.IsEmpty(person2Progress.CompletedPartIds);
    }

    [TestMethod]
    public async Task GetTroopProgressAsync_ThrowsForMissingBadge()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.GetTroopProgressAsync(99999, TestTroopId));
    }

    // --- GetPersonBadgesAsync ---

    [TestMethod]
    public async Task GetPersonBadgesAsync_ReturnsSummaries()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 2, adminParts: 0);
        var parts = ctx.BadgeParts.Where(p => p.BadgeId == badge.Id).OrderBy(p => p.SortOrder).ToList();

        // Complete one of two parts
        await _service.TogglePartAsync(badge.Id, parts[0].Id, TestPerson1Id, "Examiner");

        var summaries = await _service.GetPersonBadgesAsync(TestPerson1Id);

        Assert.HasCount(1, summaries);
        Assert.AreEqual(badge.Id, summaries[0].Badge.Id);
        Assert.AreEqual(2, summaries[0].TotalParts);
        Assert.AreEqual(1, summaries[0].CompletedParts);
        Assert.IsFalse(summaries[0].IsCompleted);
    }

    [TestMethod]
    public async Task GetPersonBadgesAsync_ReturnsEmptyWhenNoProgress()
    {
        var summaries = await _service.GetPersonBadgesAsync(TestPerson1Id);
        Assert.HasCount(0, summaries);
    }

    [TestMethod]
    public async Task GetPersonBadgesAsync_ShowsCompletedBadge()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 1, adminParts: 0);
        var partId = ctx.BadgeParts.First(p => p.BadgeId == badge.Id).Id;

        await _service.TogglePartAsync(badge.Id, partId, TestPerson1Id, "Examiner");

        var summaries = await _service.GetPersonBadgesAsync(TestPerson1Id);

        Assert.HasCount(1, summaries);
        Assert.IsTrue(summaries[0].IsCompleted);
    }

    // --- GetTroopBadgesAsync / AssignBadgeToTroopAsync / UnassignBadgeFromTroopAsync ---

    [TestMethod]
    public async Task GetTroopBadgesAsync_ReturnsEmptyWhenNoneAssigned()
    {
        var badges = await _service.GetTroopBadgesAsync(TestTroopId);
        Assert.HasCount(0, badges);
    }

    [TestMethod]
    public async Task AssignBadgeToTroopAsync_AssignsBadge()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);

        await _service.AssignBadgeToTroopAsync(badge.Id, TestTroopId);

        var badges = await _service.GetTroopBadgesAsync(TestTroopId);
        Assert.HasCount(1, badges);
        Assert.AreEqual(badge.Id, badges[0].Id);
    }

    [TestMethod]
    public async Task AssignBadgeToTroopAsync_DoesNotDuplicateAssignment()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);

        await _service.AssignBadgeToTroopAsync(badge.Id, TestTroopId);
        await _service.AssignBadgeToTroopAsync(badge.Id, TestTroopId); // duplicate

        var badges = await _service.GetTroopBadgesAsync(TestTroopId);
        Assert.HasCount(1, badges);
    }

    [TestMethod]
    public async Task AssignBadgeToTroopAsync_MultipleAssignmentsOrdered()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge1 = CreateTestBadgeWithParts(ctx, "Alfa");
        var badge2 = CreateTestBadgeWithParts(ctx, "Beta");

        await _service.AssignBadgeToTroopAsync(badge1.Id, TestTroopId);
        await _service.AssignBadgeToTroopAsync(badge2.Id, TestTroopId);

        var badges = await _service.GetTroopBadgesAsync(TestTroopId);
        Assert.HasCount(2, badges);
        Assert.AreEqual(badge1.Id, badges[0].Id, "First assigned badge should come first");
        Assert.AreEqual(badge2.Id, badges[1].Id, "Second assigned badge should come second");
    }

    [TestMethod]
    public async Task UnassignBadgeFromTroopAsync_RemovesAssignment()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx);

        await _service.AssignBadgeToTroopAsync(badge.Id, TestTroopId);
        await _service.UnassignBadgeFromTroopAsync(badge.Id, TestTroopId);

        var badges = await _service.GetTroopBadgesAsync(TestTroopId);
        Assert.HasCount(0, badges);
    }

    [TestMethod]
    public async Task UnassignBadgeFromTroopAsync_NoOpWhenNotAssigned()
    {
        // Should not throw
        await _service.UnassignBadgeFromTroopAsync(99999, TestTroopId);
    }

    [TestMethod]
    public async Task GetTroopBadgesAsync_IncludesParts()
    {
        using var ctx = new SkojjtDbContext(_options);
        var badge = CreateTestBadgeWithParts(ctx, scoutParts: 2, adminParts: 1);

        await _service.AssignBadgeToTroopAsync(badge.Id, TestTroopId);

        var badges = await _service.GetTroopBadgesAsync(TestTroopId);
        Assert.HasCount(1, badges);
        Assert.HasCount(3, badges[0].Parts, "Should include parts");
    }
}
