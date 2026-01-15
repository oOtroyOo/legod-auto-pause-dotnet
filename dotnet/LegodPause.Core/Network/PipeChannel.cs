using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LegodPause.Core.Network;

public class PipeChannel : IDisposable
{
    private readonly CancellationToken _token;
    private readonly ILogger<PipeChannel>? _logger;
    

    private Pipe? _send;

    public Pipe SendPipe
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

    public Pipe ReceivePipe
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

    private readonly ConcurrentBag<IPipSendHandle> _sendHandles = new();
    private readonly ConcurrentBag<IPipeReceiveHandle> _receiveHandles = new();


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
                var readResult = await SendPipe.Reader.ReadAtLeastAsync(1, _token);

                if (readResult.Buffer.Length > 0)
                {
                    var buffers = readResult.Buffer.Slice(readResult.Buffer.Start, readResult.Buffer.End);
                    var segment = new ArraySegment<byte>(buffers.ToArray());

                    SendPipe.Reader.AdvanceTo(readResult.Buffer.End);

                    StringBuilder logStr = new StringBuilder();
                    logStr.AppendFormat("SendPipe {0} bytes to:\n", buffers.Length);
                    foreach (var handle in _sendHandles)
                    {
                        try
                        {
                            await handle.SendBytesHandle(segment, _token);
                            // logStr.AppendFormat("{0}\n", socket.RemoteEndPoint);
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
                SendPipe.Reader.CancelPendingRead();
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
                foreach (var handle in _receiveHandles)
                {
                    IPEndPoint remoteEndPoint = default;
                    int read = await handle.ReceiveBytesHandle(buffer, _token);

                    if (read > 0)
                    {
                        await ReceivePipe.Writer.WriteAsync(new ReadOnlyMemory<byte>(
#if NETFRAMEWORK
                            buffer
#else
                            buffer.ToArray()
#endif
                            , 0, read), _token);
                        _logger?.LogInformation("Received {0} bytes from {1}", read, remoteEndPoint);
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
                ReceivePipe.Writer.CancelPendingFlush();
            }
        }
    }

    public void AddReceive(IPipeReceiveHandle handle)
    {
        _receiveHandles.Add(handle);
    }

    public void AddSend(IPipSendHandle handle)
    {
        _sendHandles.Add(handle);
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
    }
}