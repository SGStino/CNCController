using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CNCController.Tests
{
    [TestClass]
    public class CommunicationsTests
    {
        [TestMethod]
        public void TestFindMsg()
        {
            testFindMsgAt(0);
            testFindMsgAt(15);

            testFindMsgAt(9);
        }

        private void testFindMsgAt(byte position)
        {
            var buffer = new byte[512];
            Array.Copy(Communications.PREFIX, 0, buffer, position, Communications.PREFIX.Length);

            byte index;
            var result = Communications.findMsg(buffer, out index);
            Assert.IsTrue(result);
            Assert.AreEqual(index, position);
        }
    }
}
