namespace LegodPause.Service.Proto;

using Microsoft.Extensions.Configuration;

public class ConfigFile
{
    public static IConfigurationRoot? Config => _lazyConfig.Value;

    private static Lazy<IConfigurationRoot> _lazyConfig = new Lazy<IConfigurationRoot>(BuildConfigurationRoot);

    const string FileName = "config.ini";
    static string Section => Path.GetFileNameWithoutExtension(FileName);


    public static T? GetValue<T>(string key)
    {
        if (Config != null) return Config.GetValue<T>($"{Section}:{key}");

        return default;
    }


    private static IConfigurationRoot BuildConfigurationRoot()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddIniFile("config.ini", optional: true, reloadOnChange: true)
            .Build();
    }
}