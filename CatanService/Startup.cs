using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CatanService.Controllers;
using CatanService.Models;

namespace CatanService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromHours(12),
                ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);
            app.UseRouting();

            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api/registersocket"))
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        // looks like /api/registersocket/Game/User
                        var tokens = context.Request.Path.ToString().Split("/", StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length != 4)
                        {
                            context.Response.StatusCode = 400;
                            return;
                        }
                        string player = tokens[3];
                        string game = tokens[2];
                        var userId = new PlayerId
                        {
                            GameName = game.ToLower(),
                            PlayerName = player.ToLower()
                        };
                        var ret = CatanController.PlayersToResourcesDictionary.TryGetValue(userId, out PlayerResources resources);
                        if (!ret)
                        {
                            context.Response.StatusCode = 400;
                            return;
                        }
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await webSocket.SendAsync(Encoding.ASCII.GetBytes("testing"), WebSocketMessageType.Text, true, CancellationToken.None);
                        resources.WebSocket = webSocket;
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
        }

    }
}
