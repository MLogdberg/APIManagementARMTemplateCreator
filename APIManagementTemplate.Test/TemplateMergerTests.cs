using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class TemplateMergerTests
    {
        [TestInitialize()]
        public void Initialize()
        {
        }

        [TestMethod]
        public void TestSimpleMerge()
        {
            AssertMerge(new { }, new { }, new { });
            AssertMerge(new { a = 1 }, new { }, new { a = 1 });
            AssertMerge(new { }, new { a = 1 }, new { a = 1 });
            AssertMerge(new { a = 1 }, new { a = 2 }, new { a = 2 });
        }

        [TestMethod]
        public void TestArrayMerge()
        {
            AssertMerge(new { resources = new[] { new { name = "1", a = 1 } } }, new { }, new { resources = new[] { new { name = "1", a = 1 } } });
        }

        [TestMethod]

        public void TestPropertyMerge2()
        {
            AssertMerge(
                new { a = new { a = 1, b = 2 } },
                new { a = new { } },
                new { a = new { a = 1, b = 2 } });
        }

        [TestMethod]
        public void TestComplexArrayAdd()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", a = 1 } } },
                new { resources = new object[] { new { name = "2", b = 1 } } },
                new { resources = new object[] { new { name = "1", a = 1 }, new { name = "2", b = 1 } } });
        }

        [TestMethod]
        public void TestComplexArrayMerge()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", type = "1", a = 1 } } },
                new { resources = new object[] { new { name = "1", type = "1", a = 2 } } },
                new { resources = new object[] { new { name = "1", type = "1", a = 2 } } });
        }
        [TestMethod]
        public void TestComplexArrayMerge2()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", type = "1", a = 1 } } },
                new { resources = new object[] { new { name = "1", type = "1", b = 1 } } },
                new { resources = new object[] { new { name = "1", type = "1", a = 1, b = 1 } } });
        }

        [TestMethod]
        public void TestComplexArrayMerge3()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", a = 1 } } },
                new { resources = new object[] { new { b = 1 } } },
                new { resources = new object[] { new { name = "1", a = 1 }, new { b = 1 } } });
        }
        [TestMethod]
        public void TestComplexArrayMergeOnResources()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", type="1", a = 1 } } },
                new { resources = new object[] { new { name = "1", type = "1", a = 2 } } },
                new { resources = new object[] { new { name = "1", type = "1", a = 2 } } });
        }

        [TestMethod]
        public void TestComplexArrayMergeOnResponses()
        {
            AssertMerge(
                new { responses = new object[] { new { statusCode = 200, a = 1 } } },
                new { responses = new object[] { new { statusCode = 200, a = 2 } } },
                new { responses = new object[] { new { statusCode = 200, a = 2 } } });
        }

        [TestMethod]
        public void TestComplexArrayMergeOnRepresentations()
        {
            AssertMerge(
                new { representations = new object[] { new { contentType = "application/json", a = 1 } } },
                new { representations = new object[] { new { contentType = "application/json", a = 2 } } },
                new { representations = new object[] { new { contentType = "application/json", a = 2 } } });
        }

        [TestMethod]
        public void TestComplexArrayMergeOnTemplateParameters()
        {
            AssertMerge(
                new { templateParameters = new object[] { new { name = "param1", a = 1 } } },
                new { templateParameters = new object[] { new { name = "param1", a = 2 } } },
                new { templateParameters = new object[] { new { name = "param1", a = 2 } } });
        }

        [TestMethod]
        public void TestComplexArrayMergeOnUnknown()
        {
            AssertMerge(
                new { unknown = new object[] { new { name = "param1", a = 1 } } },
                new { unknown = new object[] { new { name = "param1", a = 2 } } },
                new { unknown = new object[] { new { name = "param1", a = 1 }, new { name = "param1", a = 2 } } });
        }

        [TestMethod]
        public void TestComplexArrayMergeStringValues1()
        {
            AssertMerge(
                new { dependsOn = new object[] { "a", "shouldBeSame" } },
                new { dependsOn = new object[] { "c" } },
                new { dependsOn = new object[] { "a", "shouldBeSame", "c" } });
        }

        [TestMethod]
        public void TestComplexArrayMergeStringValues2()
        {
            AssertMerge(
                new { dependsOn = new object[] { "a", "shouldBeSame" } },
                new { dependsOn = new object[] { "a", "c" } },
                new { dependsOn = new object[] { "a", "shouldBeSame", "c" } });
        }

        [TestMethod]
        public void TestSuperComplexArrayMerge1()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", type = "1", resources = new object[] { new { name = "1", type = "1", a = 1 } } } } },
                new { resources = new object[] { new { name = "1", type = "1", resources = new object[] { new { name = "1", type = "1", a = 2 } } } } },
                new { resources = new object[] { new { name = "1", type = "1", resources = new object[] { new { name = "1", type = "1", a = 2 } } } } });
        }

        [TestMethod]
        public void TestSuperComplexArrayMerge2()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", type = "1", resources = new object[] { new { name = "1", type = "1", a = 1 } } } } },
                new { resources = new object[] { new { name = "1", type = "1", resources = new object[] { new { name = "1", type = "1", b = 1 } } } } },
                new { resources = new object[] { new { name = "1", type = "1", resources = new object[] { new { name = "1", type = "1", a = 1, b = 1 } } } } });
        }

        [TestMethod]
        public void TestSuperComplexArrayMerge3()
        {
            AssertMerge(
                new { resources = new object[] { new { id = "1" } } },
                new { resources = new object[] { new { id = "1" } } },
                new { resources = new object[] { new { id = "1" } } });
        }

        [TestMethod]
        public void TestDifferentType()
        {
            AssertMerge(
                new { resources = new object[] { new { name = "1", type = "1", a = 1 } } },
                new { resources = new object[] { new { name = "1", type = "2", a = 2 } } },
                new { resources = new object[] { new { name = "1", type = "1", a = 1 }, new { name = "1", type = "2", a = 2 } } });
        }

        [TestMethod]
        public void TestSameNameButWithSpacesBetweenNames()
        {
            TestSameName(
                "[concat(parameters('apimServiceName'),'/','c341bf18-54fb-4685-ad4b-3e49b4a847c2')]", 
                "[concat(parameters('apimServiceName'), '/', 'c341bf18-54fb-4685-ad4b-3e49b4a847c2')]");
        }

        [TestMethod]
        public void TestSameNameButWithMoreSpacesBetweenNames()
        {
            TestSameName(
                "[concat(parameters('apimServiceName'),'/','c341bf18-54fb-4685-ad4b-3e49b4a847c2')]", 
                "[concat( parameters('apimServiceName'), '/' , 'c341bf18-54fb-4685-ad4b-3e49b4a847c2' )]");
        }

        [TestMethod]
        public void TestSameNameButWithSpacesInName()
        {
            TestSameName(
                "[concat(parameters('apimServiceName'),'/','c341bf18-54fb-4685-ad4b-3e49b4a847c2')]", 
                "[concat( parameters('apimServiceName '), '/' , 'c341bf18-54fb-4685-ad4b-3e49b4a847c2' )]");
        }

        private static void TestSameName(string name1, string name2, bool shouldBeSame=true)
        {
            AssertMerge(
                new {resources = new object[] {new {name = name1, type = "1", a = 1}}},
                new {resources = new object[] {new {name = name2, type = "1", a = 2}}},
                new {resources = new object[] {new {name = name2, type = "1", a = 2}}}, shouldBeSame);
        }


        private static void AssertMerge(object oldObject, object newObject, object expected, bool shouldBeSame=true)
        {
            JObject result = TemplateMerger.Merge(JObject.FromObject(oldObject), JObject.FromObject(newObject));

            Assert.AreEqual(shouldBeSame, JObject.EqualityComparer.Equals(JObject.FromObject(expected), result));
        }
    }
}