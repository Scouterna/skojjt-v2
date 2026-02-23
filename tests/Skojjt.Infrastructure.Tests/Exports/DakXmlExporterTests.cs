using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Entities;
using Skojjt.Core.Exports;
using Skojjt.Core.Utilities;
using Skojjt.Infrastructure.Exports;

namespace Skojjt.Infrastructure.Tests.Exports;

[TestClass]
public class DakXmlExporterTests
{
    private DakXmlExporter _exporter = null!;
    private AttendanceReportData _testData = null!;

    [TestInitialize]
    public void Setup()
    {
        _exporter = new DakXmlExporter();
        _testData = CreateTestData();
    }

    [TestMethod]
    public void ExporterId_ReturnsCorrectId()
    {
        Assert.AreEqual("dak", _exporter.ExporterId);
    }

    [TestMethod]
    public void DisplayName_ReturnsCorrectName()
    {
        Assert.AreEqual("DAK XML", _exporter.DisplayName);
    }

    [TestMethod]
    public async Task ExportAsync_ReturnsXmlContent()
    {
        var result = await _exporter.ExportAsync(_testData);
        
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Data);
        Assert.AreEqual("application/xml", result.ContentType);
    }

    [TestMethod]
    public async Task ExportAsync_ReturnsCorrectFileName()
    {
        var result = await _exporter.ExportAsync(_testData);
        
        Assert.EndsWith(".xml", result.FileName);
        Assert.Contains(_testData.Troop.Name, result.FileName);
    }

	[TestMethod]
	public async Task ExportAsync_GeneratesValidXml()
	{
		var result = await _exporter.ExportAsync(_testData);
		var xml = System.Text.Encoding.UTF8.GetString(result.Data);

		// Check that it's valid XML by verifying key elements
		Assert.Contains("<?xml version=\"1.0\"", xml);
		Assert.Contains("<Aktivitetskort", xml);
		Assert.Contains("<Kommun", xml);
		Assert.Contains("<Foerening", xml);
		Assert.Contains("<Naervarokort", xml);
		Assert.Contains("<DeltagarRegister", xml);
		Assert.Contains("<LedarRegister", xml);

		// Parse the xml to ensure it's well-formed
		var xmlDoc = new System.Xml.XmlDocument();
		xmlDoc.LoadXml(xml);

		// Validate against the schema using local file
		var schemaPath = Path.Combine(AppContext.BaseDirectory, "Exports", "TestData", "importSchema.xsd");
		var schemas = new System.Xml.Schema.XmlSchemaSet();
		schemas.Add(null, schemaPath);
		xmlDoc.Schemas = schemas;

		var validationErrors = new List<string>();
		xmlDoc.Validate((sender, e) =>
		{
			validationErrors.Add($"{e.Severity}: {e.Message}");
		});

		Assert.IsEmpty(validationErrors, $"XML validation errors:\n{string.Join("\n", validationErrors)}");
	}

	[TestMethod]
    public async Task ExportAsync_IncludesScoutGroupData()
    {
        var result = await _exporter.ExportAsync(_testData);
        var xml = System.Text.Encoding.UTF8.GetString(result.Data);
        
        Assert.Contains(_testData.ScoutGroup.Name, xml);
        Assert.Contains(_testData.ScoutGroup.MunicipalityId!, xml);
    }

    [TestMethod]
    public async Task ExportAsync_IncludesTroopData()
    {
        var result = await _exporter.ExportAsync(_testData);
        var xml = System.Text.Encoding.UTF8.GetString(result.Data);
        
        Assert.Contains(_testData.Troop.Name, xml);
    }

    [TestMethod]
    public async Task ExportAsync_IncludesMeetingData()
    {
        var result = await _exporter.ExportAsync(_testData);
        var xml = System.Text.Encoding.UTF8.GetString(result.Data);
        
        Assert.Contains("<Sammankomst", xml);
        Assert.Contains("Test Meeting", xml);
    }

    [TestMethod]
    public async Task ExportAsync_MeetingKodIsValidInt32()
    {
        // The DAK schema requires the kod attribute to be a string with minLength=3
        var result = await _exporter.ExportAsync(_testData);
        var xml = System.Text.Encoding.UTF8.GetString(result.Data);
        
        // Extract the kod attribute value using simple string parsing
        var kodStart = xml.IndexOf("kod=\"") + 5;
        var kodEnd = xml.IndexOf("\"", kodStart);
        var kodValue = xml.Substring(kodStart, kodEnd - kodStart);
        
        // Verify it meets minimum length requirement (3 characters)
        Assert.IsGreaterThanOrEqualTo(3, kodValue.Length, $"Meeting kod '{kodValue}' should be at least 3 characters");
        
        // Verify it can be parsed as int32 (schema allows numeric strings)
        Assert.IsTrue(int.TryParse(kodValue, out var intValue), $"Meeting kod '{kodValue}' should be convertible to int32");
        Assert.AreEqual(315000100, intValue); // Should be the meeting ID
    }

    [TestMethod]
    public async Task ExportAsync_ExcludesHikeMeetings_WhenIncludeHikeMeetingsIsFalse()
    {
        _testData = CreateTestDataWithHikeMeeting(includeHikes: false);
        
        var result = await _exporter.ExportAsync(_testData);
        var xml = System.Text.Encoding.UTF8.GetString(result.Data);
        
        Assert.DoesNotContain("Hike Meeting", xml);
        Assert.Contains("Regular Meeting", xml);
    }

    private static AttendanceReportData CreateTestData()
    {
        var scoutGroup = new ScoutGroup 
        { 
            Id = 1, 
            Name = "Test Scout Group",
            MunicipalityId = "1480",
            AssociationId = "12345",
            OrganisationNumber = "123456-7890"
        };
        
        var semester = new Semester { Id = 20251, Year = 2025, IsAutumn = true };
        
        var troop = new Troop 
        { 
            Id = 1, 
            ScoutnetId = 100, 
            Name = "Test Troop", 
            SemesterId = semester.Id 
        };

        var person1 = new Person 
        { 
            Id = 1, 
            FirstName = "Anna", 
            LastName = "Andersson", 
            PersonalNumber = "200501010020".GetNullablePersonnummer()
        };
        
        var person2 = new Person 
        { 
            Id = 2, 
            FirstName = "Erik", 
            LastName = "Eriksson", 
            PersonalNumber = "198001010019".GetNullablePersonnummer()
		};

        var meeting = new Meeting
        {
            Id = 1,
            Name = "Test Meeting",
            MeetingDate = new DateOnly(2025, 3, 15),
            StartTime = new TimeOnly(18, 30),
            DurationMinutes = 90
        };

        return new AttendanceReportData
        {
            ScoutGroup = scoutGroup,
            Troop = troop,
            Semester = semester,
            DefaultLocation = "Scouthuset",
            IncludeHikeMeetings = true,
            TroopPersons = 
            [
                new TroopPersonInfo { Person = person1, IsLeader = false, Patrol = "Örn" },
                new TroopPersonInfo { Person = person2, IsLeader = true }
            ],
            Meetings = 
            [
                new MeetingInfo { Meeting = meeting, AttendingPersonIds = [1, 2] }
            ]
        };
    }

    private static AttendanceReportData CreateTestDataWithHikeMeeting(bool includeHikes)
    {
        var data = CreateTestData();
        
        var hikeMeeting = new Meeting
        {
            Id = 2,
            Name = "Hike Meeting",
            MeetingDate = new DateOnly(2025, 4, 1),
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 480,
            IsHike = true
        };
        
        var regularMeeting = new Meeting
        {
            Id = 3,
            Name = "Regular Meeting",
            MeetingDate = new DateOnly(2025, 4, 8),
            StartTime = new TimeOnly(18, 30),
            DurationMinutes = 90,
            IsHike = false
        };

        return new AttendanceReportData
        {
            ScoutGroup = data.ScoutGroup,
            Troop = data.Troop,
            Semester = data.Semester,
            DefaultLocation = data.DefaultLocation,
            IncludeHikeMeetings = includeHikes,
            TroopPersons = data.TroopPersons,
            Meetings = 
            [
                new MeetingInfo { Meeting = hikeMeeting, AttendingPersonIds = [1, 2] },
                new MeetingInfo { Meeting = regularMeeting, AttendingPersonIds = [1] }
            ]
        };
    }
}
