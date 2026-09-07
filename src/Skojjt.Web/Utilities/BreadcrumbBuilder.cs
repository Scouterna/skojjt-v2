using System.Globalization;
using MudBlazor;

namespace Skojjt.Web.Utilities;

/// <summary>
/// Fluent builder for the breadcrumb trails used by the pages.
/// Always starts with the "Scoutkårer" root and produces invariant URLs.
/// </summary>
public sealed class BreadcrumbBuilder
{
    private const string RootLabel = "Scoutkårer";
    private const string RootHref = "/sk";
    private const string DefaultScoutGroupLabel = "Scoutkår";
    private const string DefaultTroopLabel = "Avdelning";
    private const string NoAccessLabel = "Ingen behörighet";

    private readonly List<BreadcrumbItem> _items = [new BreadcrumbItem(RootLabel, href: RootHref)];

    private BreadcrumbBuilder()
    {
    }

    /// <summary>
    /// Starts a new trail containing only the "Scoutkårer" root.
    /// </summary>
    public static BreadcrumbBuilder Create() => new();

    /// <summary>
    /// The trail shown when the user lacks permission for the requested page.
    /// </summary>
    public static List<BreadcrumbItem> NoAccess() => Create().Leaf(NoAccessLabel);

    /// <summary>
    /// Adds a link to the scout group start page.
    /// </summary>
    public BreadcrumbBuilder ScoutGroup(string? name, int scoutGroupId) =>
        Link(name ?? DefaultScoutGroupLabel, Url($"/sk/{scoutGroupId}"));

    /// <summary>
    /// Adds a link to the semester page of a scout group.
    /// </summary>
    public BreadcrumbBuilder Semester(string? name, int scoutGroupId, int semesterId) =>
        Link(name ?? string.Empty, Url($"/sk/{scoutGroupId}/t/{semesterId}"));

    /// <summary>
    /// Adds a link to a troop page.
    /// </summary>
    public BreadcrumbBuilder Troop(string? name, int scoutGroupId, int semesterId, int troopScoutnetId) =>
        Link(name ?? DefaultTroopLabel, Url($"/sk/{scoutGroupId}/t/{semesterId}/{troopScoutnetId}"));

    /// <summary>
    /// Adds a link to the member list of a scout group.
    /// </summary>
    public BreadcrumbBuilder Members(int scoutGroupId) =>
        Link("Medlemmar", Url($"/sk/{scoutGroupId}/p"));

    /// <summary>
    /// Adds a link to the badge list of a scout group.
    /// </summary>
    public BreadcrumbBuilder Badges(int scoutGroupId) =>
        Link("Märken", Url($"/sk/{scoutGroupId}/badges"));

    /// <summary>
    /// Adds an arbitrary link.
    /// </summary>
    public BreadcrumbBuilder Link(string label, string href)
    {
        _items.Add(new BreadcrumbItem(label, href: href));
        return this;
    }

    /// <summary>
    /// Adds the final, non-clickable item and returns the complete trail.
    /// </summary>
    public List<BreadcrumbItem> Leaf(string label)
    {
        _items.Add(new BreadcrumbItem(label, href: null, disabled: true));
        return _items;
    }

    /// <summary>
    /// Returns the trail as built so far, without adding a final item.
    /// </summary>
    public List<BreadcrumbItem> Build() => _items;

    private static string Url(FormattableString url) => url.ToString(CultureInfo.InvariantCulture);
}
