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
    public class PersistentQueueMonitoringSettings
    {
        public string EmailSubject { get; set; }
        public int QueueCheckInterval { get; set; }
        public int EmailSendInterval { get; set; }
        public int EmailAlertRecordsLevel { get; set; }
        public string EmailRecipients { get; set; }
        public int EmailAlertStopLevel { get; set; }
        public int EmailNotificationTimes { get; set; }
        public string EmailReplyAddress { get; set; }
    }

    public interface IPersistentQueueMonitoringTask { }

    public class PersistentQueueMonitoringTask : BackgroundService, IPersistentQueueMonitoringTask
    {
        private readonly ILogger<PersistentQueueMonitoringTask> _logger;
        private readonly PersistentQueueMonitoringSettings _settings;
        private readonly ISqliteQueueManager _sqliteQueueManager;

        public PersistentQueueMonitoringTask(IOptions<PersistentQueueMonitoringSettings> settings,
            ISqliteQueueManager sqliteQueueManager, ILogger<PersistentQueueMonitoringTask> logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _sqliteQueueManager = sqliteQueueManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                 var queueSize = _sqliteQueueManager.Count();
                // Multiple level warning?
                _sqliteQueueManager.IsClosed = (queueSize >= _settings.EmailAlertStopLevel);
                if ( queueSize >= _settings.EmailAlertRecordsLevel)
                {

                }
                await Task.Delay( TimeSpan.FromSeconds(_settings.QueueCheckInterval), stoppingToken);
            }
        }
    }
}
