using Microsoft.EntityFrameworkCore;
using Skojjt.Core;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Computes person flow between troops across semesters for Sankey chart visualization.
/// Uses IDbContextFactory for Blazor Server compatibility.
/// </summary>
public class PersonFlowService : IPersonFlowService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;

    public PersonFlowService(IDbContextFactory<SkojjtDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PersonFlowResult> GetFlowAsync(
        int scoutGroupId,
        IReadOnlyList<int> semesterIds,
        bool includeProjection = false,
        CancellationToken cancellationToken = default)
    {
        if (semesterIds.Count < 2)
            return new PersonFlowResult { Columns = [], Links = [] };

        var orderedIds = semesterIds.OrderBy(id => id).ToList();

        await using var context = _contextFactory.CreateDbContext();

        // Base query: load troops with a recognized scout unit type
        IQueryable<Troop> query = context.Troops
            .Where(t => t.ScoutGroupId == scoutGroupId
                        && orderedIds.Contains(t.SemesterId)
                        && t.UnitTypeId.HasValue
                        && ScoutUnitTypes.ValidIds.Contains(t.UnitTypeId.Value))
            .Include(t => t.Semester)
            .Include(t => t.TroopPersons);

        // Only load Person data when projection needs birth dates
        if (includeProjection)
        {
            query = query.Include(t => t.TroopPersons)
                .ThenInclude(tp => tp.Person);
        }

        var allTroops = await query
            .OrderBy(t => t.SemesterId)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var troopsBySemester = allTroops
            .GroupBy(t => t.SemesterId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var columns = new List<FlowSemesterColumn>();
        var allLinks = new List<FlowLink>();

        // Build a stable global troop ordering by age group (oldest first),
        // then alphabetically by name within the same age group.
        var globalTroopOrder = allTroops
            .GroupBy(t => t.ScoutnetId)
            .Select(g =>
            {
                var representative = g.OrderBy(t => t.SemesterId).First();
                return new { representative.ScoutnetId, representative.UnitTypeId, representative.Name };
            })
            .OrderBy(t => ScoutUnitTypes.AgeSortOrder.GetValueOrDefault(t.UnitTypeId ?? 0, 99))
            .ThenBy(t => t.Name)
            .Select((t, idx) => (t.ScoutnetId, idx))
            .ToDictionary(x => x.ScoutnetId, x => x.idx);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var semesterId = orderedIds[i];
            var troops = troopsBySemester.GetValueOrDefault(semesterId, []);
            var semester = troops.FirstOrDefault()?.Semester;
            var label = semester?.DisplayName ?? FormatSemesterId(semesterId);

            var nodes = new List<FlowNode>();

            // Placeholder for "Slutat" node — always at the bottom of each column
            // (target of edges from the previous column's troop nodes)
            FlowNode? leftNodeForColumn = null;

            foreach (var troop in troops.OrderBy(t => globalTroopOrder.GetValueOrDefault(t.ScoutnetId, int.MaxValue)))
            {
                var memberCount = troop.TroopPersons.Count(tp => !tp.IsLeader);
                nodes.Add(new FlowNode
                {
                    Id = BuildNodeId(semesterId, troop.ScoutnetId, troop.Name),
                    Name = troop.Name,
                    PersonCount = memberCount,
                    IsSpecial = false
                });
            }

            if (i > 0)
            {
                var prevSemesterId = orderedIds[i - 1];
                var prevTroops = troopsBySemester.GetValueOrDefault(prevSemesterId, []);
                var (links, newNode, leftNode) = ComputeTransitionLinks(
                    prevTroops, prevSemesterId, troops, semesterId);

                allLinks.AddRange(links);

                // "Slutat" goes at the bottom of the CURRENT column
                // (edges come FROM previous column's troop nodes → TO this node)
                leftNodeForColumn = leftNode;

                // "Ny" goes at the top of the PREVIOUS column
                // (edges go FROM this node → TO current column's troop nodes)
                if (newNode != null)
                {
                    var prevColumn = columns[i - 1];
                    var updatedPrevNodes = prevColumn.Nodes.ToList();
                    updatedPrevNodes.Insert(0, newNode);
                    columns[i - 1] = new FlowSemesterColumn
                    {
                        SemesterId = prevColumn.SemesterId,
                        Label = prevColumn.Label,
                        ColumnIndex = prevColumn.ColumnIndex,
                        Nodes = updatedPrevNodes
                    };
                }
            }

            // Add "Slutat" at the bottom
            if (leftNodeForColumn != null)
                nodes.Add(leftNodeForColumn);

            columns.Add(new FlowSemesterColumn
            {
                SemesterId = semesterId,
                Label = label,
                ColumnIndex = i,
                Nodes = nodes
            });
        }

        if (includeProjection && columns.Count > 0)
        {
            var lastSemesterId = orderedIds[^1];
            var lastTroops = troopsBySemester.GetValueOrDefault(lastSemesterId, []);

            if (lastTroops.Count > 0)
            {
                var projectedColumn = BuildProjectedColumn(
                    lastTroops, lastSemesterId, columns.Count, globalTroopOrder, allLinks);
                if (projectedColumn != null)
                    columns.Add(projectedColumn);
            }
        }

        return new PersonFlowResult { Columns = columns, Links = allLinks };
    }

    /// <summary>
    /// Builds a projected next-semester column by assigning each member to a troop
    /// based on their age at the projected semester's start date.
    /// </summary>
    private static FlowSemesterColumn? BuildProjectedColumn(
        List<Troop> lastTroops,
        int lastSemesterId,
        int columnIndex,
        Dictionary<int, int> globalTroopOrder,
        List<FlowLink> allLinks)
    {
        // Compute the next semester ID
        var lastYear = lastSemesterId / 10;
        var lastIsAutumn = (lastSemesterId % 10) == 1;
        int projYear, projSemesterId;
        bool projIsAutumn;
        if (lastIsAutumn)
        {
            projYear = lastYear + 1;
            projIsAutumn = false;
        }
        else
        {
            projYear = lastYear;
            projIsAutumn = true;
        }
        projSemesterId = (projYear * 10) + (projIsAutumn ? 1 : 0);

        // Reference year for the projected semester — used for age calculation.
        // In Swedish scouting, age grouping uses the age the person turns by
        // end of year (birth month/day is irrelevant).
        var projAgeYear = projYear;

        // Build available troops (assume same troops exist next semester)
        // Map: UnitTypeId → (ScoutnetId, TroopName)
        var troopsByUnitType = lastTroops
            .Where(t => t.UnitTypeId.HasValue)
            .GroupBy(t => t.UnitTypeId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // For each non-leader member in the last semester, project their troop.
        // Members whose age falls outside all defined age ranges are excluded.
        var projectedCounts = new Dictionary<(string From, int ToScoutnetId, string ToName), int>();

        foreach (var troop in lastTroops)
        {
            foreach (var tp in troop.TroopPersons.Where(tp => !tp.IsLeader))
            {
                var birthDate = tp.Person.BirthDate;
                if (!birthDate.HasValue)
                {
                    // No birth date — assume stays in same troop
                    var key = (BuildNodeId(lastSemesterId, troop.ScoutnetId, troop.Name),
                               troop.ScoutnetId, troop.Name);
                    projectedCounts[key] = projectedCounts.GetValueOrDefault(key) + 1;
                    continue;
                }

                // Age the person turns during the projected year (birth month irrelevant)
                var ageAtProjection = projAgeYear - birthDate.Value.Year;

                // Find the best matching troop by age range
                var matchedTroop = FindTroopForAge(ageAtProjection, troopsByUnitType, troop);

                if (matchedTroop != null)
                {
                    var key = (BuildNodeId(lastSemesterId, troop.ScoutnetId, troop.Name),
                               matchedTroop.ScoutnetId, matchedTroop.Name);
                    projectedCounts[key] = projectedCounts.GetValueOrDefault(key) + 1;
                }
                // else: age outside all defined ranges — skip from projection
            }
        }

        // Build projected nodes and links
        var projectedNodeCounts = new Dictionary<int, int>(); // ScoutnetId → count
        var projectedLinks = new List<FlowLink>();

        foreach (var (key, count) in projectedCounts)
        {
            var (fromNodeId, toScoutnetId, toName) = key;
            var toNodeId = BuildNodeId(projSemesterId, toScoutnetId, toName);
            projectedLinks.Add(new FlowLink
            {
                SourceNodeId = fromNodeId,
                TargetNodeId = toNodeId,
                Value = count
            });
            projectedNodeCounts[toScoutnetId] =
                projectedNodeCounts.GetValueOrDefault(toScoutnetId) + count;
        }

        allLinks.AddRange(projectedLinks);

        var projLabel = $"{(projIsAutumn ? "HT" : "VT")} {projYear} (proj.)";
        var nodes = new List<FlowNode>();

        foreach (var troop in lastTroops.OrderBy(t => globalTroopOrder.GetValueOrDefault(t.ScoutnetId, int.MaxValue)))
        {
            if (projectedNodeCounts.TryGetValue(troop.ScoutnetId, out var memberCount) && memberCount > 0)
            {
                nodes.Add(new FlowNode
                {
                    Id = BuildNodeId(projSemesterId, troop.ScoutnetId, troop.Name),
                    Name = troop.Name,
                    PersonCount = memberCount,
                    IsSpecial = false
                });
            }
        }

        if (nodes.Count == 0) return null;

        return new FlowSemesterColumn
        {
            SemesterId = projSemesterId,
            Label = projLabel,
            ColumnIndex = columnIndex,
            Nodes = nodes
        };
    }

    /// <summary>
    /// Finds the best troop for a member of a given age.
    /// Prefers the member's current troop if age still fits, otherwise picks the
    /// first troop whose age range matches (by age order, youngest suitable first).
    /// </summary>
    private static Troop? FindTroopForAge(
        int age,
        Dictionary<int, Troop> troopsByUnitType,
        Troop currentTroop)
    {
        // Check if member still fits in their current troop
        if (currentTroop.UnitTypeId.HasValue &&
            ScoutUnitTypes.AgeRanges.TryGetValue(currentTroop.UnitTypeId.Value, out var currentRange) &&
            age >= currentRange.MinAge && age <= currentRange.MaxAge)
        {
            return currentTroop;
        }

        // Find the best matching troop by age (prefer the "next" age group up)
        foreach (var (unitTypeId, (minAge, maxAge)) in ScoutUnitTypes.AgeRanges
            .Where(kv => kv.Key != 7) // Skip "Annat"
            .OrderBy(kv => kv.Value.MinAge))
        {
            if (age >= minAge && age <= maxAge && troopsByUnitType.TryGetValue(unitTypeId, out var troop))
            {
                return troop;
            }
        }

        // Age doesn't fit any defined range — exclude from projection
        return null;
    }

    private static (List<FlowLink> Links, FlowNode? NewNode, FlowNode? LeftNode) ComputeTransitionLinks(
        List<Troop> prevTroops, int prevSemesterId,
        List<Troop> currTroops, int currSemesterId)
    {
        var prevMap = BuildPersonTroopMap(prevTroops);
        var currMap = BuildPersonTroopMap(currTroops);

        var transitionCounts = new Dictionary<(string From, string To), int>();
        var leftCount = 0;
        var newCount = 0;

        var leftNodeId = BuildSpecialNodeId(currSemesterId, "Slutat");
        var newNodeId = BuildSpecialNodeId(currSemesterId, "Ny");

        foreach (var (personId, prevEntries) in prevMap)
        {
            if (currMap.TryGetValue(personId, out var currEntries))
            {
                foreach (var prev in prevEntries)
                {
                    foreach (var curr in currEntries)
                    {
                        var key = (BuildNodeId(prevSemesterId, prev.ScoutnetId, prev.Name),
                                   BuildNodeId(currSemesterId, curr.ScoutnetId, curr.Name));
                        transitionCounts[key] = transitionCounts.GetValueOrDefault(key) + 1;
                    }
                }
            }
            else
            {
                // Count each troop-membership that ended (not unique persons)
                // so the node size matches the sum of incoming edge values.
                foreach (var prev in prevEntries)
                {
                    var key = (BuildNodeId(prevSemesterId, prev.ScoutnetId, prev.Name), leftNodeId);
                    transitionCounts[key] = transitionCounts.GetValueOrDefault(key) + 1;
                    leftCount++;
                }
            }
        }

        foreach (var (personId, currEntries) in currMap)
        {
            if (!prevMap.ContainsKey(personId))
            {
                // Count each new troop-membership (not unique persons)
                // so the node size matches the sum of outgoing edge values.
                foreach (var curr in currEntries)
                {
                    var key = (newNodeId, BuildNodeId(currSemesterId, curr.ScoutnetId, curr.Name));
                    transitionCounts[key] = transitionCounts.GetValueOrDefault(key) + 1;
                    newCount++;
                }
            }
        }

        var links = transitionCounts
            .Where(kv => kv.Value > 0)
            .Select(kv => new FlowLink
            {
                SourceNodeId = kv.Key.From,
                TargetNodeId = kv.Key.To,
                Value = kv.Value
            })
            .ToList();

        FlowNode? leftNode = leftCount > 0
            ? new FlowNode { Id = leftNodeId, Name = "Slutat", PersonCount = leftCount, IsSpecial = true }
            : null;

        FlowNode? newNode = newCount > 0
            ? new FlowNode { Id = newNodeId, Name = "Ny", PersonCount = newCount, IsSpecial = true }
            : null;

        return (links, newNode, leftNode);
    }

    private static Dictionary<int, List<(int ScoutnetId, string Name)>> BuildPersonTroopMap(List<Troop> troops)
    {
        var map = new Dictionary<int, List<(int ScoutnetId, string Name)>>();
        foreach (var troop in troops)
        {
            foreach (var tp in troop.TroopPersons.Where(tp => !tp.IsLeader))
            {
                if (!map.TryGetValue(tp.PersonId, out var list))
                {
                    list = [];
                    map[tp.PersonId] = list;
                }
                list.Add((troop.ScoutnetId, troop.Name));
            }
        }
        return map;
    }

    private static string BuildNodeId(int semesterId, int scoutnetId, string troopName)
        => $"s{semesterId}_t{scoutnetId}_{troopName}";

    private static string BuildSpecialNodeId(int semesterId, string specialName)
        => $"s{semesterId}_{specialName}";

    private static string FormatSemesterId(int semesterId)
    {
        var year = semesterId / 10;
        var isAutumn = (semesterId % 10) == 1;
        return $"{(isAutumn ? "HT" : "VT")} {year}";
    }
}
