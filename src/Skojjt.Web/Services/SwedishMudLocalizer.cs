using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Skojjt.Web.Services;

/// <summary>
/// Swedish translations for MudBlazor components.
/// </summary>
public class SwedishMudLocalizer : MudLocalizer
{
    private readonly Dictionary<string, string> _translations = new()
    {
        // Snackbar
        ["MudSnackbar_Close"] = "Stäng",
        
        // DataGrid / Table
        ["MudDataGrid.AddFilter"] = "Lägg till filter",
        ["MudDataGrid.Apply"] = "Tillämpa",
        ["MudDataGrid.Cancel"] = "Avbryt",
        ["MudDataGrid.Clear"] = "Rensa",
        ["MudDataGrid.CollapseAllGroups"] = "Fäll ihop alla grupper",
        ["MudDataGrid.Column"] = "Kolumn",
        ["MudDataGrid.Columns"] = "Kolumner",
        ["MudDataGrid.contains"] = "innehåller",
        ["MudDataGrid.ends with"] = "slutar med",
        ["MudDataGrid.equals"] = "är lika med",
        ["MudDataGrid.ExpandAllGroups"] = "Expandera alla grupper",
        ["MudDataGrid.Filter"] = "Filter",
        ["MudDataGrid.False"] = "Falskt",
        ["MudDataGrid.Group"] = "Grupp",
        ["MudDataGrid.Hide"] = "Dölj",
        ["MudDataGrid.HideAll"] = "Dölj alla",
        ["MudDataGrid.is"] = "är",
        ["MudDataGrid.is after"] = "är efter",
        ["MudDataGrid.is before"] = "är före",
        ["MudDataGrid.is empty"] = "är tom",
        ["MudDataGrid.is not"] = "är inte",
        ["MudDataGrid.is not empty"] = "är inte tom",
        ["MudDataGrid.is on or after"] = "är på eller efter",
        ["MudDataGrid.is on or before"] = "är på eller före",
        ["MudDataGrid.MoveDown"] = "Flytta ner",
        ["MudDataGrid.MoveUp"] = "Flytta upp",
        ["MudDataGrid.not contains"] = "innehåller inte",
        ["MudDataGrid.not equals"] = "är inte lika med",
        ["MudDataGrid.Operator"] = "Operator",
        ["MudDataGrid.RefreshData"] = "Uppdatera data",
        ["MudDataGrid.Save"] = "Spara",
        ["MudDataGrid.ShowAll"] = "Visa alla",
        ["MudDataGrid.starts with"] = "börjar med",
        ["MudDataGrid.True"] = "Sant",
        ["MudDataGrid.Ungroup"] = "Ta bort gruppering",
        ["MudDataGrid.Unsort"] = "Ta bort sortering",
        ["MudDataGrid.Value"] = "Värde",
        
        // Pagination
        ["MudTablePager.All"] = "Alla",
        ["MudTablePager.First"] = "Första",
        ["MudTablePager.Last"] = "Sista",
        ["MudTablePager.Previous"] = "Föregående",
        ["MudTablePager.Next"] = "Nästa",
        ["MudTablePager.RowsPerPage"] = "Rader per sida:",
        ["MudTablePager.InfoFormat"] = "{first_item}-{last_item} av {all_items}",
        
        // DatePicker
        ["MudDatePicker.Today"] = "Idag",
        ["MudDatePicker.Clear"] = "Rensa",
        ["MudDatePicker.Cancel"] = "Avbryt",
        ["MudDatePicker.OK"] = "OK",
        
        // TimePicker
        ["MudTimePicker.Now"] = "Nu",
        ["MudTimePicker.Clear"] = "Rensa",
        ["MudTimePicker.Cancel"] = "Avbryt",
        ["MudTimePicker.OK"] = "OK",
        
        // ColorPicker
        ["MudColorPicker.Cancel"] = "Avbryt",
        ["MudColorPicker.OK"] = "OK",
        
        // FileUpload
        ["MudFileUpload.Clear"] = "Rensa",
        
        // Dialog
        ["MudDialog.Ok"] = "OK",
        ["MudDialog.Cancel"] = "Avbryt",
        ["MudDialog.Yes"] = "Ja",
        ["MudDialog.No"] = "Nej",
        
        // Autocomplete
        ["MudAutocomplete.Clear"] = "Rensa",
        ["MudAutocomplete.NoItemsFound"] = "Inga träffar",
    };

    public override LocalizedString this[string key]
    {
        get
        {
            if (_translations.TryGetValue(key, out var translation))
            {
                return new LocalizedString(key, translation);
            }
            return new LocalizedString(key, key, resourceNotFound: true);
        }
    }
}
