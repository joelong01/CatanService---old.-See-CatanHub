using System;
using System.Net.Http;
using Catan.Proxy;
using CatanService;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace ServiceTests
{
    public class ServiceFixture : IDisposable, IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        public ITestOutputHelper Output { get; private set; }

        public ServiceFixture(ITestOutputHelper output)
        {
            Output = output;

        }
        
        public void Dispose()
        {
        }
    }
}