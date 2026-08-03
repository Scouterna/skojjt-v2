using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Services;

namespace Skojjt.Infrastructure.Tests.Services;

[TestClass]
public class TroopManagementServiceTests
{
    private DbContextOptions<SkojjtDbContext> _options = null!;
    private Mock<IDbContextFactory<SkojjtDbContext>> _mockFactory = null!;
    private TroopManagementService _service = null!;

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

        var logger = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<TroopManagementService>();

        _service = new TroopManagementService(_mockFactory.Object, logger);
    }

    private void SeedGroup(int nextLocalTroopId = 250)
    {
        using var context = new SkojjtDbContext(_options);
        context.ScoutGroups.Add(new ScoutGroup
        {
            Id = GroupId,
            Name = "Testscoutkåren",
            NextLocalTroopId = nextLocalTroopId
        });
        context.Semesters.Add(new Semester(SemesterId));
        context.SaveChanges();
    }

    [TestMethod]
    public async Task CreateLocalTroopAsync_CreatesRegularTroopWithLocalId()
    {
        SeedGroup();

        var result = await _service.CreateLocalTroopAsync(
            GroupId, SemesterId, "Spårarna", unitTypeId: 2,
            defaultStartTime: new TimeOnly(18, 0), defaultDurationMinutes: 120,
            defaultMeetingLocation: "Scoutstugan");

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Troop);
        Assert.AreEqual(TroopType.Regular, result.Troop.TroopType);
        Assert.AreEqual("Spårarna", result.Troop.Name);
        Assert.AreEqual(250, result.Troop.ScoutnetId);
        Assert.AreEqual(2, result.Troop.UnitTypeId);
        Assert.AreEqual(new TimeOnly(18, 0), result.Troop.DefaultStartTime);
        Assert.AreEqual(120, result.Troop.DefaultDurationMinutes);
        Assert.AreEqual("Scoutstugan", result.Troop.DefaultMeetingLocation);
    }

    [TestMethod]
    public async Task CreateLocalTroopAsync_AdvancesNextLocalTroopId()
    {
        SeedGroup();

        await _service.CreateLocalTroopAsync(GroupId, SemesterId, "Avd 1");

        using var context = new SkojjtDbContext(_options);
        var group = await context.ScoutGroups.FindAsync(GroupId);
        Assert.AreEqual(251, group!.NextLocalTroopId);
    }

    [TestMethod]
    public async Task CreateLocalTroopAsync_UsesDefaultsWhenNotProvided()
    {
        SeedGroup();

        var result = await _service.CreateLocalTroopAsync(GroupId, SemesterId, "Avd");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(new TimeOnly(18, 30), result.Troop!.DefaultStartTime);
        Assert.AreEqual(90, result.Troop.DefaultDurationMinutes);
        Assert.IsNull(result.Troop.UnitTypeId);
        Assert.IsNull(result.Troop.DefaultMeetingLocation);
    }

    [TestMethod]
    public async Task CreateLocalTroopAsync_TrimsName()
    {
        SeedGroup();

        var result = await _service.CreateLocalTroopAsync(GroupId, SemesterId, "  Avd  ");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Avd", result.Troop!.Name);
    }

    [TestMethod]
    public async Task CreateLocalTroopAsync_RejectsBlankName()
    {
        SeedGroup();

        var result = await _service.CreateLocalTroopAsync(GroupId, SemesterId, "   ");

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateLocalTroopAsync_MissingGroup_ReturnsFailure()
    {
        // No group seeded.
        var result = await _service.CreateLocalTroopAsync(GroupId, SemesterId, "Avd");

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateLocalTroopAsync_RangeExhausted_ReturnsFailure()
    {
        SeedGroup(nextLocalTroopId: ScoutGroup.MaxLocalTroopId + 1);

        var result = await _service.CreateLocalTroopAsync(GroupId, SemesterId, "Avd");

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
    }
}
