using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Scoutnet;

namespace Skojjt.Infrastructure.Tests.Scoutnet;

[TestClass]
public class ScoutnetRegistrationServiceTests : IDisposable
{
    private readonly Mock<IScoutGroupRepository> _mockScoutGroupRepository;
    private readonly Mock<IScoutnetApiClient> _mockApiClient;
    private readonly DbContextOptions<SkojjtDbContext> _dbOptions;
    private readonly ScoutnetRegistrationService _service;
    private const int TestScoutGroupId = 9999;
    private const int TestTroopId = 100;
    private const int TestMemberNo = 123456;

    public ScoutnetRegistrationServiceTests()
    {
        _mockScoutGroupRepository = new Mock<IScoutGroupRepository>();
        _mockApiClient = new Mock<IScoutnetApiClient>();
        var logger = LoggerFactory
            .Create(builder => builder.SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<ScoutnetRegistrationService>();

        _dbOptions = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockContextFactory = new Mock<IDbContextFactory<SkojjtDbContext>>();
        mockContextFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new SkojjtDbContext(_dbOptions));

        _service = new ScoutnetRegistrationService(
            _mockScoutGroupRepository.Object,
            mockContextFactory.Object,
            _mockApiClient.Object,
            logger);

        // Default scout group setup
        _mockScoutGroupRepository
            .Setup(r => r.GetByIdAsync(TestScoutGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScoutGroup
            {
                Id = TestScoutGroupId,
                Name = "Testscoutkåren",
                ApiKeyWaitinglist = "test-waitinglist-key"
            });

        SeedTestData();
    }

    private void SeedTestData()
    {
        using var context = new SkojjtDbContext(_dbOptions);
        context.ScoutGroups.Add(new ScoutGroup
        {
            Id = TestScoutGroupId,
            Name = "Testscoutkåren",
            ApiKeyWaitinglist = "test-waitinglist-key"
        });

        var semester = new Semester(Semester.GenerateId(2025, true), 2025, true);
        context.Semesters.Add(semester);

        context.Troops.Add(new Troop
        {
            Id = TestTroopId,
            ScoutnetId = 500,
            ScoutGroupId = TestScoutGroupId,
            SemesterId = semester.Id,
            Name = "Testavdelningen"
        });

        context.SaveChanges();
    }

    public void Dispose()
    {
        using var context = new SkojjtDbContext(_dbOptions);
        context.Database.EnsureDeleted();
    }

    private static WaitinglistRegistrationRequest CreateValidRequest() => new()
    {
        FirstName = "Anna",
        LastName = "Andersson",
        Personnummer = "20100115-2386",
        Email = "anna.andersson@example.com",
        AddressLine1 = "Storgatan 1",
        ZipCode = "41301",
        ZipName = "Göteborg",
        Mobile = "0701234567",
        Phone = "031123456"
    };

    private void SetupApiSuccess(int memberNo = TestMemberNo)
    {
        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WaitinglistRegistrationResult
            {
                Success = true,
                MemberNo = memberNo
            });
    }

    #region Validation Tests

    [TestMethod]
    public async Task AddToWaitinglistAsync_EmptyFirstName_ReturnsError()
    {
        var request = CreateValidRequest();
        request.FirstName = string.Empty;

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Förnamn");
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_EmptyLastName_ReturnsError()
    {
        var request = CreateValidRequest();
        request.LastName = string.Empty;

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Efternamn");
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_InvalidPersonnummer_ReturnsError()
    {
        var request = CreateValidRequest();
        request.Personnummer = "12345";

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Personnumret");
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_EmptyEmail_ReturnsError()
    {
        var request = CreateValidRequest();
        request.Email = string.Empty;

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "E-post");
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_EmptyAddress_ReturnsError()
    {
        var request = CreateValidRequest();
        request.AddressLine1 = string.Empty;

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Adress");
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_EmptyZipCode_ReturnsError()
    {
        var request = CreateValidRequest();
        request.ZipCode = string.Empty;

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Postnummer");
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_EmptyZipName_ReturnsError()
    {
        var request = CreateValidRequest();
        request.ZipName = string.Empty;

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Postort");
    }

    #endregion

    #region Scout Group Tests

    [TestMethod]
    public async Task AddToWaitinglistAsync_ScoutGroupNotFound_ReturnsError()
    {
        _mockScoutGroupRepository
            .Setup(r => r.GetByIdAsync(1234, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScoutGroup?)null);

        var request = CreateValidRequest();

        var result = await _service.AddToWaitinglistAsync(1234, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "kunde inte hittas");
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_MissingApiKey_ReturnsError()
    {
        _mockScoutGroupRepository
            .Setup(r => r.GetByIdAsync(TestScoutGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScoutGroup
            {
                Id = TestScoutGroupId,
                Name = "Testscoutkåren",
                ApiKeyWaitinglist = null
            });

        var request = CreateValidRequest();

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "API-nyckel");
    }

    #endregion

    #region Successful Registration Tests

    [TestMethod]
    public async Task AddToWaitinglistAsync_ValidRequest_CallsApiAndReturnsSuccess()
    {
        var request = CreateValidRequest();
        SetupApiSuccess();

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(TestMemberNo, result.MemberNo);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_ApiReturnsError_ReturnsFailure()
    {
        var request = CreateValidRequest();

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WaitinglistRegistrationResult
            {
                Success = false,
                ErrorMessage = "Personnumret är redan registrerat"
            });

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Personnumret är redan registrerat", result.ErrorMessage);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_ApiThrowsException_ReturnsFailure()
    {
        var request = CreateValidRequest();

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScoutnetApiException("Connection failed"));

        var result = await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Connection failed");
    }

    #endregion

    #region Form Data Building Tests

    [TestMethod]
    public async Task AddToWaitinglistAsync_ValidRequest_SendsCorrectProfileData()
    {
        var request = CreateValidRequest();
        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);
        Assert.AreEqual("Anna", capturedFormData["profile[first_name]"]);
        Assert.AreEqual("Andersson", capturedFormData["profile[last_name]"]);
        Assert.AreEqual("201001152386", capturedFormData["profile[ssno]"]);
        Assert.AreEqual("anna.andersson@example.com", capturedFormData["profile[email]"]);
        Assert.AreEqual("2010-01-15", capturedFormData["profile[date_of_birth]"]);
        Assert.AreEqual("2", capturedFormData["profile[sex]"]); // Even second-to-last digit → female
        Assert.AreEqual("sv", capturedFormData["profile[preferred_culture]"]);
        Assert.AreEqual("1", capturedFormData["profile[newsletter]"]);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_ValidRequest_SendsCorrectAddressData()
    {
        var request = CreateValidRequest();
        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);
        Assert.AreEqual("Storgatan 1", capturedFormData["address_list[addresses][address_1][address_line1]"]);
        Assert.AreEqual("41301", capturedFormData["address_list[addresses][address_1][zip_code]"]);
        Assert.AreEqual("Göteborg", capturedFormData["address_list[addresses][address_1][zip_name]"]);
        Assert.AreEqual("0", capturedFormData["address_list[addresses][address_1][address_type]"]);
        Assert.AreEqual("752", capturedFormData["address_list[addresses][address_1][country_code]"]);
        Assert.AreEqual("1", capturedFormData["address_list[addresses][address_1][is_primary]"]);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_ValidRequest_SendsCorrectContactData()
    {
        var request = CreateValidRequest();
        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);
        Assert.AreEqual("0701234567", capturedFormData["contact_list[contacts][contact_1][details]"]);
        Assert.AreEqual("1", capturedFormData["contact_list[contacts][contact_1][contact_type_id]"]);
        Assert.AreEqual("031123456", capturedFormData["contact_list[contacts][contact_2][details]"]);
        Assert.AreEqual("2", capturedFormData["contact_list[contacts][contact_2][contact_type_id]"]);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_WithGuardians_SendsGuardianContactData()
    {
        var request = CreateValidRequest();
        request.Guardian1Name = "Erik Andersson";
        request.Guardian1Email = "erik@example.com";
        request.Guardian1Mobile = "0709876543";
        request.Guardian2Name = "Maria Andersson";

        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);

        // Guardian 1
        Assert.AreEqual("Erik Andersson", capturedFormData["contact_list[contacts][contact_3][details]"]);
        Assert.AreEqual("14", capturedFormData["contact_list[contacts][contact_3][contact_type_id]"]);
        Assert.AreEqual("erik@example.com", capturedFormData["contact_list[contacts][contact_4][details]"]);
        Assert.AreEqual("33", capturedFormData["contact_list[contacts][contact_4][contact_type_id]"]);
        Assert.AreEqual("0709876543", capturedFormData["contact_list[contacts][contact_5][details]"]);
        Assert.AreEqual("38", capturedFormData["contact_list[contacts][contact_5][contact_type_id]"]);

        // Guardian 2
        Assert.AreEqual("Maria Andersson", capturedFormData["contact_list[contacts][contact_7][details]"]);
        Assert.AreEqual("16", capturedFormData["contact_list[contacts][contact_7][contact_type_id]"]);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_NoOptionalContacts_OmitsEmptyContactFields()
    {
        var request = CreateValidRequest();
        request.Mobile = null;
        request.Phone = null;

        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);
        Assert.IsFalse(capturedFormData.ContainsKey("contact_list[contacts][contact_1][details]"));
        Assert.IsFalse(capturedFormData.ContainsKey("contact_list[contacts][contact_2][details]"));
        Assert.IsFalse(capturedFormData.ContainsKey("contact_list[contacts][contact_3][details]"));
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_MalePersonnummer_SetsSexToOne()
    {
        var request = CreateValidRequest();
        request.Personnummer = "20100115-2394"; // Odd second-to-last digit → male

        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);
        Assert.AreEqual("1", capturedFormData["profile[sex]"]);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_ValidRequest_SendsMembershipStatus()
    {
        var request = CreateValidRequest();
        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);
        Assert.AreEqual("1", capturedFormData["membership[status]"]);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_PersonnummerWithDash_NormalizesSsno()
    {
        var request = CreateValidRequest();
        request.Personnummer = "20100115-2386";

        Dictionary<string, string>? capturedFormData = null;

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, string, Dictionary<string, string>, CancellationToken>(
                (_, _, formData, _) => capturedFormData = formData)
            .ReturnsAsync(new WaitinglistRegistrationResult { Success = true, MemberNo = TestMemberNo });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        Assert.IsNotNull(capturedFormData);
        Assert.AreEqual("201001152386", capturedFormData["profile[ssno]"]);
    }

    #endregion

    #region Database Persistence Tests

    [TestMethod]
    public async Task AddToWaitinglistAsync_Success_CreatesPersonInDatabase()
    {
        var request = CreateValidRequest();
        SetupApiSuccess();

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        await using var context = new SkojjtDbContext(_dbOptions);
        var person = await context.Persons.FindAsync(TestMemberNo);

        Assert.IsNotNull(person);
        Assert.AreEqual("Anna", person.FirstName);
        Assert.AreEqual("Andersson", person.LastName);
        Assert.AreEqual("anna.andersson@example.com", person.Email);
        Assert.AreEqual("Storgatan 1", person.Street);
        Assert.AreEqual("41301", person.ZipCode);
        Assert.AreEqual("Göteborg", person.ZipName);
        Assert.AreEqual("0701234567", person.Mobile);
        Assert.AreEqual("031123456", person.Phone);
        Assert.AreEqual(new DateOnly(2010, 1, 15), person.BirthDate);
        Assert.IsNotNull(person.PersonalNumber);
        Assert.IsFalse(person.Removed);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_Success_CreatesScoutGroupPerson()
    {
        var request = CreateValidRequest();
        SetupApiSuccess();

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        await using var context = new SkojjtDbContext(_dbOptions);
        var sgp = await context.ScoutGroupPersons
            .FirstOrDefaultAsync(s => s.PersonId == TestMemberNo && s.ScoutGroupId == TestScoutGroupId);

        Assert.IsNotNull(sgp);
        Assert.IsFalse(sgp.NotInScoutnet);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_WithTroopId_CreatesTroopPerson()
    {
        var request = CreateValidRequest();
        SetupApiSuccess();

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request, TestTroopId);

        await using var context = new SkojjtDbContext(_dbOptions);
        var tp = await context.TroopPersons
            .FirstOrDefaultAsync(t => t.TroopId == TestTroopId && t.PersonId == TestMemberNo);

        Assert.IsNotNull(tp);
        Assert.IsFalse(tp.IsLeader);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_WithoutTroopId_DoesNotCreateTroopPerson()
    {
        var request = CreateValidRequest();
        SetupApiSuccess();

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        await using var context = new SkojjtDbContext(_dbOptions);
        var tp = await context.TroopPersons
            .FirstOrDefaultAsync(t => t.PersonId == TestMemberNo);

        Assert.IsNull(tp);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_WithGuardians_PersistsGuardianData()
    {
        var request = CreateValidRequest();
        request.Guardian1Name = "Erik Andersson";
        request.Guardian1Email = "erik@example.com";
        request.Guardian1Mobile = "0709876543";
        request.Guardian2Name = "Maria Andersson";
        request.Guardian2Email = "maria@example.com";
        request.Guardian2Mobile = "0701111111";

        SetupApiSuccess();

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        await using var context = new SkojjtDbContext(_dbOptions);
        var person = await context.Persons.FindAsync(TestMemberNo);

        Assert.IsNotNull(person);
        Assert.AreEqual("Erik Andersson", person.MumName);
        Assert.AreEqual("erik@example.com", person.MumEmail);
        Assert.AreEqual("0709876543", person.MumMobile);
        Assert.AreEqual("Maria Andersson", person.DadName);
        Assert.AreEqual("maria@example.com", person.DadEmail);
        Assert.AreEqual("0701111111", person.DadMobile);
    }

    [TestMethod]
    public async Task AddToWaitinglistAsync_ApiFailure_DoesNotCreatePerson()
    {
        var request = CreateValidRequest();

        _mockApiClient
            .Setup(c => c.RegisterMemberAsync(
                TestScoutGroupId,
                "test-waitinglist-key",
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WaitinglistRegistrationResult
            {
                Success = false,
                ErrorMessage = "Error"
            });

        await _service.AddToWaitinglistAsync(TestScoutGroupId, request);

        await using var context = new SkojjtDbContext(_dbOptions);
        var personCount = await context.Persons.CountAsync();
        Assert.AreEqual(0, personCount);
    }

    #endregion

    #region ValidateRequest Static Tests

    [TestMethod]
    public void ValidateRequest_ValidRequest_ReturnsNull()
    {
        var request = CreateValidRequest();
        var error = ScoutnetRegistrationService.ValidateRequest(request);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void ValidateRequest_InvalidPersonnummerChecksum_ReturnsError()
    {
        var request = CreateValidRequest();
        request.Personnummer = "20100115-0001"; // Invalid checksum

        var error = ScoutnetRegistrationService.ValidateRequest(request);

        Assert.IsNotNull(error);
        StringAssert.Contains(error, "ogiltigt");
    }

    #endregion
}
