namespace WindowSwitch.Services;

public interface IWindowSettingsStore
{
    WindowSettings? Load();

    void Save(WindowSettings settings);
}
