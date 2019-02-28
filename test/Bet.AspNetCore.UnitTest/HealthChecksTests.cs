﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Bet.AspNetCore.UnitTest
{
    public class HealthChecksTests
    {
        [Fact]
        public async Task Return_Unhealthy_On_SIGTERM()
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddHealthChecks().AddSigtermCheck("sigterm_check");
                })
                .Configure(app=>
                {
                    app.UseHealthChecks("/hc");
                });

            var server = new TestServer(builder);
            var client = server.CreateClient();

            var appLifetime = server.Host.Services.GetService<IApplicationLifetime>();

            appLifetime.StopApplication();

            var response = await client.GetAsync("hc");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task Return_Ok_Status()
        {
            var statusCode = 200;

            var factoryMock = new Mock<IHttpClientFactory>();
            var fakeHttpMessageHandler = new FakeHttpMessageHandler(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
            });

            factoryMock.Setup(x => x.CreateClient("Successful")).Returns(new HttpClient(fakeHttpMessageHandler));
            var hostBuilder = new WebHostBuilder()
                .ConfigureTestServices(services =>
                {
                    services.AddSingleton<IHttpClientFactory>(factoryMock.Object);

                    services.AddHealthChecks()
                    .AddUriHealthCheck("Successful", builder =>
                    {
                        builder.Add(options =>
                        {
                            options
                            .AddUri($"http://localhost/api/HttpStat/${statusCode}")
                            .UseTimeout(TimeSpan.FromSeconds(60))
                            .UseExpectedHttpCode(statusCode);
                        });
                    });
                })
                .UseStartup<TestStartup>();

            var client = new TestServer(hostBuilder).CreateClient();


            var result = await client.GetAsync("/healthy");
            Assert.Equal(statusCode, (int)result.StatusCode);
        }
    }
}
