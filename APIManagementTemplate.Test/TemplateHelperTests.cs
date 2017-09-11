using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class TemplateHelperTests
    {


        [TestMethod]
        public void TestLoadModel()
        {
            var document = Utils.GetEmbededFileContent("APIManagementTemplate.Test.Samples.StandardInstance-New.json");
            Assert.IsNotNull(document);

        }
    }
}
