using Wonder.Infrastructure;

namespace SomeTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestxPrint()
        {
            xPrint xp = new();
            xp.AppendLine("url: http://localhost:80");
            xp.ToString();
        }
        [TestMethod]
        public void TestxPrint_Build()
        {
            xPrint xp = new();
            xp.ToString();
        }
    }
}