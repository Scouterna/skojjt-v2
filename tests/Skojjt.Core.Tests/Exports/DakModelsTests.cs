using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Entities;
using Skojjt.Core.Exports;

namespace Skojjt.Core.Tests.Exports;

[TestClass]
public class DakModelsTests
{
    [TestMethod]
    public void DakDeltagare_IsFemale_ReturnsTrue_WhenPersonnummerIndicatesFemale()
    {
        // Even second-to-last digit = female
        var deltagare = new DakDeltagare { Personnummer = "200001010020" };
        Assert.IsTrue(deltagare.IsFemale());
    }

    [TestMethod]
    public void DakDeltagare_IsFemale_ReturnsFalse_WhenPersonnummerIndicatesMale()
    {
        // Odd second-to-last digit = male
        var deltagare = new DakDeltagare { Personnummer = "200001010019" };
        Assert.IsFalse(deltagare.IsFemale());
    }

    [TestMethod]
    public void DakDeltagare_IsFemale_ReturnsFalse_WhenPersonnummerIsEmpty()
    {
        var deltagare = new DakDeltagare { Personnummer = "" };
        Assert.IsFalse(deltagare.IsFemale());
    }

    [TestMethod]
    public void DakDeltagare_IsFemale_ReturnsFalse_WhenPersonnummerIsTooShort()
    {
        var deltagare = new DakDeltagare { Personnummer = "2000010100" };
        Assert.IsFalse(deltagare.IsFemale());
    }

    [TestMethod]
    public void DakDeltagare_AgeThisSemester_CalculatesCorrectAge()
    {
        var deltagare = new DakDeltagare { Personnummer = "201001010020" };
        var age = deltagare.AgeThisSemester(2025);
        Assert.AreEqual(15, age);
    }

    [TestMethod]
    public void DakDeltagare_AgeThisSemester_ReturnsZero_WhenPersonnummerIsInvalid()
    {
        var deltagare = new DakDeltagare { Personnummer = "abc" };
        var age = deltagare.AgeThisSemester(2025);
        Assert.AreEqual(0, age);
    }

    [TestMethod]
    public void DakSammankomst_GetDateString_ReturnsFormattedDate()
    {
        var sammankomst = new DakSammankomst("KOD123", new DateTime(2025, 3, 15, 18, 30, 0), 90, "Aktivitet");
      
        Assert.AreEqual("2025-03-15", sammankomst.GetDateString());
        Assert.AreEqual("03/15", sammankomst.GetDateString("MM/dd"));
    }

    [TestMethod]
    public void DakSammankomst_GetStartTimeString_ReturnsFormattedTime()
    {
        var sammankomst = new DakSammankomst("KOD123", new DateTime(2025, 3, 15, 18, 30, 0), 90, "Aktivitet");
        
        Assert.AreEqual("18:30:00", sammankomst.GetStartTimeString());
        Assert.AreEqual("18:30", sammankomst.GetStartTimeString("HH:mm"));
    }

    [TestMethod]
    public void DakSammankomst_GetStopTimeString_ReturnsCorrectEndTime()
    {
        var sammankomst = new DakSammankomst("KOD123", new DateTime(2025, 3, 15, 18, 30, 0), 90, "Aktivitet");
        
        Assert.AreEqual("20:00:00", sammankomst.GetStopTimeString());
    }

    [TestMethod]
    public void DakSammankomst_GetStopTimeString_CapsAtMidnight()
    {
        var sammankomst = new DakSammankomst(
			"KOD123", 
			new DateTime(2025, 3, 15, 23, 30, 0), 
			120, // Would go past midnight
			"Aktivitet");
        
        Assert.AreEqual("23:59:59", sammankomst.GetStopTimeString());
    }

    [TestMethod]
    public void DakSammankomst_GetAllPersons_ReturnsAllLeadersAndParticipants()
    {
        var sammankomst = new DakSammankomst("KOD123", new DateTime(2025, 3, 15, 18, 30, 0), 90, "Aktivitet");
        sammankomst.Ledare.Add(new DakDeltagare { Uid = "1", Ledare = true });
        sammankomst.Ledare.Add(new DakDeltagare { Uid = "2", Ledare = true });
        sammankomst.Deltagare.Add(new DakDeltagare { Uid = "3", Ledare = false });
        sammankomst.Deltagare.Add(new DakDeltagare { Uid = "4", Ledare = false });
        sammankomst.Deltagare.Add(new DakDeltagare { Uid = "5", Ledare = false });
        
        var allPersons = sammankomst.GetAllPersons().ToList();

        Assert.HasCount(5, allPersons);
        Assert.AreEqual(2, allPersons.Count(p => p.Ledare));
        Assert.AreEqual(3, allPersons.Count(p => !p.Ledare));
    }

    [TestMethod]
    public void DakData_DefaultValues_AreCorrect()
    {
        var dak = new DakData();
        
        Assert.IsNotNull(dak.Kort);
        Assert.AreEqual("", dak.KommunId);
        Assert.AreEqual("", dak.ForeningsId);
        Assert.AreEqual("", dak.ForeningsNamn);
        Assert.AreEqual("", dak.Organisationsnummer);
    }

    [TestMethod]
    public void DakNarvarokort_DefaultValues_AreCorrect()
    {
        var kort = new DakNarvarokort();
        
        Assert.AreEqual("Scouthuset", kort.Lokal);
        Assert.AreEqual("Scouting", kort.Aktivitet);
        Assert.AreEqual("", kort.NamnPaKort);
        Assert.AreEqual("", kort.NarvarokortNummer);
        Assert.IsNotNull(kort.Deltagare);
        Assert.IsNotNull(kort.Ledare);
        Assert.IsNotNull(kort.Sammankomster);
        Assert.IsEmpty(kort.Deltagare);
        Assert.IsEmpty(kort.Ledare);
        Assert.IsEmpty(kort.Sammankomster);
    }
}
