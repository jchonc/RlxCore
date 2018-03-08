using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace HL7Core.Tools
{
    public interface IHL7Acknowledger
    {
        string CreateAckPacket(string packet);
    }

    public class HL7Acknowledger: IHL7Acknowledger
    {
        protected string[] ExtractMSH(string packet)
        {
            int n = packet.IndexOf("MSH");
            if( n >= 0 )
            {
                int m = packet.IndexOf("\r", n);
                if( m >= 0 )
                {
                    string temp = packet.Substring(n, m);
                    return temp.Split('|');
                }
            }
            return null;
        } 

        public string CreateAckPacket(string packet)
        {
            string[] mshParts = ExtractMSH(packet);
            if(mshParts != null)
            {
                var sendingApplication = mshParts[4];
                var sendingFacility = mshParts[5];
                var receivingApplication = mshParts[2];
                var receivingFacility = mshParts[3];
                var messageDateTime = mshParts[6];
                var messageType = mshParts[8];
                messageType = "ACK" + messageType.Substring(messageType.IndexOf('^'));
                var messageControlId = mshParts[9];
                var versionId = mshParts[11];
                return $"MSH|^~\\&|{sendingApplication}|{sendingFacility}|{receivingApplication}|{receivingFacility}|{messageDateTime}||{messageType}|{messageControlId}|P|{versionId}\rMSA|AA|{messageControlId}";
            }
            return null;
        }

    }
}
