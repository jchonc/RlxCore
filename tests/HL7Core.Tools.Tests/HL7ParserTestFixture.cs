using Microsoft.VisualStudio.TestTools.UnitTesting;
using HL7Core.Tools;

namespace HL7Core.Tools.Tests
{
    [TestClass]
    public class HL7ParserTestFixture
    {
        [TestMethod]
        public void CanParseLLP()
        {
            const string packet = @"MSH|^~\&|ADT|EPIC-PRD|QDXI|CHB|20120103161618||ADT^A03|12586919|P|2.3-CH|||NS
            var result = HL7Parser.LoadPackage(packet);
            Assert.IsNotNull(result);
        }
    }
}