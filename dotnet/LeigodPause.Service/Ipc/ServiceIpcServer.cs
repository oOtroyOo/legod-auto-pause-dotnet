using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LeigodPause.Service.Ipc;

public sealed class ServiceIpcServer(LeigodCoreModel leigodCoreModel, ILogger<ServiceIpcServer>? logger, ProtobufMsgEncoder protobufMsgEncoder)
{
    private readonly CancellationTokenSource _cts = new();

    public void Start()
    {
        logger?.LogInformation("IPC pipe server starting on {PipeName}", IpcPipeNames.CommandPipe);
        _ = ListenLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        logger?.LogInformation("IPC pipe server stopping");
        _cts.Cancel();
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SecurityIdentifier securityIdentifier = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            PipeSecurity pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(securityIdentifier, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
            NamedPipeServerStream pipe = new NamedPipeServerStream(IpcPipeNames.CommandPipe, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous, 0, 0, pipeSecurity);

            try
            {
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (Exception e)
            {
                pipe.Dispose();
                logger?.LogError(e, "Named pipe wait-connection failed");
                continue;
            }

            logger?.LogInformation("IPC client connected on {PipeName}", IpcPipeNames.CommandPipe);
            _ = HandleClientAsync(pipe, token);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        try
        {
            var command = await protobufMsgEncoder.Decode<IpcEnvelope>(pipe, token).ConfigureAwait(false);
            logger?.LogInformation("IPC command received: {Command}", command.Command);

            var result = await DispatchAsync(command).ConfigureAwait(false);
            await protobufMsgEncoder.Encode(pipe, result, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Named pipe command handling failed");
        }
        finally
        {
            pipe.Dispose();
            logger?.LogDebug("IPC client disconnected");
        }
    }

    private async Task<IpcEnvelope> DispatchAsync(IpcEnvelope cmd)
    {
        var (code, msg, data) = cmd.Command switch
        {
            ServiceCommand.Ping => (0, null, (byte[]?)null),
            ServiceCommand.RefreshUserInfo => await DoRefreshAsync(),
            ServiceCommand.StartPause => await DoPauseAsync(),
            ServiceCommand.StopPause => (-2, "Not implemented: resume pause", (byte[]?)null),
            ServiceCommand.ReloadConfig => await DoReloadConfig(),
            ServiceCommand.Exit => (0, (string?)null, (byte[]?)null),
            _ => (-2, "Unknown command", null),
        };

        return new IpcEnvelope { Command = cmd.Command, Code = code, Msg = msg, Payload = data };
    }

    private async Task<(int Code, string? Msg, byte[]? Data)> DoReloadConfig()
    {
        ConfigFile.Reload();
        return (0, null, null);
    }

    public IpcEnvelope? LastUserInfoError { get; set; }

    private async Task<(int Code, string? Msg, byte[]? Data)> DoRefreshAsync()
    {
        int? errCode = null;
        string? errMsg = null;

        if (LastUserInfoError == null && leigodCoreModel.UserInfoJson == null || leigodCoreModel.RequestInfoTimeout)
        {
            await leigodCoreModel.RequestUserInfo((c, m) =>
            {
                errCode = c;
                errMsg = m;
                LastUserInfoError = new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo, Code = c, Msg = m };
            });
        }
        else if (LastUserInfoError != null)
        {
            errMsg = LastUserInfoError.Msg;
            errCode = LastUserInfoError.Code;
        }

        if (errCode is { } code && code != 0)
        {
            return (code, errMsg, null);
        }

        var json = JsonSerializer.Serialize(leigodCoreModel.UserInfoJson, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        });
        return (0, null, Encoding.UTF8.GetBytes(json));
    }

    private async Task<(int Code, string? Msg, byte[]? Data)> DoPauseAsync()
    {
        int? errCode = null;
        string? errMsg = null;
        await leigodCoreModel.SendPauseState((c, m) =>
        {
            errCode = c;
            errMsg = m;
        });

        if (errCode is { } code && code != 0)
        {
            return (code, errMsg, null);
        }

        return (0, null, null);
    }

    // Apply a permissive DACL (Everyone full control) and a low mandatory-integrity SACL
    // to the pipe handle. This lets a medium-integrity user-mode client (the WPF app)
    // connect even when the service runs at System integrity; otherwise ConnectAsync
    // throws UnauthorizedAccessException ("Access to the path is denied").
    private static void ApplyPipeSecurity(SafePipeHandle handle)
    {
        // D:(A;;FA;;;WD)  -> DACL: Everyone full access
        // S:(ML;;NW;;;LW) -> SACL: low integrity label (No-Write-Up)
        const string sddl = "D:(A;;FA;;;WD)S:(ML;;NW;;;LW)";

        if (!NativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptor(
                sddl, 1, out var sd, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            if (!NativeMethods.GetSecurityDescriptorDacl(sd, out _, out var pDacl, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!NativeMethods.GetSecurityDescriptorSacl(sd, out _, out var pSacl, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var result = NativeMethods.SetSecurityInfo(
                handle.DangerousGetHandle(),
                NativeMethods.SeObjectKind.SeKernelObject,
                NativeMethods.SecurityInformation.Dacl | NativeMethods.SecurityInformation.Sacl,
                IntPtr.Zero,
                IntPtr.Zero,
                pDacl,
                pSacl);

            if (result != 0)
            {
                throw new Win32Exception(result);
            }
        }
        finally
        {
            NativeMethods.LocalFree(sd);
        }
    }

    private static class NativeMethods
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string stringSecurityDescriptor,
            int stringSdRevision,
            out IntPtr securityDescriptor,
            IntPtr securityDescriptorSize);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSecurityDescriptorDacl(
            IntPtr securityDescriptor,
            [MarshalAs(UnmanagedType.Bool)] out bool daclPresent,
            out IntPtr dacl,
            [MarshalAs(UnmanagedType.Bool)] out bool daclDefaulted);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSecurityDescriptorSacl(
            IntPtr securityDescriptor,
            [MarshalAs(UnmanagedType.Bool)] out bool saclPresent,
            out IntPtr sacl,
            [MarshalAs(UnmanagedType.Bool)] out bool saclDefaulted);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int SetSecurityInfo(
            IntPtr handle,
            SeObjectKind objectType,
            SecurityInformation securityInfo,
            IntPtr owner,
            IntPtr group,
            IntPtr dacl,
            IntPtr sacl);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr LocalFree(IntPtr hMem);

        internal enum SeObjectKind
        {
            SeKernelObject = 6,
        }

        [Flags]
        internal enum SecurityInformation : uint
        {
            Dacl = 0x0004,
            Sacl = 0x0008,
        }
    }
}