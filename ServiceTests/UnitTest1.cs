using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System;
using Xunit;

namespace ServiceTests
{
    public class TestHostingEnvironment : IWebHostEnvironment
    {
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
    public class CatanServiceFixture : IDisposable
    {
        private readonly IConfigurationRoot configuration;
        private readonly IServiceCollection services;
        public ServiceProvider ServiceProvider { get; private set; }

        public CatanServiceFixture()
        {
            configuration = new ConfigurationBuilder().Build();
            services = new ServiceCollection()
                .AddLogging()                
                .AddSingleton<IWebHostEnvironment, TestHostingEnvironment>();
        }

        public void Initialize(Action<IServiceCollection> init)
        {
            init(services);
            ServiceProvider = services.BuildServiceProvider();            
        }

        public void Dispose()
        {
            ServiceProvider.Dispose();
        }
    }
    public class BasicTests
    { 

    }
}
