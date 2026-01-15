using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Logging;
using LegodPause.Avalonia.ViewModels;
using LegodPause.Core.Network;
using LegodPause.Core.Proto;
using Logger = Avalonia.Logging.Logger;

namespace LegodPause.Avalonia.Views;

public partial class MainView : UserControl
{
    UdpClient? udpClient;
    PipeChannel? channel;
    private MainViewModel Model => (MainViewModel)DataContext;

    public MainView()
    {
        InitializeComponent();
        Task.Run(Run);
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
    }

    private void Control_OnUnLoaded(object? sender, RoutedEventArgs e)
    {
        channel?.Dispose();
        udpClient?.Dispose();
    }

    private async Task Run()
    {
        // var endpoint = new IPEndPoint(IPAddress.Any, NetworkServer.UdpPort);
        // udpClient = new UdpClient(endpoint);
        // UdpClientPipHandle udpClientPipHandle = new(udpClient);
        // channel = new PipeChannel(CancellationToken.None, null);
        // channel.AddReceive(udpClientPipHandle);
        // var protobufMsgEncoder = new ProtobufMsgEncoder(null);
        // try
        // {
        //     while (true)
        //     {
        //         var receiveResult = await protobufMsgEncoder.Decode<ProtoLib>(channel.ReceivePipe.Reader);
        //         if (receiveResult.Ping != null)
        //         {
        //             var pingTime = DateTimeOffset.FromUnixTimeMilliseconds(receiveResult.Ping.pingTime);
        //             Model.Greeting = pingTime.ToString("yyyy-MM-dd HH:mm:ss");
        //             Logger.Sink.Log(LogEventLevel.Information, Model.Greeting, null, null);
        //         }
        //     }
        // }
        // catch (Exception e)
        // {
        //     Logger.Sink.Log(LogEventLevel.Error, e.Message, e, null);
        //     throw;
        // }
    }
}