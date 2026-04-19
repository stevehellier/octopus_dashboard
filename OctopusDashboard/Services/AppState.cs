namespace OctopusDashboard.Services;

public class AppState
{
    private bool _showCosts = false;
    public bool ShowCosts
    {
        get => _showCosts;
        set { _showCosts = value; OnChange?.Invoke(); }
    }
    public event Action? OnChange;
}
