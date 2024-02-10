using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using Savorboard.CAP.InMemoryMessageQueue;
using OpenTelemetry.Trace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.Configuration;

namespace GhostLyzer.Core.CAP
{
    /// <summary>
    /// Provides extension methods for the <see cref="IServiceCollection"/> interface.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Adds and configures CAP (DotNetCore.CAP) services.
        /// with OpenTelemetry and jaeger export.
        /// </summary>
        /// <remarks>
        /// Remember to set the configuration values for the Jaeger endpoint.
        /// Jaeger protocol, host, and port.
        /// </remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configuration">The application's configuration.</param>
        /// <returns>The same service collection so that multiple calls can be chained.</returns>
        public static IServiceCollection AddCustomCAP(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddCap(x =>
            {
                x.UseInMemoryStorage();
                x.UseInMemoryMessageQueue();

                x.UseDashboard();
                x.FailedRetryCount = 5;
                x.FailedThresholdCallback = failed =>
                {
                    var logger = failed.ServiceProvider.GetService<ILogger>();
                    logger.LogError(
                        $@"A message of type {failed.MessageType} failed after executing {x.FailedRetryCount} several times,
                            requiering manual troubleshooting. Message name: {failed.Message.GetName()}");
                };
                x.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            });

            services.Scan(s =>
                s.FromAssemblies(AppDomain.CurrentDomain.GetAssemblies())
                    .AddClasses(c => c.AssignableTo(typeof(ICapSubscribe)))
                    .AsImplementedInterfaces()
                    .WithScopedLifetime());

            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddCapInstrumentation()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint =
                            new Uri($"{configuration["Jaeger:Protocol"]}://{configuration["Jaeger:Host"]}:{configuration["Jaeger:Port"]}");
                    }));

            return services;
        }
    }
}
