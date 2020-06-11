using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Catan.Proxy;
using CatanService.State;
using Microsoft.AspNetCore.Http;

namespace CatanService
{
    public class WebSocketData
    {
        private ConcurrentQueue<byte[]> MessageQueue { get; } = new ConcurrentQueue<byte[]>();
        public HttpContext HttpContext { get; set; }
        public WebSocket WebSocket { get; set; }
        public TaskCompletionSource<object> Tcs { get; private set; } = new TaskCompletionSource<object>();

        public WebSocketData(ConcurrentQueue<(Guid, byte[])> messages)
        {
            foreach (var (_, message) in messages)
            {

                MessageQueue.Enqueue(message);
            }
        }

        public void PostMessage(byte[] msg)
        {

            MessageQueue.Enqueue(msg); // note this is not a copy, but it is readonly
            if (Tcs != null && Tcs.Task.IsCompleted == false)
            {
                Tcs.SetResult(null);
            }
        }
        public async Task ProcessMessages()
        {

            while (true)
            {
                Contract.Assert(Tcs != null);
                Contract.Assert(Tcs.Task.IsCompleted == false);

                if (MessageQueue.Count == 0)
                {
                    await Tcs.Task;
                    if (Tcs.Task.IsCanceled) break;
                }
                while (MessageQueue.TryDequeue(out byte[] byteMessage))
                {
                    Contract.Assert(byteMessage != null);
                    Contract.Assert(WebSocket != null);
                    await WebSocket.SendAsync(byteMessage, WebSocketMessageType.Text, true, CancellationToken.None);
                }

                Tcs = new TaskCompletionSource<object>();

                //  there is a race condition where messages are posted faster than we can send them to the client
                //  and then they stop posting -- so we end up with message in the MessageQueu but we don't have
                //  anything to release them.  this is very unlikely t happen in our scenario as games don't get
                //  created all that often.



                //
                //  wait for the client to ack -- this needs a timeout in case the client dies
                //  We expect the client to get the message and immediately ack back -- if you are dedugging on the client, 
                //  don't break between recieving the message and sending the ack...
                //
                var buffer = new byte[1024 * 4];
                Task<WebSocketReceiveResult> task = WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                try
                {
                    var result = await task.TimeoutAfter<WebSocketReceiveResult>(TimeSpan.FromSeconds(60));
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    CatanMessage message = CatanProxy.Deserialize<CatanMessage>(json); // need the CatanProxies JsonOptions
                    //
                    //  "break" terminates the 
                    if (message == null) break;
                    if (message.MessageType != MessageType.Ack) break;
                    if (result.CloseStatus != null && result.CloseStatus.HasValue == false) break;
                }
                catch (TimeoutException)
                {
                    break;
                }

            }
            //
            //  return will close the socket
        }



    }
    public class CatanWebSocketService
    {
        private List<WebSocketData> WsCallbacks { get; } = new List<WebSocketData>();
        private ConcurrentQueue<(Guid, byte[])> HistoricalMessages { get; set; } = new ConcurrentQueue<(Guid, byte[])>();
        

        public async Task RegisterWebSocket(HttpContext context, WebSocket webSocket)
        {
            var wsData = new WebSocketData(HistoricalMessages) { HttpContext = context, WebSocket = webSocket };

            WsCallbacks.Add(wsData);
            await wsData.ProcessMessages();
            WsCallbacks.Remove(wsData);


        }
     
    }
}
