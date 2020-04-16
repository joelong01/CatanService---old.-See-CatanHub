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
           

        }
        
        public void Dispose()
        {
        }
    }
}