using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HL7Core.Service.Configuration
{
    public class Hl7ListenerSettings
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string LogName { get; set; }
        public long BufferLimit { get; set; }

    }

 }
