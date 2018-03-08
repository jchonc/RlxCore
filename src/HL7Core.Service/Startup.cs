using Autofac;
using Autofac.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HL7Core.PersistentQueue;
using HL7Core.Service.Configuration;
using HL7Core.Service.Tasks;
using HL7Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HL7Core.Service
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();

            services.AddTransient<IHL7Acknowledger, HL7Acknowledger>();

            // Setup the persistent queue first.
            services.Configure<SqliteQueueManager>(options => Configuration.GetSection("Queue").Bind(options));
            services.AddSingleton<ISqliteQueueManager, SqliteQueueManager>();

            // Setup the background task for queue sanity keeper
            services.Configure<PersistentQueueMonitoringSettings>(options => Configuration.GetSection("QueueMonitoring").Bind(options));
            services.AddSingleton<IPersistentQueueMonitoringTask, PersistentQueueMonitoringTask>();

            // Setup the consumer of the queue
            services.AddSingleton<IHL7HandlerManager, HL7HandlerManager>();

            // Now let's spin up all the listeners, they are all producers to the queue
            var listenerConfigurations = Configuration.GetSection("Listeners");

            foreach (var configuration in listenerConfigurations.GetChildren())
            {
                var listenerSettings = new Hl7ListenerSettings();
                configuration.Bind(listenerSettings);

                services.AddTransient<HL7ListenerService>((factory) =>
                {
                    return new HL7ListenerService(Options.Create<Hl7ListenerSettings>(listenerSettings),
                        factory.GetRequiredService<IHL7Acknowledger>(),
                        factory.GetRequiredService<ISqliteQueueManager>(),
                        factory.GetRequiredService<ILoggerFactory>());
                });
            }

            var container = new ContainerBuilder();
            container.Populate(services);
            return new AutofacServiceProvider(container.Build());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            app.Map("/liveness", lapp => lapp.Run(async ctx => ctx.Response.StatusCode = 200));
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }
    }
}
