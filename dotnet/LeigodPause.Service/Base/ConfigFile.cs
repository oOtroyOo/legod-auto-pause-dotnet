namespace LeigodPause.Service.Proto;

using IniParser;
using IniParser.Model;
using System;
using System.Globalization;
using System.IO;

public class ConfigFile
{
    const string FileName = "config.ini";
    static string Section => Path.GetFileNameWithoutExtension(FileName);
    private static string FilePath => Path.Combine(Environment.CurrentDirectory, FileName); //Path.Combine(AppContext.BaseDirectory, FileName);

    private static readonly FileIniDataParser _parser = new FileIniDataParser();
    private static IniData _data = Load();
    public static event Action OnSave;

    private static DateTime lastModifyDate;

    private static IniData Load()
    {
        _parser.Parser.Configuration.CommentString = "#";
        var fileInfo = new FileInfo(FilePath);
        if (fileInfo.Exists)
        {
            lastModifyDate = fileInfo.LastWriteTime;
            return _parser.ReadFile(FilePath, Encoding.UTF8);
        }

        return new IniData();
    }

    private static void EnsureSection()
    {
        if (!_data.Sections.ContainsSection(Section))
            _data.Sections.AddSection(Section);
    }

    public static T? GetValue<T>(string key)
    {
        var fileInfo = new FileInfo(FilePath);
        if (fileInfo.Exists && fileInfo.LastWriteTime > lastModifyDate)
        {
            Reload();
        }

        var section = _data.Sections[Section];
        var raw = section?[key];
        if (string.IsNullOrEmpty(raw)) return default;

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            object? converted = targetType.IsEnum
                ? Enum.Parse(targetType, raw, ignoreCase: true)
                : Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
            return (T)converted;
        }
        catch
        {
            return default;
        }
    }

    public static void SetValue<T>(string key, T value)
    {
        EnsureSection();
        _data[Section][key] = value?.ToString();
        _parser.WriteFile(FilePath, _data, Encoding.UTF8);
        OnSave?.Invoke();
        lastModifyDate = DateTime.Now;
    }

    public static void Reload()
    {
        _data = Load();
    }
}