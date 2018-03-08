using HL7Core.PersistentQueue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HL7Core.Service.Tasks
{
    public class HL7HandlerSettings
    {
        public string DatabaseName { get; set; }
        public int IdleWhenEmpty { get; set; }
    }

    public interface IHL7HandlerManager
    {

    }

    public class HL7HandlerManager : BackgroundService, IHL7HandlerManager
    {
        private readonly ILogger<HL7HandlerManager> _logger;
        private readonly HL7HandlerSettings _settings;
        private readonly ISqliteQueueManager _sqliteQueueManager;
        private readonly IPersistentQueueMonitoringTask _queueMonitor;

        public HL7HandlerManager(IOptions<HL7HandlerSettings> settings, 
            ISqliteQueueManager sqliteQueueManager, IPersistentQueueMonitoringTask queueManager, ILogger<HL7HandlerManager> logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _sqliteQueueManager = sqliteQueueManager;
            _queueMonitor = queueManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                var result = _sqliteQueueManager.ProcessAllItems(HandlePacket, stoppingToken);
                if (result == 0)
                {
                    await Task.Delay(_settings.IdleWhenEmpty, stoppingToken);
                }
                else
                {
                    await Task.Yield();
                }
            }
        }

        protected void HandlePacket(string packet)
        {
            // Is this the XML already?
        }
    }
}
