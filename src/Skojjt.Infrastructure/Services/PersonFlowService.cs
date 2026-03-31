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
        // Map: UnitTypeId → list of troops (multiple troops may share the same age group)
        var troopsByUnitType = lastTroops
            .Where(t => t.UnitTypeId.HasValue)
            .GroupBy(t => t.UnitTypeId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Phase 1: For each non-leader member, determine whether they stay in their
        // current troop or move to a different unit type.
        // Direct assignments (stays in current troop or no birth date) go straight
        // into projectedCounts. Members moving to a new unit type are collected in
        // pendingDistribution so they can be spread evenly across target troops.
        var projectedCounts = new Dictionary<(string From, int ToScoutnetId, string ToName), int>();
        var pendingDistribution = new Dictionary<(string SourceNodeId, int TargetUnitTypeId), int>();

        foreach (var troop in lastTroops)
        {
            var sourceNodeId = BuildNodeId(lastSemesterId, troop.ScoutnetId, troop.Name);

            foreach (var tp in troop.TroopPersons.Where(tp => !tp.IsLeader))
            {
                var birthDate = tp.Person.BirthDate;
                if (!birthDate.HasValue)
                {
                    // No birth date — assume stays in same troop
                    var key = (sourceNodeId, troop.ScoutnetId, troop.Name);
                    projectedCounts[key] = projectedCounts.GetValueOrDefault(key) + 1;
                    continue;
                }

                // Age the person turns during the projected year (birth month irrelevant)
                var ageAtProjection = projAgeYear - birthDate.Value.Year;

                // Check if member still fits in their current troop
                if (troop.UnitTypeId.HasValue &&
                    ScoutUnitTypes.AgeRanges.TryGetValue(troop.UnitTypeId.Value, out var currentRange) &&
                    ageAtProjection >= currentRange.MinAge && ageAtProjection <= currentRange.MaxAge)
                {
                    var key = (sourceNodeId, troop.ScoutnetId, troop.Name);
                    projectedCounts[key] = projectedCounts.GetValueOrDefault(key) + 1;
                    continue;
                }

                // Find target unit type for age-based move
                var targetUnitTypeId = FindUnitTypeForAge(ageAtProjection, troopsByUnitType);
                if (targetUnitTypeId.HasValue)
                {
                    var pendingKey = (sourceNodeId, targetUnitTypeId.Value);
                    pendingDistribution[pendingKey] = pendingDistribution.GetValueOrDefault(pendingKey) + 1;
                }
                // else: age outside all defined ranges — skip from projection
            }
        }

        // Phase 2: Distribute pending members evenly across troops of the same
        // unit type. Uses running counts (seeded from direct assignments) so the
        // final projected troop sizes are as balanced as possible.
        var directCountsPerTroop = new Dictionary<int, int>();
        foreach (var ((_, scoutnetId, _), count) in projectedCounts)
        {
            directCountsPerTroop[scoutnetId] = directCountsPerTroop.GetValueOrDefault(scoutnetId) + count;
        }

        foreach (var group in pendingDistribution.GroupBy(kv => kv.Key.TargetUnitTypeId))
        {
            var unitTypeId = group.Key;
            var troops = troopsByUnitType[unitTypeId];
            var sources = group.Select(kv => (kv.Key.SourceNodeId, Count: kv.Value)).ToList();

            if (troops.Count <= 1)
            {
                var troop = troops[0];
                foreach (var (src, count) in sources)
                {
                    var key = (src, troop.ScoutnetId, troop.Name);
                    projectedCounts[key] = projectedCounts.GetValueOrDefault(key) + count;
                }
            }
            else
            {
                // Seed running counts with members already staying in these troops
                var troopRunningCounts = troops.ToDictionary(
                    t => t.ScoutnetId,
                    t => directCountsPerTroop.GetValueOrDefault(t.ScoutnetId));

                foreach (var (src, count) in sources)
                {
                    for (var m = 0; m < count; m++)
                    {
                        // Assign to the troop with fewest members for balance
                        var targetTroop = troops.MinBy(t => troopRunningCounts[t.ScoutnetId])!;
                        var key = (src, targetTroop.ScoutnetId, targetTroop.Name);
                        projectedCounts[key] = projectedCounts.GetValueOrDefault(key) + 1;
                        troopRunningCounts[targetTroop.ScoutnetId]++;
                    }
                }
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
    /// Finds the unit type ID for a member of a given age.
    /// Returns the first unit type whose age range matches (ordered youngest first).
    /// </summary>
    private static int? FindUnitTypeForAge(
        int age,
        Dictionary<int, List<Troop>> troopsByUnitType)
    {
        foreach (var (unitTypeId, (minAge, maxAge)) in ScoutUnitTypes.AgeRanges
            .Where(kv => kv.Key != 7) // Skip "Annat"
            .OrderBy(kv => kv.Value.MinAge))
        {
            if (age >= minAge && age <= maxAge && troopsByUnitType.ContainsKey(unitTypeId))
            {
                return unitTypeId;
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
