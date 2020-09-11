using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinRepro.SimpleDuplexService
{
    public class GrpcTestFixture<TStartup> : IDisposable where TStartup : class
    {
        class ResponseVersionHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken);
                response.Version = request.Version;

                return response;
            }
        }

        readonly TestServer server;
        readonly IHost host;
        public ILoggerFactory LoggerFactory { get; }
        public HttpClient Client { get; }

        public GrpcTestFixture(bool enableDebugLogging, Action<IServiceCollection> initialConfigureServices)
        {
            LoggerFactory = ConfigureLogging(enableDebugLogging);

            var builder = new HostBuilder()
                .ConfigureServices(services =>
                {
                    initialConfigureServices(services);
                    services.AddSingleton<ILoggerFactory>(LoggerFactory);
                })
                .ConfigureWebHostDefaults(webHost =>
                {
                    webHost
                        .UseTestServer()
                        .UseStartup<TStartup>();
                });
            host = builder.Start();
            server = host.GetTestServer();

            Client = ConfigureHttpClient();
        }

        ILoggerFactory ConfigureLogging(bool enableDebugLogging)
        {
            if (enableDebugLogging)
            {
                return Microsoft.Extensions.Logging.LoggerFactory.Create((logging) => {
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Debug);
                });
            }
            else
            {
                return new Microsoft.Extensions.Logging.LoggerFactory();
            }
        }

        HttpClient ConfigureHttpClient()
        {
            // Need to set the response version to 2.0.
            // Required because of this TestServer issue - https://github.com/aspnet/AspNetCore/issues/16940
            var responseVersionHandler = new ResponseVersionHandler();
            responseVersionHandler.InnerHandler = server.CreateHandler();

            var client = new HttpClient(responseVersionHandler);
            client.BaseAddress = new Uri("http://localhost");

            return client;
        }

        public void Dispose()
        {
            Client.Dispose();
            host.Dispose();
            server.Dispose();
        }
    }
}
