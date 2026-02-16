using MudBlazor;
using MudBlazor.Utilities;

namespace Skojjt.Web.Theme;

/// <summary>
/// Custom MudBlazor theme for Skojjt using scout colors.
/// Primary color: #023A5A (Scout shirt blue)
/// </summary>
public static class SkojjtTheme
{
    // Scout colors
    public const string ScoutBlue = "#023A5A";
    public const string ScoutBlueLight = "#0D5A8A";
    public const string ScoutBlueDark = "#012840";
    public const string ScoutYellow = "#FFD700";
    
    public static MudTheme Theme => new()
    {
        PaletteLight = new PaletteLight
        {
            // Primary colors - Scout blue
            Primary = ScoutBlue,
            PrimaryContrastText = Colors.Shades.White,
            PrimaryLighten = "#1565A0",
            PrimaryDarken = ScoutBlueDark,
            
            // Secondary colors
            Secondary = "#5C6BC0",
            SecondaryContrastText = Colors.Shades.White,
            
            // Tertiary colors
            Tertiary = "#26A69A",
            TertiaryContrastText = Colors.Shades.White,
            
            // Info colors
            Info = "#2196F3",
            InfoContrastText = Colors.Shades.White,
            
            // Success colors
            Success = "#4CAF50",
            SuccessContrastText = Colors.Shades.White,
            
            // Warning colors
            Warning = "#FF9800",
            WarningContrastText = Colors.Shades.Black,
            
            // Error colors
            Error = "#F44336",
            ErrorContrastText = Colors.Shades.White,
            
            // Dark colors (for dark text on light backgrounds)
            Dark = "#212121",
            DarkContrastText = Colors.Shades.White,
            
            // Background colors
            Background = "#F5F5F5",
            BackgroundGray = "#E0E0E0",
            Surface = Colors.Shades.White,
            
            // Text colors
            TextPrimary = "#212121",
            TextSecondary = "#616161",
            TextDisabled = "#9E9E9E",
            
            // Action colors
            ActionDefault = "#616161",
            ActionDisabled = "#BDBDBD",
            ActionDisabledBackground = "#E0E0E0",
            
            // App bar
            AppbarBackground = ScoutBlue,
            AppbarText = Colors.Shades.White,
            
            // Drawer
            DrawerBackground = Colors.Shades.White,
            DrawerText = "#212121",
            DrawerIcon = "#616161",
            
            // Dividers and lines
            Divider = "#E0E0E0",
            DividerLight = "#EEEEEE",
            
            // Table colors
            TableLines = "#E0E0E0",
            TableStriped = "#FAFAFA",
            TableHover = "#F5F5F5",
            
            // Special: Leader row background - using a light blue that contrasts well
            LinesDefault = "#BBDEFB",
            LinesInputs = "#90CAF9",
        },
        
        PaletteDark = new PaletteDark
        {
            // Primary colors - Scout blue (lighter for dark mode)
            Primary = ScoutBlueLight,
            PrimaryContrastText = Colors.Shades.White,
            PrimaryLighten = "#4FC3F7",
            PrimaryDarken = ScoutBlue,
            
            // Secondary colors
            Secondary = "#7986CB",
            SecondaryContrastText = Colors.Shades.White,
            
            // Tertiary colors
            Tertiary = "#4DB6AC",
            TertiaryContrastText = Colors.Shades.White,
            
            // Info colors
            Info = "#64B5F6",
            InfoContrastText = Colors.Shades.Black,
            
            // Success colors
            Success = "#81C784",
            SuccessContrastText = Colors.Shades.Black,
            
            // Warning colors
            Warning = "#FFB74D",
            WarningContrastText = Colors.Shades.Black,
            
            // Error colors
            Error = "#E57373",
            ErrorContrastText = Colors.Shades.Black,
            
            // Dark colors
            Dark = "#E0E0E0",
            DarkContrastText = "#121212",
            
            // Background colors
            Background = "#121212",
            BackgroundGray = "#1E1E1E",
            Surface = "#1E1E1E",
            
            // Text colors
            TextPrimary = "#E0E0E0",
            TextSecondary = "#BDBDBD",
            TextDisabled = "#757575",
            
            // Action colors
            ActionDefault = "#BDBDBD",
            ActionDisabled = "#616161",
            ActionDisabledBackground = "#424242",
            
            // App bar
            AppbarBackground = "#1E1E1E",
            AppbarText = Colors.Shades.White,
            
            // Drawer
            DrawerBackground = "#1E1E1E",
            DrawerText = "#E0E0E0",
            DrawerIcon = "#BDBDBD",
            
            // Dividers and lines
            Divider = "#424242",
            DividerLight = "#2D2D2D",
            
            // Table colors
            TableLines = "#424242",
            TableStriped = "#262626",
            TableHover = "#2D2D2D",
            
            // Special: Leader row background - darker blue that contrasts well with white text
            LinesDefault = "#1A3A5C",
            LinesInputs = "#234B6E",
        },
        
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "4px",
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "260px"
        }
    };
}
