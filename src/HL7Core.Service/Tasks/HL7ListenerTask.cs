using HL7Core.PersistentQueue;
using HL7Core.Service.Configuration;
using HL7Core.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HL7Core.Service.Tasks
{

    public class HL7ListenerService : BackgroundService
    {
        const char HL7_BOP = (char)11;
        const char HL7_EOP = (char)28;

        private readonly ILogger _logger;
        private readonly Hl7ListenerSettings _settings;
        private readonly ISqliteQueueManager _sqliteQueueManager;
        private readonly IHL7Acknowledger _hl7Acknowledger;
        private readonly TcpListener _listener;

        public HL7ListenerService (IOptions<Hl7ListenerSettings> settings,
            IHL7Acknowledger hl7Acknowledger, ISqliteQueueManager sqliteQueueManager, ILoggerFactory logFactory)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _sqliteQueueManager = sqliteQueueManager;
            _hl7Acknowledger = hl7Acknowledger;
            _logger = logFactory.CreateLogger(_settings.Name);

            var serverIp = IPAddress.Any;
            if(!string.IsNullOrEmpty(_settings.IpAddress))
            {
                serverIp = IPAddress.Parse(_settings.IpAddress);
            }
            _listener = new TcpListener(new IPEndPoint(serverIp, _settings.Port));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting");
                _listener.Start();
                while (!stoppingToken.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Factory.StartNew(() => ReceiveClientData(client, stoppingToken));
                }
            }
            finally
            {
                _listener.Stop();
            }
        }

        protected async void ReceiveClientData(TcpClient client, CancellationToken ct)
        {
            using (var stream = client.GetStream())
            {
                byte[] bytes = new Byte[1024];
                int dataRead = 0;
                var buffer = new StringBuilder();

                while ((dataRead = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    if (ct.IsCancellationRequested) break;
                    string data = System.Text.Encoding.ASCII.GetString(bytes, 0, dataRead);
                    var packets = ExtractPackets(buffer, data);
                    foreach (var packet in packets)
                    {
                        await HandlePacket(packet, stream);
                    }
                }
            }
        }

        protected List<string> ExtractPackets(StringBuilder leftOver, string receivedData)
        {
            List<string> results = new List<string>();
            foreach (var ch in receivedData)
            {
                if (ch == HL7_BOP)
                {
                    leftOver.Clear();
                    leftOver.Append(ch);
                }
                else if (ch == HL7_EOP)
                {
                    if (leftOver[0] == HL7_BOP)
                    {
                        results.Add(leftOver.ToString().Substring(1));
                    }
                    else
                    {
                        _logger.LogWarning("Invalid data received. Received end of package symbol without pairing start of package symbol.");
                        _logger.LogDebug("[DATA]: {0}", leftOver.ToString());
                    }
                }
                else
                {
                    if (leftOver.Length < _settings.BufferLimit)
                    {
                        leftOver.Append(ch);
                    }
                    else
                    {
                        leftOver.Clear();
                        _logger.LogWarning("Maximum buffer limit is reached. Flushing the buffer.");
                    }
                }
            }
            return results;
        }

        protected async Task HandlePacket(string packet, Stream netwrokStream)
        {
            var acknowledgement = HL7_BOP + _hl7Acknowledger.CreateAckPacket(packet) + HL7_EOP;
            if(!_sqliteQueueManager.IsClosed)
            {
                _sqliteQueueManager.Enqueue(packet);
            }
            byte[] msg = System.Text.Encoding.ASCII.GetBytes(acknowledgement);
            await netwrokStream.WriteAsync(msg, 0, msg.Length);
        }
    }
}
