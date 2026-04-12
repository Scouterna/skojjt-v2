using Skojjt.Infrastructure.Scoutnet;

namespace Skojjt.Infrastructure.Tests.Scoutnet;

/// <summary>
/// Generates anonymized test data for Scoutnet import tests.
/// All names, personal numbers, and contact information are fictional.
/// </summary>
public static class ScoutnetTestDataGenerator
{
    private static readonly string[] FirstNames = 
    [
        "Anna", "Erik", "Maria", "Johan", "Emma", "Lars", "Karin", "Anders",
        "Sara", "Magnus", "Linda", "Peter", "Helena", "Mikael", "Eva", "Fredrik",
        "Sofia", "Gustav", "Ingrid", "Henrik", "Maja", "Oscar", "Linnea", "Viktor",
        "Olivia", "William", "Elsa", "Lucas", "Alice", "Liam", "Ella", "Noah"
    ];

    private static readonly string[] LastNames =
    [
        "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson",
        "Olsson", "Persson", "Svensson", "Gustafsson", "Pettersson", "Jonsson",
        "Jansson", "Hansson", "Bengtsson", "Lindberg", "Lindqvist", "Lindström",
        "Axelsson", "Bergström", "Sandberg", "Holmberg", "Lindgren", "Eklund"
    ];

    private static readonly string[] TroopNames =
    [
        "Nyckelpigan", "Trollsländan", "Humlorna", "Myrstacken", "Ekorrarna",
        "Ugglorna", "Bävrarna", "Vildkattarna", "Räven", "Spåret"
    ];

    private static readonly string[] PatrolNames =
    [
        "Örnen", "Vargen", "Björnen", "Lodjuret", "Älgen", "Räven",
        "Falken", "Korpen", "Vesslan", "Grävlingen", "Uttern", "Järven"
    ];

    private static readonly string[] Streets =
    [
        "Storgatan", "Parkvägen", "Skogsstigen", "Ängsvägen", "Björkallén",
        "Lindalléen", "Ekgatan", "Tallvägen", "Solvägen", "Månvägen"
    ];

    private static readonly string[] Cities =
    [
        "Göteborg", "Stockholm", "Malmö", "Uppsala", "Västerås",
        "Örebro", "Linköping", "Helsingborg", "Jönköping", "Norrköping"
    ];

    private static readonly Random _random = new(42); // Fixed seed for reproducible tests
	public const int DefaultScoutGroupId = 9999;

	/// <summary>
	/// Creates a ScoutnetMemberListResponse with the specified number of members.
	/// </summary>
	public static ScoutnetMemberListResponse CreateMemberListResponse(
        int memberCount,
        int scoutGroupId = DefaultScoutGroupId,
        string scoutGroupName = "Testscoutkåren",
        int troopCount = 3,
        bool includeLeaders = true)
    {
        var response = new ScoutnetMemberListResponse();
        var troops = GenerateTroops(troopCount, scoutGroupId);

        for (int i = 0; i < memberCount; i++)
        {
            var memberId = 1000 + i;
            var isLeader = includeLeaders && i < troopCount; // First N members are leaders
            var troop = troops[i % troops.Count];

            var member = CreateMember(
                memberId,
                scoutGroupId,
                scoutGroupName,
                troop.Id,
                troop.Name,
                isLeader);

            response.Data[memberId.ToString()] = member;
        }

        return response;
    }

    /// <summary>
    /// Creates a single ScoutnetMember with anonymized data.
    /// </summary>
    public static ScoutnetMember CreateMember(
        int memberId,
        int scoutGroupId = DefaultScoutGroupId,
        string scoutGroupName = "Testscoutkåren",
        int? troopId = null,
        string? troopName = null,
        bool isLeader = false,
        string? patrol = null)
    {
        var firstName = FirstNames[_random.Next(FirstNames.Length)];
        var lastName = LastNames[_random.Next(LastNames.Length)];
        var birthYear = isLeader ? _random.Next(1975, 2000) : _random.Next(2008, 2018);
        var birthMonth = _random.Next(1, 13);
        var birthDay = _random.Next(1, 29);
        var personalNumber = GeneratePersonalNumber(birthYear, birthMonth, birthDay, _random.Next(0, 2) == 0);

        return new ScoutnetMember
        {
            MemberNo = new ScoutnetValue { Value = memberId.ToString() },
            FirstName = new ScoutnetValue { Value = firstName },
            LastName = new ScoutnetValue { Value = lastName },
            DateOfBirth = new ScoutnetValue { Value = $"{birthYear:D4}-{birthMonth:D2}-{birthDay:D2}" },
            PersonalNumber = new ScoutnetValue { Value = personalNumber },
            Status = new ScoutnetValue { Value = "Aktiv" },
            Group = new ScoutnetValue { RawValue = scoutGroupId.ToString(), Value = scoutGroupName },
            Unit = troopId.HasValue 
                ? new ScoutnetValue { RawValue = troopId.Value.ToString(), Value = troopName ?? TroopNames[_random.Next(TroopNames.Length)] } 
                : null,
            UnitRole = isLeader ? new ScoutnetValue { RawValue = "2", Value = "Avdelningsledare" } : null,
            Patrol = patrol != null || !isLeader 
                ? new ScoutnetValue
                {
                    Value = patrol ?? PatrolNames[_random.Next(PatrolNames.Length)],
                    RawValue = (50000 + memberId).ToString()
                }
                : null,
            Email = new ScoutnetValue { Value = $"{firstName.ToLower()}.{lastName.ToLower()}@example.com" },
            AltEmail = new ScoutnetValue { Value = $"{firstName.ToLower()}{_random.Next(100, 999)}@example.org" },
            MobilePhone = new ScoutnetValue { Value = $"467{_random.Next(10000000, 99999999)}" },
            HomePhone = new ScoutnetValue { Value = $"0{_random.Next(100, 999)}{_random.Next(100000, 999999)}" },
            Address1 = new ScoutnetValue { Value = $"{Streets[_random.Next(Streets.Length)]} {_random.Next(1, 100)}" },
            Postcode = new ScoutnetValue { Value = $"{_random.Next(10000, 99999)}" },
            Town = new ScoutnetValue { Value = Cities[_random.Next(Cities.Length)] },
            MothersName = new ScoutnetValue { Value = $"{FirstNames[_random.Next(FirstNames.Length)]} {lastName}" },
            MothersEmail = new ScoutnetValue { Value = $"mamma.{lastName.ToLower()}@example.com" },
            MothersMobile = new ScoutnetValue { Value = $"467{_random.Next(10000000, 99999999)}" },
            FathersName = new ScoutnetValue { Value = $"{FirstNames[_random.Next(FirstNames.Length)]} {lastName}" },
            FathersEmail = new ScoutnetValue { Value = $"pappa.{lastName.ToLower()}@example.com" },
            FathersMobile = new ScoutnetValue { Value = $"467{_random.Next(10000000, 99999999)}" },
            GroupRole = isLeader ? new ScoutnetValue { Value = "Ledare, Styrelseledamot" } : null,
            Roles = isLeader && troopId.HasValue ? CreateLeaderRoles(troopId.Value) : null
        };
    }

    /// <summary>
    /// Creates a response that simulates a member being removed from Scoutnet.
    /// </summary>
    public static ScoutnetMemberListResponse CreateResponseWithRemovedMember(
        int[] existingMemberIds,
        int memberIdToRemove,
        int scoutGroupId = DefaultScoutGroupId)
    {
        var response = new ScoutnetMemberListResponse();
        
        foreach (var memberId in existingMemberIds)
        {
            if (memberId != memberIdToRemove)
            {
                response.Data[memberId.ToString()] = CreateMember(memberId, scoutGroupId);
            }
        }
        
        return response;
    }

    /// <summary>
    /// Creates a response with members in multiple troops for testing troop assignment logic.
    /// </summary>
    public static ScoutnetMemberListResponse CreateMultiTroopResponse(
        int scoutGroupId = DefaultScoutGroupId,
        string scoutGroupName = "Testscoutkåren")
    {
        var response = new ScoutnetMemberListResponse();
        var troops = GenerateTroops(3, scoutGroupId);

        // Members in Troop 1
        for (int i = 0; i < 8; i++)
        {
            var memberId = 1001 + i;
            var isLeader = i < 2;
            response.Data[memberId.ToString()] = CreateMember(
                memberId, scoutGroupId, scoutGroupName,
                troops[0].Id, troops[0].Name, isLeader);
        }

        // Members in Troop 2
        for (int i = 0; i < 10; i++)
        {
            var memberId = 1101 + i;
            var isLeader = i < 2;
            response.Data[memberId.ToString()] = CreateMember(
                memberId, scoutGroupId, scoutGroupName,
                troops[1].Id, troops[1].Name, isLeader);
        }

        // Members in Troop 3
        for (int i = 0; i < 6; i++)
        {
            var memberId = 1201 + i;
            var isLeader = i < 1;
            response.Data[memberId.ToString()] = CreateMember(
                memberId, scoutGroupId, scoutGroupName,
                troops[2].Id, troops[2].Name, isLeader);
        }

        return response;
    }

    /// <summary>
    /// Creates a response simulating a leader being assigned to multiple troops via roles.
    /// </summary>
    public static ScoutnetMemberListResponse CreateLeaderWithMultipleTroopRolesResponse(
        int scoutGroupId = DefaultScoutGroupId)
    {
        var response = new ScoutnetMemberListResponse();
        var troops = GenerateTroops(2, scoutGroupId);

        // Create a leader who is assigned to their primary troop
        var leaderMember = CreateMember(
            1001, scoutGroupId, "Testscoutkåren",
            troops[0].Id, troops[0].Name, isLeader: true);

        // Add additional troop role in the roles structure
        leaderMember.Roles = new ScoutnetRolesValue
        {
            Value = new ScoutnetRoles
            {
                Troop = new Dictionary<string, Dictionary<string, ScoutnetRole>>
                {
                    [troops[0].Id.ToString()] = new()
                    {
                        ["role_1"] = new ScoutnetRole { RoleId = 2, RoleKey = "leader", RoleName = "Avdelningsledare" }
                    },
                    [troops[1].Id.ToString()] = new()
                    {
                        ["role_2"] = new ScoutnetRole { RoleId = 4, RoleKey = "vice_leader", RoleName = "Vice avdelningsledare" }
                    }
                }
            }
        };

        response.Data["1001"] = leaderMember;

        // Add a scout to the second troop
        response.Data["1002"] = CreateMember(
            1002, scoutGroupId, "Testscoutkåren",
            troops[1].Id, troops[1].Name, isLeader: false);

        return response;
    }

    private static List<(int Id, string Name)> GenerateTroops(int count, int baseId)
    {
        var troops = new List<(int Id, string Name)>();
        for (int i = 0; i < count; i++)
        {
            troops.Add((baseId * 100 + 10 + i, TroopNames[i % TroopNames.Length]));
        }
        return troops;
    }

    private static string GeneratePersonalNumber(int year, int month, int day, bool isFemale)
    {
        // Format: YYYYMMDDXXXX where the second-to-last digit is even for female, odd for male
        var lastTwo = isFemale ? _random.Next(0, 50) * 2 : _random.Next(0, 50) * 2 + 1;
        var checkDigit = _random.Next(0, 10);
        return $"{year:D4}{month:D2}{day:D2}{_random.Next(10, 100)}{lastTwo:D2}{checkDigit}";
    }

    private static ScoutnetRolesValue? CreateLeaderRoles(int troopId)
    {
        return new ScoutnetRolesValue
        {
            Value = new ScoutnetRoles
            {
                Troop = new Dictionary<string, Dictionary<string, ScoutnetRole>>
                {
                    [troopId.ToString()] = new()
                    {
                        ["role_1"] = new ScoutnetRole { RoleId = 2, RoleKey = "leader", RoleName = "Avdelningsledare" }
                    }
                }
            }
        };
    }
}
