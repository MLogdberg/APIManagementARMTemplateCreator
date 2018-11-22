using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace APIManagementTemplate.Test
{
    [TestClass]
    public class JObjectMergerTests
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
        public void TestComplexArrayMergeStringValues1()
        {
            AssertMerge(
                new { dependsOn = new object[] { "a", "b" } },
                new { dependsOn = new object[] { "c" } },
                new { dependsOn = new object[] { "a", "b", "c" } });
        }

        [TestMethod]
        public void TestComplexArrayMergeStringValues2()
        {
            AssertMerge(
                new { dependsOn = new object[] { "a", "b" } },
                new { dependsOn = new object[] { "a", "c" } },
                new { dependsOn = new object[] { "a", "b", "c" } });
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


        private static void AssertMerge(object oldObject, object newObject, object expected)
        {
            JObject result = JObjectMerger.Merge(JObject.FromObject(oldObject), JObject.FromObject(newObject), "name");

            Assert.IsTrue(JObject.EqualityComparer.Equals(JObject.FromObject(expected), result));
        }
    }
}