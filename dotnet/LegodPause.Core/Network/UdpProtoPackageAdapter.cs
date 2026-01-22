using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using LegodPause.Core.Proto;
using TouchSocket.Core;

namespace LegodPause.Core.Network;

public class UdpProtoPackageAdapter : UdpDataHandlingAdapter
{
    class PipeToPack : IDisposable
    {
        public Pipe Pipe = new();
        public ConcurrentStack<TaskCompletionSource<ProtoLib>> Stack = new();
        private readonly Task reading;
        private readonly CancellationToken cancellationToken;
        ProtoPackageAdapter adapter;

        public PipeToPack(ProtoPackageAdapter adapter, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.adapter = adapter;
            reading = Task.Run(this.ReadAsync, cancellationToken);
        }

        async Task ReadAsync()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var readResult = await Pipe.Reader.ReadAsync(cancellationToken);
                    var reader = new BytesReader(readResult.Buffer);
                    if (this.adapter.TryParseRequest(ref reader, out var lib))
                    {
                        if (Stack.TryPop(out var source))
                        {
                            source.SetResult(lib);
                        }

                        await Pipe.Reader.CompleteAsync();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public async Task Write(ReadOnlyMemory<byte> memory)
        {
            await Pipe.Writer.WriteAsync(memory, cancellationToken);
        }

        public Task<ProtoLib> WaitResult()
        {
            var source = new TaskCompletionSource<ProtoLib>();
            Stack.Push(source);
            return source.Task;
        }

        public void Dispose()
        {
            reading.Dispose();
            adapter.Dispose();
            Stack.Clear();
        }
    }

    private readonly ProtoPackageAdapter adapter;

    public override bool CanSendRequestInfo => true;

    private ConcurrentDictionary<EndPoint, PipeToPack> cachePipe = new();
    private CancellationTokenSource TokenSource = new();

    public UdpProtoPackageAdapter(ProtoPackageAdapter adapter)
    {
        this.adapter = adapter;
        this.ReceivedCallBack += HandleReceivedCallBack;
        this.SendCallBackAsync += HandleSendCallBack;
    }

    protected override async Task PreviewReceivedAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> memory)
    {
        // return base.PreviewReceivedAsync(remoteEndPoint, memory);

        var pipeToPack = cachePipe.GetOrAdd(remoteEndPoint, e => new(adapter, TokenSource.Token));
        await pipeToPack.Write(memory);

        var lib = await pipeToPack.WaitResult();
        await GoReceived(remoteEndPoint, memory, lib);
    }

    protected override Task PreviewSendAsync(EndPoint endPoint, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken)
    {
        return base.PreviewSendAsync(endPoint, memory, cancellationToken);
    }

    protected override Task PreviewSendAsync(EndPoint endPoint, IRequestInfo requestInfo, CancellationToken cancellationToken)
    {
        return base.PreviewSendAsync(endPoint, requestInfo, cancellationToken);
    }

    private async Task HandleSendCallBack(EndPoint endPoint, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken)
    {
    }


    private async Task HandleReceivedCallBack(EndPoint remoteEndPoint, ReadOnlyMemory<byte> memory, IRequestInfo requestInfo)
    {
    }

    protected override void SafetyDispose(bool disposing)
    {
        TokenSource.Cancel();
        foreach (var kv in cachePipe)
        {
            kv.Value.Dispose();
        }

        cachePipe.Clear();
        base.SafetyDispose(disposing);
    }
}