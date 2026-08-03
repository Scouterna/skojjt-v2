using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Entities;

namespace Skojjt.Core.Tests.Entities;

[TestClass]
public class ScoutGroupTests
{
    [TestMethod]
    public void TryAllocateNextLocalTroopId_AllocatesAndAdvances()
    {
        var group = new ScoutGroup { NextLocalTroopId = 250 };

        var success = group.TryAllocateNextLocalTroopId(out var id);

        Assert.IsTrue(success);
        Assert.AreEqual(250, id);
        Assert.AreEqual(251, group.NextLocalTroopId);
    }

    [TestMethod]
    public void TryAllocateNextLocalTroopId_AtUpperBound_StillAllocates()
    {
        var group = new ScoutGroup { NextLocalTroopId = ScoutGroup.MaxLocalTroopId };

        var success = group.TryAllocateNextLocalTroopId(out var id);

        Assert.IsTrue(success);
        Assert.AreEqual(ScoutGroup.MaxLocalTroopId, id);
        Assert.AreEqual(ScoutGroup.MaxLocalTroopId + 1, group.NextLocalTroopId);
    }

    [TestMethod]
    public void TryAllocateNextLocalTroopId_WhenExhausted_ReturnsFalseAndDoesNotAdvance()
    {
        var group = new ScoutGroup { NextLocalTroopId = ScoutGroup.MaxLocalTroopId + 1 };

        var success = group.TryAllocateNextLocalTroopId(out var id);

        Assert.IsFalse(success);
        Assert.AreEqual(0, id);
        Assert.AreEqual(ScoutGroup.MaxLocalTroopId + 1, group.NextLocalTroopId);
    }

    [TestMethod]
    public void TryAllocateNextLocalTroopId_SequentialCalls_ProduceUniqueIds()
    {
        var group = new ScoutGroup { NextLocalTroopId = 250 };

        group.TryAllocateNextLocalTroopId(out var first);
        group.TryAllocateNextLocalTroopId(out var second);

        Assert.AreEqual(250, first);
        Assert.AreEqual(251, second);
        Assert.AreNotEqual(first, second);
    }
}
