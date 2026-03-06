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
            Secondary = "#4A56A0",
            SecondaryContrastText = Colors.Shades.White,

            // Tertiary colors - Scout teal/green
            Tertiary = "#1E8C82",
            TertiaryContrastText = Colors.Shades.White,

            // Info colors
            Info = "#1976D2",
            InfoContrastText = Colors.Shades.White,

            // Success colors
            Success = "#388E3C",
            SuccessContrastText = Colors.Shades.White,

            // Warning colors
            Warning = "#F57C00",
            WarningContrastText = Colors.Shades.White,

            // Error colors
            Error = "#D32F2F",
            ErrorContrastText = Colors.Shades.White,

            // Dark colors (for dark text on light backgrounds)
            Dark = "#1A2027",
            DarkContrastText = Colors.Shades.White,

            // Background colors - visible contrast between bg and surface cards
            Background = "#EBEEF2",
            BackgroundGray = "#DEE2E8",
            Surface = Colors.Shades.White,

            // Text colors - high contrast for readability
            TextPrimary = "#0F1419",
            TextSecondary = "#3D5060",
            TextDisabled = "#90A4AE",

            // Action colors
            ActionDefault = "#3D5060",
            ActionDisabled = "#B0BEC5",
            ActionDisabledBackground = "#DEE2E8",

            // App bar - deep scout blue
            AppbarBackground = ScoutBlue,
            AppbarText = Colors.Shades.White,

            // Drawer - white with clear visual boundary
            DrawerBackground = Colors.Shades.White,
            DrawerText = "#0F1419",
            DrawerIcon = "#3D5060",

            // Dividers and lines - visible but not heavy
            Divider = "#CFD8DC",
            DividerLight = "#ECEFF1",

            // Table colors
            TableLines = "#CFD8DC",
            TableStriped = "#F5F7FA",
            TableHover = "#E8ECF1",

            // Special: Leader row background - using a light blue that contrasts well
            LinesDefault = "#BBDEFB",
            LinesInputs = "#90CAF9",
        },

        PaletteDark = new PaletteDark
        {
            // Primary colors - bright enough to read on dark surfaces (WCAG AA)
            Primary = "#5BB8E8",
            PrimaryContrastText = "#0F1214",
            PrimaryLighten = "#8AD0F0",
            PrimaryDarken = "#3A96C8",

            // Secondary colors
            Secondary = "#B0B8E8",
            SecondaryContrastText = "#0F1214",

            // Tertiary colors
            Tertiary = "#80CBC4",
            TertiaryContrastText = "#0F1214",

            // Info colors
            Info = "#64B5F6",
            InfoContrastText = "#0F1214",

            // Success colors
            Success = "#81C784",
            SuccessContrastText = "#0F1214",

            // Warning colors
            Warning = "#FFB74D",
            WarningContrastText = "#0F1214",

            // Error colors
            Error = "#EF9A9A",
            ErrorContrastText = "#0F1214",

            // Dark colors
            Dark = "#E8EAED",
            DarkContrastText = "#0F1214",

            // Background colors - three-tier layering: bg < surface < elevated
            Background = "#0F1214",
            BackgroundGray = "#161A1F",
            Surface = "#1E2329",

            // Text colors - higher contrast for readability
            TextPrimary = "#ECEFF1",
            TextSecondary = "#B0BEC5",
            TextDisabled = "#546E7A",

            // Action colors
            ActionDefault = "#B0BEC5",
            ActionDisabled = "#546E7A",
            ActionDisabledBackground = "#263238",

            // App bar - visually distinct from page surface
            AppbarBackground = "#141820",
            AppbarText = "#ECEFF1",

            // Drawer - slightly lighter than appbar for visual distinction
            DrawerBackground = "#1A1F26",
            DrawerText = "#ECEFF1",
            DrawerIcon = "#B0BEC5",

            // Dividers and lines - visible separators
            Divider = "#37414B",
            DividerLight = "#2A323A",

            // Table colors - distinct stripe and hover
            TableLines = "#37414B",
            TableStriped = "#1A1F26",
            TableHover = "#263038",

            // Special: Leader row background - darker blue that contrasts well with white text
            LinesDefault = "#1A3A5C",
            LinesInputs = "#234B6E",
        },

        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "Helvetica Neue", "Helvetica", "Arial", "sans-serif"],
                LetterSpacing = "normal",
            },
            H4 = new H4Typography
            {
                FontWeight = "600",
            },
            H5 = new H5Typography
            {
                FontWeight = "600",
            },
            H6 = new H6Typography
            {
                FontWeight = "600",
            },
            Button = new ButtonTypography
            {
                FontWeight = "500",
                TextTransform = "none",
            },
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "260px",
        }
    };
}
