using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Entities;
using Skojjt.Core.Exports;

namespace Skojjt.Core.Tests.Exports;

[TestClass]
public class AttendanceReportDataTests
{
    [TestMethod]
    public void AttendanceReportData_CanBeCreated_WithRequiredProperties()
    {
        var scoutGroup = new ScoutGroup { Id = 1, Name = "Test Group" };
        var semester = new Semester { Id = 20251, Year = 2025, IsAutumn = true };
        var troop = new Troop { Id = 1, ScoutnetId = 100, Name = "Test Troop", SemesterId = semester.Id };
        
        var data = new AttendanceReportData
        {
            ScoutGroup = scoutGroup,
            Troop = troop,
            Semester = semester,
            TroopPersons = [],
            Meetings = []
        };
        
        Assert.AreEqual(scoutGroup, data.ScoutGroup);
        Assert.AreEqual(troop, data.Troop);
        Assert.AreEqual(semester, data.Semester);
        Assert.IsEmpty( data.TroopPersons);
        Assert.IsEmpty(data.Meetings);
    }

    [TestMethod]
    public void AttendanceReportData_DefaultValues_AreCorrect()
    {
        var scoutGroup = new ScoutGroup { Id = 1, Name = "Test Group" };
        var semester = new Semester { Id = 20251, Year = 2025, IsAutumn = true };
        var troop = new Troop { Id = 1, ScoutnetId = 100, Name = "Test Troop" };
        
        var data = new AttendanceReportData
        {
            ScoutGroup = scoutGroup,
            Troop = troop,
            Semester = semester,
            TroopPersons = [],
            Meetings = []
        };
        
        Assert.AreEqual(string.Empty, data.DefaultLocation);
        Assert.IsTrue(data.IncludeHikeMeetings);
    }

    [TestMethod]
    public void TroopPersonInfo_CanBeCreated()
    {
        var person = new Person { Id = 1, FirstName = "Test", LastName = "Person" };
        
        var info = new TroopPersonInfo
        {
            Person = person,
            IsLeader = true,
            Patrol = "Örn"
        };
        
        Assert.AreEqual(person, info.Person);
        Assert.IsTrue(info.IsLeader);
        Assert.AreEqual("Örn", info.Patrol);
    }

    [TestMethod]
    public void MeetingInfo_CanBeCreated()
    {
        var meeting = new Meeting 
        { 
            Id = 1, 
            Name = "Test Meeting", 
            MeetingDate = new DateOnly(2025, 3, 15) 
        };
        
        var info = new MeetingInfo
        {
            Meeting = meeting,
            AttendingPersonIds = [1, 2, 3]
        };
        
        Assert.AreEqual(meeting, info.Meeting);
        Assert.HasCount(3, info.AttendingPersonIds);
        Assert.IsTrue(info.AttendingPersonIds.Contains(1));
        Assert.IsTrue(info.AttendingPersonIds.Contains(2));
        Assert.IsTrue(info.AttendingPersonIds.Contains(3));
    }
}
