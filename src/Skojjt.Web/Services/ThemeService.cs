namespace Skojjt.Web.Services;

/// <summary>
/// Service for managing the application theme (dark/light mode).
/// Uses browser local storage to persist user preference.
/// </summary>
public class ThemeService
{
    private bool _isDarkMode;
    
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                OnThemeChanged?.Invoke();
            }
        }
    }

    public event Action? OnThemeChanged;

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }
}
