using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Infrastructure.Scoutnet;

namespace Skojjt.Infrastructure.Tests.Scoutnet;

[TestClass]
public class ScoutnetApiModelsTests
{
    [TestMethod]
    public void ScoutnetMember_GetMemberNo_ReturnsCorrectValue()
    {
        var member = new ScoutnetMember
        {
            MemberNo = new ScoutnetValue { Value = "3252248" }
        };

        Assert.AreEqual(3252248, member.GetMemberNo());
    }

    [TestMethod]
    public void ScoutnetMember_GetMemberNo_ReturnsZeroForInvalidValue()
    {
        var member = new ScoutnetMember
        {
            MemberNo = new ScoutnetValue { Value = "invalid" }
        };

        Assert.AreEqual(0, member.GetMemberNo());
    }

    [TestMethod]
    public void ScoutnetMember_GetMobile_FixesCountryPrefix()
    {
        var member = new ScoutnetMember
        {
            MobilePhone = new ScoutnetValue { Value = "46738931758" }
        };

        Assert.AreEqual("+46738931758", member.GetMobile());
    }

    [TestMethod]
    public void ScoutnetMember_GetMobile_DoesNotAddPrefixToLocalNumber()
    {
        var member = new ScoutnetMember
        {
            MobilePhone = new ScoutnetValue { Value = "0738931758" }
        };

        Assert.AreEqual("0738931758", member.GetMobile());
    }

    [TestMethod]
    public void ScoutnetMember_GetBirthDate_ParsesCorrectly()
    {
        var member = new ScoutnetMember
        {
            DateOfBirth = new ScoutnetValue { Value = "1974-02-20" }
        };

        var result = member.GetBirthDate();
        Assert.IsNotNull(result);
        Assert.AreEqual(new DateOnly(1974, 2, 20), result.Value);
    }

    [TestMethod]
    public void ScoutnetMember_IsActive_ReturnsTrueForActiveStatus()
    {
        var member = new ScoutnetMember
        {
            Status = new ScoutnetValue { Value = "Aktiv" }
        };

        Assert.IsTrue(member.IsActive());
    }

    [TestMethod]
    public void ScoutnetMember_IsActive_ReturnsFalseForOtherStatus()
    {
        var member = new ScoutnetMember
        {
            Status = new ScoutnetValue { Value = "Inaktiv" }
        };

        Assert.IsFalse(member.IsActive());
    }

    [TestMethod]
    public void ScoutnetMember_IsLeader_ReturnsTrueForLeaderRole()
    {
        var member = new ScoutnetMember
        {
            UnitRole = new ScoutnetValue { RawValue = "2" } // Avdelningsledare
        };

        Assert.IsTrue(member.IsLeader());
    }

    [TestMethod]
    public void ScoutnetMember_IsLeader_ReturnsTrueForMultipleRolesIncludingLeader()
    {
        var member = new ScoutnetMember
        {
            UnitRole = new ScoutnetValue { RawValue = "2,3" } // Avdelningsledare, Ledare
        };

        Assert.IsTrue(member.IsLeader());
    }

    [TestMethod]
    public void ScoutnetMember_IsLeader_ReturnsFalseForNonLeaderRole()
    {
        var member = new ScoutnetMember
        {
            UnitRole = new ScoutnetValue { RawValue = "1" } // Scout
        };

        Assert.IsFalse(member.IsLeader());
    }

    [TestMethod]
    public void ScoutnetMember_GetGroupId_ParsesRawValue()
    {
        var member = new ScoutnetMember
        {
            Group = new ScoutnetValue { RawValue = "1137", Value = "Tynnereds Scoutkår" }
        };

        Assert.AreEqual(1137, member.GetGroupId());
        Assert.AreEqual("Tynnereds Scoutkår", member.GetGroupName());
    }

    [TestMethod]
    public void ScoutnetMember_GetUnitId_ParsesRawValue()
    {
        var member = new ScoutnetMember
        {
            Unit = new ScoutnetValue { RawValue = "11268", Value = "Ledare" }
        };

        Assert.AreEqual(11268, member.GetUnitId());
        Assert.AreEqual("Ledare", member.GetUnitName());
    }

    [TestMethod]
    public void ScoutnetMemberListResponse_DeserializesCorrectly()
    {
		var json = """
        {
            "data": {
                "1234567": {
                    "member_no": {"value": "1234567"},
                    "first_name": {"value": "Adam"},
                    "last_name": {"value": "Ek"},
                    "ssno": {"value": "19740220-1234"},
                    "date_of_birth": {"value": "1974-02-20"},
                    "status": {"raw_value": "2", "value": "Aktiv"},
                    "group": {"raw_value": "9999", "value": "Testscoutkåren"},
                    "unit": {"raw_value": "11268", "value": "Ledare"},
                    "unit_role": {"raw_value": "3", "value": "Ledare"},
                    "email": {"value": "adam.ek@test.se"},
                    "contact_mobile_phone": {"value": "46731234567"}
                }
            },
            "labels": {
                "member_no": "Medlemsnr."
            }
        }
        """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<ScoutnetMemberListResponse>(json, options);

        Assert.IsNotNull(response);
        Assert.HasCount(1, response.Data);
        Assert.IsTrue(response.Data.ContainsKey("1234567"));

        var member = response.Data["1234567"];
        Assert.AreEqual(1234567, member.GetMemberNo());
        Assert.AreEqual("Adam", member.GetFirstName());
        Assert.AreEqual("Ek", member.GetLastName());
        Assert.AreEqual("adam.ek@test.se", member.GetEmail());
        Assert.AreEqual("+46731234567", member.GetMobile());
        Assert.AreEqual(9999, member.GetGroupId());
        Assert.AreEqual(11268, member.GetUnitId());
        Assert.IsTrue(member.IsActive());
        Assert.IsTrue(member.IsLeader());
    }

    [TestMethod]
    public void ScoutnetMember_ExtractsParentInfo()
    {
        var member = new ScoutnetMember
        {
            MothersName = new ScoutnetValue { Value = "Kim Andersen" },
            MothersEmail = new ScoutnetValue { Value = "kim@example.com" },
            MothersMobile = new ScoutnetValue { Value = "46707744988" },
            FathersName = new ScoutnetValue { Value = "Fatma Andersen" },
            FathersEmail = new ScoutnetValue { Value = "fatma@example.com" },
            FathersMobile = new ScoutnetValue { Value = "46707744989" }
        };

        Assert.AreEqual("Kim Andersen", member.GetMumName());
        Assert.AreEqual("kim@example.com", member.GetMumEmail());
        Assert.AreEqual("+46707744988", member.GetMumMobile());
        Assert.AreEqual("Fatma Andersen", member.GetDadName());
        Assert.AreEqual("fatma@example.com", member.GetDadEmail());
        Assert.AreEqual("+46707744989", member.GetDadMobile());
    }

    [TestMethod]
    public void ScoutnetMember_GetPatrolId_ParsesRawValue()
    {
        var member = new ScoutnetMember
        {
            Patrol = new ScoutnetValue { RawValue = "19018", Value = "4196 Kangchenjunga patrol" }
        };

        Assert.AreEqual(19018, member.GetPatrolId());
        Assert.AreEqual("4196 Kangchenjunga patrol", member.GetPatrol());
    }

    [TestMethod]
    public void ScoutnetMember_GetPatrolId_ReturnsNullWhenNoPatrol()
    {
        var member = new ScoutnetMember();

        Assert.IsNull(member.GetPatrolId());
        Assert.IsNull(member.GetPatrol());
    }
}
