using MudBlazor;

namespace ExportDocGen.Components;

/// <summary>
/// The house theme. Two faces of one system:
///   • light  = "Ledger"  — warm paper, deep customs-green, serif headings
///   • dark   = "Console" — slate panels, filtration-teal, sans headings
/// Body text is Inter in both modes; the light-only serif headings are applied
/// in <c>app.css</c> by overriding <c>--mud-typography-*-family</c> under
/// <c>.t-light</c> (kept out of the theme so dialogs stay sans everywhere).
/// </summary>
public static class AppTheme
{
    private static readonly string[] Sans =
        ["Inter", "IBM Plex Sans", "Segoe UI", "Helvetica Neue", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1F5C43",
            PrimaryContrastText = "#F7F8F3",
            Secondary = "#8A5A12",
            SecondaryContrastText = "#FBFBF9",
            Tertiary = "#3D5A80",

            Black = "#1B1C18",
            White = "#FFFFFF",

            Background = "#FBFBF9",
            BackgroundGray = "#F1F1EC",
            Surface = "#FFFFFF",

            AppbarBackground = "#FBFBF9",
            AppbarText = "#1B1C18",
            DrawerBackground = "#F4F4F0",
            DrawerText = "#43453D",
            DrawerIcon = "#6C6E63",

            TextPrimary = "#1B1C18",
            TextSecondary = "#5C5E52",
            TextDisabled = "rgba(27,28,24,0.38)",
            ActionDefault = "#6C6E63",
            ActionDisabled = "rgba(27,28,24,0.26)",
            ActionDisabledBackground = "rgba(27,28,24,0.12)",

            Divider = "#E5E4DA",
            DividerLight = "#EEEDE5",
            LinesDefault = "#E0DFD3",
            LinesInputs = "#CFCEBF",
            TableLines = "#E5E4DA",
            TableStriped = "#F6F5EF",
            TableHover = "#EEEDE3",

            Success = "#1F5C43",
            Warning = "#8A5A12",
            Error = "#9E3A2E",
            Info = "#3D5A80",
            HoverOpacity = 0.06,
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#3FB8A8",
            PrimaryContrastText = "#07201D",
            Secondary = "#E0A13C",
            SecondaryContrastText = "#1B1205",
            Tertiary = "#7FA8D9",

            Black = "#0F1216",
            White = "#E9ECF1",

            Background = "#13161B",
            BackgroundGray = "#0F1216",
            Surface = "#171B21",

            AppbarBackground = "#13161B",
            AppbarText = "#E9ECF1",
            DrawerBackground = "#171B21",
            DrawerText = "#B9C0CA",
            DrawerIcon = "#828B98",

            TextPrimary = "#E9ECF1",
            TextSecondary = "#98A1AC",
            TextDisabled = "rgba(233,236,241,0.36)",
            ActionDefault = "#98A1AC",
            ActionDisabled = "rgba(233,236,241,0.26)",
            ActionDisabledBackground = "rgba(233,236,241,0.12)",

            Divider = "#242932",
            DividerLight = "#1E232B",
            LinesDefault = "#242932",
            LinesInputs = "#333A45",
            TableLines = "#242932",
            TableStriped = "#191D24",
            TableHover = "#1A1F27",

            Success = "#3FB8A8",
            Warning = "#E0A13C",
            Error = "#E5806F",
            Info = "#7FA8D9",
            HoverOpacity = 0.08,
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            DrawerWidthLeft = "232px",
            AppbarHeight = "60px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Sans },

            H1 = new H1Typography { FontFamily = Sans, LetterSpacing = "-.02em" },
            H2 = new H2Typography { FontFamily = Sans, LetterSpacing = "-.02em" },
            H3 = new H3Typography { FontFamily = Sans, LetterSpacing = "-.015em" },
            H4 = new H4Typography { FontFamily = Sans, LetterSpacing = "-.015em" },
            H5 = new H5Typography { FontFamily = Sans, LetterSpacing = "-.01em" },
            H6 = new H6Typography { FontFamily = Sans, LetterSpacing = "-.005em" },

            Subtitle1 = new Subtitle1Typography { FontFamily = Sans },
            Subtitle2 = new Subtitle2Typography { FontFamily = Sans },
            Body1 = new Body1Typography { FontFamily = Sans },
            Body2 = new Body2Typography { FontFamily = Sans },
            Button = new ButtonTypography { FontFamily = Sans, LetterSpacing = ".01em", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = Sans },
            Overline = new OverlineTypography { FontFamily = Sans, LetterSpacing = ".12em" },
        },
    };
}
