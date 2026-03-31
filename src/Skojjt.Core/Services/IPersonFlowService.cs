namespace Skojjt.Core.Services;

/// <summary>
/// Service for computing person flow between troops across semesters.
/// Used to visualize how members move between avdelningar over time.
/// </summary>
public interface IPersonFlowService
{
    /// <summary>
    /// Computes person flow between consecutive semesters for a scout group.
    /// </summary>
    /// <param name="includeProjection">
    /// When true, appends a projected next-semester column based on member ages
    /// and troop age ranges, assuming the same troops exist.
    /// </param>
    Task<PersonFlowResult> GetFlowAsync(
        int scoutGroupId,
        IReadOnlyList<int> semesterIds,
        bool includeProjection = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Complete result for rendering a person flow Sankey chart.
/// </summary>
public class PersonFlowResult
{
    public required IReadOnlyList<FlowSemesterColumn> Columns { get; init; }
    public required IReadOnlyList<FlowLink> Links { get; init; }
}

/// <summary>
/// A single semester column in the Sankey chart.
/// </summary>
public class FlowSemesterColumn
{
    public required int SemesterId { get; init; }
    public required string Label { get; init; }
    public required int ColumnIndex { get; init; }
    public required IReadOnlyList<FlowNode> Nodes { get; init; }
}

/// <summary>
/// A node representing a troop (or special "Ny"/"Slutat" node).
/// </summary>
public class FlowNode
{
    /// <summary>
    /// Internal unique ID, e.g. "s20251_t123_Spårarna".
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Short display name, e.g. "Spårarna", "Ny", "Slutat".
    /// </summary>
    public required string Name { get; init; }

    public required int PersonCount { get; init; }
    public required bool IsSpecial { get; init; }
}

/// <summary>
/// A link between two nodes representing person movement.
/// </summary>
public class FlowLink
{
    public required string SourceNodeId { get; init; }
    public required string TargetNodeId { get; init; }
    public required int Value { get; init; }
}
