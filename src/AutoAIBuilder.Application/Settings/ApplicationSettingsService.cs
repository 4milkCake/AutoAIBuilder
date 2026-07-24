namespace AutoAIBuilder.Application.Settings;

public sealed class ApplicationSettingsService(IApplicationSettingsRepository repository)
{
    public ApplicationSettings Load()
    {
        try
        {
            return repository.Load().ValidateAndNormalize();
        }
        catch (ArgumentException)
        {
            return ApplicationSettings.CreateDefault();
        }
        catch (System.Text.Json.JsonException)
        {
            return ApplicationSettings.CreateDefault();
        }
    }

    public ApplicationSettings Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.ValidateAndNormalize();
        repository.Save(normalized);
        return normalized;
    }

    public ApplicationSettings RestoreDefaults() =>
        Save(ApplicationSettings.CreateDefault());
}
