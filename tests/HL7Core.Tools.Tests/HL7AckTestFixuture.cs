﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HL7Core.Tools.Tests
{
    [TestClass]
    public class HL7AckTestFixuture
    {
        [TestMethod]
        public void CanGenerateAck()
        {
            var acker = new HL7Acknowledger();
            var packet = @"MSH|^~\&|ADT|EPIC-PRD|QDXI|CHB|20120103161618||ADT^A03|12586919|P|2.3-CH|||NS
            var result = acker.CreateAckPacket(packet);
            Assert.AreEqual(result, "MSH|^~\\&|QDXI|CHB|ADT|EPIC-PRD|20120103161618||ACK^A03|12586919|P|2.3-CH\rMSA|AA|12586919");
        }
    }
}