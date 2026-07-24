namespace AutoAIBuilder.Application.Settings;

public interface IApplicationSettingsRepository
{
    ApplicationSettings Load();

    void Save(ApplicationSettings settings);
}
