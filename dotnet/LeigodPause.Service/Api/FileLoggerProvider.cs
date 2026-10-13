using System.Text;

namespace LeigodPause.Service.Api;

/// <summary>
/// 简单的文件日志 Provider：把日志按行追加写入指定文件（UTF-8、AutoFlush）。
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    public FileLoggerProvider(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            foreach (var s in Directory.GetFiles(dir, "*.txt"))
            {
                if (DateTime.Now - new FileInfo(s).CreationTime > TimeSpan.FromDays(5))
                {
                    try
                    {
                        File.Delete(s);
                    }
                    catch
                    {
                        //
                    }
                }
            }
        }

        _writer = new StreamWriter(
            new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_writer, _gate, categoryName);

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly StreamWriter _writer;
        private readonly object _gate;
        private readonly string _category;

        public FileLogger(StreamWriter writer, object gate, string category)
        {
            _writer = writer;
            _gate = gate;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) where TState : notnull
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [").Append(logLevel).Append("] ");
            sb.Append(_category).Append(": ");
            sb.Append(formatter(state, exception));
            if (exception != null)
            {
                sb.AppendLine().Append(exception);
            }

            lock (_gate)
            {
                _writer.WriteLine(sb.ToString());
            }
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}