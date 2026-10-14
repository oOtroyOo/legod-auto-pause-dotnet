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
    public static event Action? OnSave;

    private static DateTime _lastModifyDate = default;

    // 保护 _data 的并发读写（Worker / IPC / UI 多线程）。
    private static readonly object _dataLock = new();

    private static IniData Load()
    {
        _parser.Parser.Configuration.CommentString = "#";
        var fileInfo = new FileInfo(FilePath);
        if (fileInfo.Exists)
        {
            _lastModifyDate = fileInfo.LastWriteTime;
            return _parser.ReadFile(FilePath, Encoding.UTF8);
        }

        return new IniData();
    }

    private static void EnsureSection()
    {
        lock (_dataLock)
        {
            if (!_data.Sections.ContainsSection(Section))
                _data.Sections.AddSection(Section);
        }
    }

    public static T? GetValue<T>(string key)
    {
        var fileInfo = new FileInfo(FilePath);
        lock (_dataLock)
        {
            // 先取最新修改时间，判断是否需要热重载（Reload 同样走 _dataLock，可重入）
            if (fileInfo.Exists && fileInfo.LastWriteTime > _lastModifyDate)
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
    }

    public static void SetValue<T>(string key, T value)
    {
        EnsureSection();
        lock (_dataLock)
        {
            _data[Section][key] = value?.ToString();
            _parser.WriteFile(FilePath, _data, Encoding.UTF8);
            _lastModifyDate = DateTime.Now;
        }

        // 事件在锁外触发，避免回调长时间阻塞配置读写
        OnSave?.Invoke();
    }

    public static void Reload()
    {
        lock (_dataLock)
        {
            _data = Load();
        }
    }
}
