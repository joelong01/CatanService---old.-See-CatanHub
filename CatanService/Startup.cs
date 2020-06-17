using System;
using System.Diagnostics.Contracts;
using System.Net.Mime;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Catan.Proxy;
using CatanService.Controllers;
using CatanService.State;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;

namespace CatanService
{

    public class Startup
    {
        static bool addSignalR = true;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
           {
               builder.AddConsole()
               .AddDebug()
               .AddFilter<ConsoleLoggerProvider>(category: null, level: LogLevel.Debug)
               .AddFilter<DebugLoggerProvider>(category: null, level: LogLevel.Debug);
           });

            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

            });
            if (addSignalR)
            services.AddSignalR().AddAzureSignalR();

        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseFileServer();

            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 16 * 1024 // 16K
            };

            app.UseWebSockets(webSocketOptions);
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await WaitForConnect(context, webSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }

            });

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                if (addSignalR)
                    endpoints.MapHub<CatanHub>("/CatanHub");
            });


        }

        private async Task WaitForConnect(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            Contract.Assert(!result.CloseStatus.HasValue);
            try
            {
                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                WsMessage message = CatanProxy.Deserialize<WsMessage>(json);
                if (message.MessageType != CatanWsMessageType.RegisterForGameNotifications)
                {
                    throw new Exception($"Invalid Message sent: {message} - DataTypeName=CatanWsMessageType");
                }

                await GameController.Games.RegisterWebSocket(context, webSocket); // runs until the end



            }
            catch (Exception e)
            {

                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(e.ToString()), 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
            }


            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }

   
}

