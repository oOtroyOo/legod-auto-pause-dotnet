using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LegodPause.Core.Network;

public class PipeChannel : IDisposable
{
    private readonly CancellationToken _token;
    private readonly ILogger<PipeChannel>? _logger;

    private Pipe? _send;

    public Pipe Send
    {
        get
        {
            if (_send == null)
            {
                _send = new Pipe();
                Task.Run(KeepSend);
            }

            return _send;
        }
    }

    private Pipe? _receive;

    public Pipe Receive
    {
        get
        {
            if (_receive == null)
            {
                _receive = new Pipe();
                Task.Run(KeepReceive);
            }

            return _receive;
        }
    }

    private readonly ConcurrentBag<Socket> _sockets = new();


    public PipeChannel(CancellationToken token, ILogger<PipeChannel>? logger)
    {
        _token = token;
        _logger = logger;
        _token.Register(Dispose);
    }

    private async Task KeepSend()
    {
        while (!_token.IsCancellationRequested)
        {
            try
            {
                var readResult = await Send.Reader.ReadAtLeastAsync(1, _token);

                if (readResult.Buffer.Length > 0)
                {
                    var buffers = readResult.Buffer.Slice(readResult.Buffer.Start, readResult.Buffer.End);
                    var segment = new ArraySegment<byte>(buffers.ToArray());

                    Send.Reader.AdvanceTo(readResult.Buffer.End);

                    StringBuilder logStr = new StringBuilder();
                    logStr.AppendFormat("Send {0} bytes to:\n", buffers.Length);
                    foreach (var socket in _sockets)
                    {
                        try
                        {
#if NETFRAMEWORK
                                await socket.SendAsync(segment, SocketFlags.None);
#else
                            await socket.SendAsync(segment, _token);
#endif
                            logStr.AppendFormat("{0}\n", socket.RemoteEndPoint);
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError(e, "socket.SendAsync error");
                        }
                    }

                    _logger?.LogInformation("{0}", logStr);
                }

                await Task.Delay(10, _token); // Add small delay to prevent tight loop
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "KeepSend error");
            }
            finally
            {
                Send.Reader.CancelPendingRead();
            }
        }
    }

    private async Task KeepReceive()
    {
#if NETFRAMEWORK
        var buffer = new byte[1024];
#else
        var buffer = new Memory<byte>(new byte[1024]);
#endif
        while (!_token.IsCancellationRequested)
        {
            try
            {
                foreach (var socket in _sockets)
                {
                    var socketArgs = new SocketAsyncEventArgs();
                    TaskCompletionSource<int> completionSource = new TaskCompletionSource<int>();
#if NETFRAMEWORK
                    socketArgs.SetBuffer(buffer, 0, buffer.Length);
#else
                    socketArgs.SetBuffer(buffer);
#endif
                    if (socket.ProtocolType == ProtocolType.Udp)
                    {
                        socketArgs.RemoteEndPoint = new IPEndPoint(((IPEndPoint)socket.LocalEndPoint).Address, 0);
                    }
                    else if (socket.ProtocolType == ProtocolType.Tcp)
                    {
                        if (!socket.Connected)
                        {
                            continue;
                        }
                    }

                    socketArgs.Completed += (sender, e) => { completionSource.TrySetResult(socketArgs.BytesTransferred); };

                    var received = socket.ReceiveFromAsync(socketArgs);
                    if (received)
                    {
                        await completionSource.Task;
                        if (socketArgs.Count > 0)
                        {
                            await Receive.Writer.WriteAsync(new ReadOnlyMemory<byte>(socketArgs.Buffer, 0, socketArgs.BytesTransferred), _token);
                            _logger?.LogInformation("Received {0} bytes from {1}", socketArgs.BytesTransferred, socketArgs.RemoteEndPoint);
                        }
                    }
                }

                await Task.Delay(10, _token); // Add small delay to prevent tight loop
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "KeepReceive error");
            }
            finally
            {
                Receive.Writer.CancelPendingFlush();
            }
        }
    }

    public void AddSocket(Socket socket)
    {
        // if (_sockets.TryRemove(socket.RemoteEndPoint, out var s))
        // {
        //     s.Dispose();
        // }

        _sockets.Add(socket);
    }


    public void Dispose()
    {
        if (_send != null)
        {
            _send.Writer.Complete();
            _send.Reader.Complete();
            _send.Reset();
        }

        if (_receive != null)
        {
            _receive.Writer.Complete();
            _receive.Reader.Complete();
            _receive.Reset();
        }

        lock (_sockets)
        {
            while (_sockets.Count > 0)
            {
                if (_sockets.TryTake(out var socket))
                {
                    try
                    {
                        socket.Dispose();
                    }
                    catch (Exception e)
                    {
                        _logger?.LogError(e, "socket.Dispose error");
                    }
                }
            }
        }
    }
}