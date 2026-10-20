using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace LeigodPause.Service;

public class Worker(ILogger<Worker> logger, ServiceIpcServer ipcServer, LeigodCoreModel leigodCoreModel, ConfigFile configFile) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("StopAsync");
        // 注销事件，避免内存泄漏
        SystemEvents.SessionEnding -= OnSessionEnding;
        await ipcServer.Stop();
        try
        {
            if (Program.AppHost != null)
            {
                var hostLifetime = Program.AppHost.Services.GetRequiredService<IHostLifetime>();
                if (hostLifetime is CustomServiceLifetime customLifetime)
                {
                    customLifetime.OnShutdownEvents -= OnHostLifetimeShutdown;
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "获取不到Lifetime实例");
        }

        await OnEndingActionAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ExecuteAsync");
        logger.LogInformation("games: {games}", configFile.GetValue<string>("games"));
        ipcServer.Start();
        SystemEvents.SessionEnding += OnSessionEnding;
        try
        {
            if (Program.AppHost != null)
            {
                var hostLifetime = Program.AppHost.Services.GetRequiredService<IHostLifetime>();
                logger.LogInformation("IHostLifetime is " + hostLifetime.GetType().FullName);
                if (hostLifetime is CustomServiceLifetime customLifetime)
                {
                    customLifetime.OnShutdownEvents += OnHostLifetimeShutdown;
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "获取不到Lifetime实例");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time:yyyyMMdd HH:mm:ss}", DateTimeOffset.Now);
            }


            try
            {
                await UpdateTimer();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Worker error");
            }

            float updateTime = configFile.GetValue<float>("update");
            if (updateTime < 1) updateTime = 1;

            await Task.Delay((int)(updateTime * 1000), stoppingToken);
        }

        logger.LogInformation("ExecuteAsync 的token已取消");
        await OnEndingActionAsync();
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        logger.LogInformation("OnSessionEnding");
        _ = OnEndingActionAsync();
    }

    private void OnHostLifetimeShutdown()
    {
        logger.LogInformation("OnHostLifetimeShutdown");
        _ = OnEndingActionAsync();
    }

/*
2026-10-14 14:23:07.173 [Information] CustomServiceLifetime: 触发系统关机事件，执行OnShutdown自定义逻辑
2026-10-14 14:23:07.173 [Information] LeigodPause.Service.Worker: OnHostLifetimeShutdown
2026-10-14 14:23:07.173 [Information] LeigodPause.Service.Worker: OnEndingAction
2026-10-14 14:23:07.173 [Information] LeigodPause.Service.Worker: SendPauseState
2026-10-14 14:23:07.174 [Information] LeigodPause.Service.Worker: SendPauseState Finish
2026-10-14 14:23:07.175 [Information] Microsoft.Hosting.Lifetime: Application is shutting down...
2026-10-14 14:23:07.185 [Information] LeigodPause.Service.Worker: StopAsync
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Ipc.ServiceIpcServer: IPC pipe server stopping
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Worker: OnEndingAction
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Worker: SendPauseState
2026-10-14 14:23:07.186 [Information] LeigodPause.Service.Worker: SendPauseState Finish
*/
    /// <summary>
    /// 关机/登出时执行暂停。非阻塞、带超时，避免 .Wait() 阻塞系统关机流程。
    /// </summary>
    private async Task OnEndingActionAsync()
    {
        logger.LogInformation("OnEndingAction");
        try
        {
            var pauseTask = SendPauseState();
            await Task.WhenAny(pauseTask, Task.Delay(3000));
        }
        catch (Exception e)
        {
            logger.LogError(e, "OnEndingAction SendPauseState 失败");
        }
    }

    private async Task UpdateTimer()
    {
        if (leigodCoreModel.RequestInfoTimeout)
        {
            await RequestUserInfo();
        }

        if (leigodCoreModel.IsNotPaused)
        {
            var matched = await leigodCoreModel.FindGameProcess();

            // 服务端主动下发检测结果给客户端（无回执，允许数据丢弃）
            await ipcServer.PushFindGameProcessAsync(matched);

            if (string.IsNullOrEmpty(matched) && leigodCoreModel.MatchedGameProcessExitTime != default)
            {
                var loopTime = configFile.GetValue<float>("looptime");
                if (loopTime < 1) loopTime = 1;
                // 以游戏退出时间（MatchedGameProcessExitTime）为起点计时，给 looptime 秒宽限
                if (DateTimeOffset.Now - leigodCoreModel.MatchedGameProcessExitTime > TimeSpan.FromSeconds(loopTime))
                {
                    await SendPauseState();
                    await RequestUserInfo();
                }
            }
        }
    }

    private async Task RequestUserInfo()
    {
        var token = configFile.GetValue<string>("account_token");
        if (token is { Length: > 0 })
        {
            await leigodCoreModel.RequestUserInfo((c, m) =>
            {
                // leigodCoreModel.Token = null;

                ipcServer.LastUserInfoError = new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo, Code = c, Msg = m };
                if (c > 0)
                {
                    if (!ipcServer.HasClient)
                        StartMainApp();
                    else
                    {
                        _ = ipcServer.PushToClient(ipcServer.LastUserInfoError);
                    }
                }

                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }

    private async Task SendPauseState()
    {
        logger.LogInformation("SendPauseState");
        var token = configFile.GetValue<string>("account_token");
        if (token is { Length: > 0 })
        {
            await leigodCoreModel.SendPauseState((c, m) =>
            {
                // leigodCoreModel.Token = null;
                ipcServer.LastPauseStateError = new IpcEnvelope() { Command = ServiceCommand.PauseState, Code = c, Msg = m };
                if (c > 0)
                {
                    if (!ipcServer.HasClient)
                        StartMainApp();
                    else
                    {
                        _ = ipcServer.PushToClient(ipcServer.LastPauseStateError);
                    }
                }

                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }

        logger.LogInformation("SendPauseState Finish");
    }

    public void StartMainApp()
    {
        var appName = Path.GetFileName(Process.GetCurrentProcess().MainModule!.FileName).Replace(".Service", "");
        logger.LogInformation("启动主应用 {appName} ", appName);
        try
        {
            if (Process.GetProcessesByName(appName).Length > 0) return;

            var appPath = Path.Combine(AppContext.BaseDirectory, appName);

            // Windows 服务运行在 Session 0，直接 Process.Start 会把 GUI 启动在无交互桌面的
            // Session 0，用户看不到窗口。需拿到活动用户会话令牌，在用户桌面(winsta0\default)启动。
            if (!TryLaunchInUserSession(appPath))
            {
                // 兜底：当前进程本身就在用户会话（如调试时以当前用户运行）时仍可正常显示
                Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "不能启动主应用 {appName}", appName);
        }
    }

    /// <summary>
    /// 在活动用户会话（winsta0\default 桌面）启动目标程序，使 GUI 对用户可见。
    /// 失败返回 false（无活动会话 / 令牌获取失败），交由调用方兜底。
    /// </summary>
    private static bool TryLaunchInUserSession(string appPath)
    {
        IntPtr sessionInfo = IntPtr.Zero;
        IntPtr userToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        try
        {
            if (!NativeMethods.WTSEnumerateSessions(IntPtr.Zero, 0, 1, out sessionInfo, out int count))
            {
                return false;
            }

            int sessionSize = Marshal.SizeOf<NativeMethods.WTS_SESSION_INFO>();
            IntPtr offset = sessionInfo;
            uint? activeSessionId = null;
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<NativeMethods.WTS_SESSION_INFO>(offset);
                if (info.State == NativeMethods.WTS_CONNECTSTATE_CLASS.Active)
                {
                    activeSessionId = (uint)info.SessionID;
                    break;
                }

                offset = IntPtr.Add(offset, sessionSize);
            }

            if (activeSessionId == null)
            {
                return false;
            }

            if (!NativeMethods.WTSQueryUserToken(activeSessionId.Value, out userToken))
            {
                return false;
            }

            const uint tokenPrivs = NativeMethods.TOKEN_QUERY
                                    | NativeMethods.TOKEN_DUPLICATE
                                    | NativeMethods.TOKEN_ASSIGN_PRIMARY
                                    | NativeMethods.TOKEN_ADJUST_DEFAULT
                                    | NativeMethods.TOKEN_ADJUST_SESSIONID;

            if (!NativeMethods.DuplicateTokenEx(
                    userToken, tokenPrivs, IntPtr.Zero,
                    NativeMethods.SECURITY_IMPLEMENTATION.SecurityImpersonation,
                    NativeMethods.TOKEN_TYPE.TokenPrimary, out primaryToken))
            {
                return false;
            }

            var si = new NativeMethods.STARTUPINFO
            {
                cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>(),
                lpDesktop = @"winsta0\default",
            };

            if (NativeMethods.CreateProcessAsUser(
                    primaryToken, appPath, null,
                    IntPtr.Zero, IntPtr.Zero, false,
                    NativeMethods.NORMAL_PRIORITY_CLASS, IntPtr.Zero,
                    Path.GetDirectoryName(appPath), ref si, out _))
            {
                return true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (sessionInfo != IntPtr.Zero)
            {
                NativeMethods.WTSFreeMemory(sessionInfo);
            }

            if (userToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(userToken);
            }

            if (primaryToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(primaryToken);
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSEnumerateSessions(IntPtr hServer, int reserved, int version, out IntPtr ppSessionInfo, out int pCount);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern void WTSFreeMemory(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string? lpApplicationName,
            string? lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            SECURITY_IMPLEMENTATION impersonationLevel,
            TOKEN_TYPE tokenType,
            out IntPtr phNewToken);

        public const uint TOKEN_QUERY = 0x0008;
        public const uint TOKEN_DUPLICATE = 0x0002;
        public const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        public const uint TOKEN_ADJUST_DEFAULT = 0x0080;
        public const uint TOKEN_ADJUST_SESSIONID = 0x0100;
        public const uint NORMAL_PRIORITY_CLASS = 0x0020;

        public enum SECURITY_IMPLEMENTATION : int
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        }

        public enum TOKEN_TYPE : int
        {
            TokenPrimary = 1,
            TokenImpersonation = 2,
        }

        public enum WTS_CONNECTSTATE_CLASS
        {
            Active,
            Connected,
            ConnectQuery,
            Shadow,
            Disconnected,
            Idle,
            Listen,
            Reset,
            Down,
            Init,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFO
        {
            public int SessionID;
            public IntPtr pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }
    }
}