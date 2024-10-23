using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Serilog;

namespace Microsoft.Azure.Agent;

internal class AzureCopilotReceiver : IDisposable
{
    private const int BufferSize = 4096;

    private readonly byte[] _buffer;
    private readonly ClientWebSocket _webSocket;
    private readonly MemoryStream _memoryStream;
    private readonly CancellationTokenSource _cancelMessageReceiving;
    private readonly BlockingCollection<CopilotActivity> _activityQueue;

    private AzureCopilotReceiver(ClientWebSocket webSocket)
    {
        _webSocket = webSocket;
        _buffer = new byte[BufferSize];
        _memoryStream = new MemoryStream();
        _cancelMessageReceiving = new CancellationTokenSource();
        _activityQueue = new BlockingCollection<CopilotActivity>();

        Watermark = -1;
    }

    internal int Watermark { get; private set; }

    internal static async Task<AzureCopilotReceiver> CreateAsync(string streamUrl)
    {
        var webSocket = new ClientWebSocket();
        await webSocket.ConnectAsync(new Uri(streamUrl), CancellationToken.None);

        var copilotReader = new AzureCopilotReceiver(webSocket);
        _ = Task.Run(copilotReader.ProcessActivities);

        return copilotReader;
    }

    private async Task ProcessActivities()
    {
        Log.Debug("[AzureCopilotReceiver] Receiver is up and running.");

        while (_webSocket.State is WebSocketState.Open)
        {
            string closingMessage = null;
            WebSocketReceiveResult result = null;

            try
            {
                result = await _webSocket.ReceiveAsync(_buffer, _cancelMessageReceiving.Token);
                if (result.MessageType is WebSocketMessageType.Close)
                {
                    closingMessage = "Close message received";
                    _activityQueue.Add(new CopilotActivity { Error = new ConnectionDroppedException("The server websocket is closing. Connection dropped.") });
                    Log.Information("[AzureCopilotReceiver] Web socket closed by server.");
                }
            }
            catch (OperationCanceledException)
            {
                // Close the web socket before the thread is going away.
                closingMessage = "Client closing";
                Log.Information("[AzureCopilotReceiver] Receiver was cancelled and disposed.");
            }

            if (closingMessage is not null)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, closingMessage, CancellationToken.None);
                _activityQueue.CompleteAdding();
                break;
            }

            // Occasionally, the Direct Line service sends an empty message as a liveness ping.
            // We simply ignore these messages.
            if (result.Count is 0)
            {
                continue;
            }

            _memoryStream.Write(_buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                _memoryStream.Position = 0;
                var rawResponse =  JsonSerializer.Deserialize<RawResponse>(_memoryStream, Utils.JsonOptions);
                _memoryStream.SetLength(0);

                if (rawResponse.Watermark is not null)
                {
                    Watermark = int.Parse(rawResponse.Watermark);
                }

                foreach (CopilotActivity activity in rawResponse.Activities)
                {
                    if (activity.IsFromCopilot)
                    {
                        _activityQueue.Add(activity);
                    }
                }
            }
        }

        Log.Error("[AzureCopilotReceiver] Web socket connection dropped. State: '{0}'", _webSocket.State);
        _activityQueue.Add(new CopilotActivity { Error = new ConnectionDroppedException($"The websocket got in '{_webSocket.State}' state. Connection dropped.") });
        _activityQueue.CompleteAdding();
    }

    internal CopilotActivity Take(CancellationToken cancellationToken)
    {
        CopilotActivity activity = _activityQueue.Take(cancellationToken);
        if (activity.Error is not null)
        {
            ExceptionDispatchInfo.Capture(activity.Error).Throw();
        }

        return activity;
    }

    public void Dispose()
    {
        _webSocket.Dispose();
        _cancelMessageReceiving.Cancel();
    }
}
