using System;
using System.Net.Http;
using CatanService;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ServiceTests
{
    public class ServiceFixture: IDisposable, IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        public ServiceFixture()
        {
            var factory = new CustomWebApplicationFactory<Startup>();
            HttpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        public HttpClient HttpClient { get; set; }


        public string GameName  => "Game123";


        public void Dispose()
        {
        }
    }
}