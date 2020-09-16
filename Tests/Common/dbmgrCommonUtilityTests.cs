using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace dbmgr.utilities.common.Tests
{
    [TestClass]
    public class dbmgrCommonUtilityTests
    {
        [TestMethod()]
        public void ReplaceTokensInContentRegressionTest()
        {
            string s;
            s = CommonUtilities.ReplaceTokensInContent(null, null);
            Assert.IsNull(s);

            string content = "THIS IS A TEST OF THE TOKEN REPLACEMENT LOGIC";
            s = CommonUtilities.ReplaceTokensInContent(content, null);
            Assert.AreEqual(content, s);

            Dictionary<string, string> replacementValues = new Dictionary<string, string>();
            s = CommonUtilities.ReplaceTokensInContent(content, replacementValues);
            Assert.AreEqual(content, s);

            replacementValues.Add("TEST", "VALUE");
            s = CommonUtilities.ReplaceTokensInContent(content, replacementValues);
            Assert.AreEqual(content, s);

            content = "THIS IS A #{TEST} OF THE #{TOKEN} REPLACEMENT LOGIC";
            Assert.ThrowsException<ApplicationException>(() => CommonUtilities.ReplaceTokensInContent(content, replacementValues));

            string expected = "THIS IS A VALUE OF THE NEW REPLACEMENT LOGIC";
            replacementValues.Add("TOKEN", "NEW");
            s = CommonUtilities.ReplaceTokensInContent(content, replacementValues);
            Assert.AreEqual(expected, s);
        }

        [TestMethod()]
        public void ReplaceRegressionTest()
        {
            Assert.AreEqual("AACDE", CommonUtilities.Replace("ABCDE", "B", "A", StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual("AACDE", CommonUtilities.Replace("ABCDE", "b", "A", StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual("AACDE", CommonUtilities.Replace("ABCDE", "B", "A", StringComparison.InvariantCulture));
            Assert.AreEqual("ABCDE", CommonUtilities.Replace("ABCDE", "b", "A", StringComparison.InvariantCulture));
            Assert.AreEqual("ABCDE", CommonUtilities.Replace("ABCDE", "G", "A", StringComparison.InvariantCultureIgnoreCase));
            Assert.IsNull(CommonUtilities.Replace(null, null, null, StringComparison.InvariantCultureIgnoreCase));
        }

        [TestMethod()]
        public void ComputeAdlerCRCRegressionTest()
        {
            (uint crc, ulong length) expected = (0, 0);
            (uint crc, ulong length) crc_check;
            crc_check = CommonUtilities.ComputeAdlerCRC(null);
            Assert.AreEqual(expected, crc_check);
            crc_check = CommonUtilities.ComputeAdlerCRC("filenotfound.txt");
            Assert.AreEqual(expected, crc_check);

            // Stable?
            expected = (790105748, 38);
            crc_check = CommonUtilities.ComputeAdlerCRC("TestContent/CRCTest1.txt");
            Assert.AreEqual(expected, crc_check);
            crc_check = CommonUtilities.ComputeAdlerCRC("TestContent/CRCTest1.txt");
            Assert.AreEqual(expected, crc_check);

            // Not the same as another file
            (uint crc, ulong length) crc_check2 = CommonUtilities.ComputeAdlerCRC("TestContent/CRCTest2.txt");
            Assert.AreNotEqual(crc_check, crc_check2);
        }

        [TestMethod()]
        public void TopSortRegressionTest()
        {
            Dictionary<string, int> result;
            result = CommonUtilities.TopSort(null);
            Assert.AreEqual(0, result.Count);

            Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();
            result = CommonUtilities.TopSort(dependencies);
            Assert.AreEqual(0, result.Count);

            Dictionary<string, int> expected = new Dictionary<string, int>
            {
                { "3", 1 },
                { "2", 2 },
                { "1", 3 }
            };

            dependencies.Add("1", new List<string>() { "2", "3" });
            dependencies.Add("2", new List<string>() { "3" });
            dependencies.Add("3", new List<string>() { });
            result = CommonUtilities.TopSort(dependencies);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(expected["1"], result["1"]);
            Assert.AreEqual(expected["2"], result["2"]);
            Assert.AreEqual(expected["3"], result["3"]);

            dependencies.Add("4", new List<string>() { "5" });
            dependencies.Add("5", new List<string>() { "4" });
            Assert.ThrowsException<NotSupportedException>(() => CommonUtilities.TopSort(dependencies));
        }

        [TestMethod()]
        public void GetEmbeddedResourceContentRegressionTest()
        {
            string s = CommonUtilities.GetEmbeddedResourceContent(null);
            Assert.IsNull(s);

            s = CommonUtilities.GetEmbeddedResourceContent("garbage.file");
            Assert.IsNull(s);

            s = CommonUtilities.GetEmbeddedResourceContent("template.up");
            Assert.IsNotNull(s);

            s = CommonUtilities.GetEmbeddedResourceContent("template.down");
            Assert.IsNotNull(s);
        }
    }
}
