namespace LeigodPause.Service.Proto;

using IniParser;
using IniParser.Model;
using System;
using System.Globalization;
using System.IO;

public class ConfigFile(ILogger<ConfigFile> logger)
{
    const string FileName = "config.ini";
    string Section => Path.GetFileNameWithoutExtension(FileName);
    private string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    private readonly FileIniDataParser _parser = new FileIniDataParser();
    private IniData? _data;
    public event Action? OnSave;

    private DateTime _lastModifyDate = default;

    // 保护 _data 的并发读写（Worker / IPC / UI 多线程）。
    private readonly object _dataLock = new();

    private IniData Load()
    {
        try
        {
            _parser.Parser.Configuration.CommentString = "#";
            var fileInfo = new FileInfo(FilePath);
            logger.Log(LogLevel.Information, "Config file path: {FilePath} Exsist{fileInfo.Exists}", FilePath, fileInfo.Exists);
            if (fileInfo.Exists)
            {
                _lastModifyDate = fileInfo.LastWriteTime;
                return _parser.ReadFile(FilePath, Encoding.UTF8);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "配置文件读取失败");
        }

        return new IniData();
    }

    private void EnsureSection()
    {
        lock (_dataLock)
        {
            _data ??= Load();
            var fileInfo = new FileInfo(FilePath);

            // 先取最新修改时间，判断是否需要热重载（Reload 同样走 _dataLock，可重入）
            if (fileInfo.Exists && fileInfo.LastWriteTime > _lastModifyDate)
            {
                Reload();
            }


            if (!_data.Sections.ContainsSection(Section))
                _data.Sections.AddSection(Section);
        }
    }

    public T? GetValue<T>(string key)
    {
        lock (_dataLock)
        {
            EnsureSection();

            if (_data == null) return default;

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

    public void SetValue<T>(string key, T value)
    {
        EnsureSection();
        if (_data == null) return;
        lock (_dataLock)
        {
            _data[Section][key] = value?.ToString();
            _parser.WriteFile(FilePath, _data, Encoding.UTF8);
            _lastModifyDate = DateTime.Now;
        }

        // 事件在锁外触发，避免回调长时间阻塞配置读写
        OnSave?.Invoke();
    }

    public void Reload()
    {
        lock (_dataLock)
        {
            _data = Load();
        }
    }
}