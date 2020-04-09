using System;
using System.Net.Http;
using Catan.Proxy;
using CatanService;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ServiceTests
{
    public class ServiceFixture : IDisposable, IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        public ServiceFixture()
        {
            var factory = new CustomWebApplicationFactory<Startup>();
            HttpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            Proxy.Client = HttpClient;

        }
        public CatanProxy Proxy { get; } = new CatanProxy();
        public HttpClient HttpClient { get; set; }
        public string HostName => "http://localhost:5000";

        public string GameName => "Game123";


        public void Dispose()
        {
        }
    }
}