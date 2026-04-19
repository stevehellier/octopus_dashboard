namespace OctopusDashboard.Services;

public class AppState
{
    public bool ShowCosts { get; private set; } = false;
    public event Action? OnChange;

    public void ToggleShowCosts()
    {
        ShowCosts = !ShowCosts;
        OnChange?.Invoke();
    }
}
