using dbmgr.utilities.common;

namespace dbmgr.utilities.common.Tests
{
    public class dbmgrCommonUtilityTests
    {
        [Fact]
        public void ReplaceTokensInContentRegressionTest()
        {
            string s;
            s = CommonUtilities.ReplaceTokensInContent(null, null);
            Assert.Null(s);

            string content = "THIS IS A TEST OF THE TOKEN REPLACEMENT LOGIC";
            s = CommonUtilities.ReplaceTokensInContent(content, null);
            Assert.Equal(content, s);

            Dictionary<string, string> replacementValues = new Dictionary<string, string>();
            s = CommonUtilities.ReplaceTokensInContent(content, replacementValues);
            Assert.Equal(content, s);

            replacementValues.Add("TEST", "VALUE");
            s = CommonUtilities.ReplaceTokensInContent(content, replacementValues);
            Assert.Equal(content, s);

            content = "THIS IS A #{TEST} OF THE #{TOKEN} REPLACEMENT LOGIC";
            Assert.Throws<ApplicationException>(() => CommonUtilities.ReplaceTokensInContent(content, replacementValues));

            string expected = "THIS IS A VALUE OF THE NEW REPLACEMENT LOGIC";
            replacementValues.Add("TOKEN", "NEW");
            s = CommonUtilities.ReplaceTokensInContent(content, replacementValues);
            Assert.Equal(expected, s);
        }

        [Fact]
        public void ReplaceRegressionTest()
        {
            Assert.Equal("AACDE", CommonUtilities.Replace("ABCDE", "B", "A", StringComparison.InvariantCultureIgnoreCase));
            Assert.Equal("AACDE", CommonUtilities.Replace("ABCDE", "b", "A", StringComparison.InvariantCultureIgnoreCase));
            Assert.Equal("AACDE", CommonUtilities.Replace("ABCDE", "B", "A", StringComparison.InvariantCulture));
            Assert.Equal("ABCDE", CommonUtilities.Replace("ABCDE", "b", "A", StringComparison.InvariantCulture));
            Assert.Equal("ABCDE", CommonUtilities.Replace("ABCDE", "G", "A", StringComparison.InvariantCultureIgnoreCase));
            Assert.Null(CommonUtilities.Replace(null, null, null, StringComparison.InvariantCultureIgnoreCase));
        }

        [Fact]
        public void ComputeAdlerCRCRegressionTest()
        {
            CommonUtilities.CRCResult expected = new CommonUtilities.CRCResult(0, 0);
            CommonUtilities.CRCResult crc_check;
            crc_check = CommonUtilities.ComputeAdlerCRC(null);
            Assert.Equal(expected, crc_check);
            crc_check = CommonUtilities.ComputeAdlerCRC("filenotfound.txt");
            Assert.Equal(expected, crc_check);

            // Stable?
            expected = new CommonUtilities.CRCResult(790105748, 38);
            crc_check = CommonUtilities.ComputeAdlerCRC("TestContent/CRCTest1.txt");
            Assert.Equal(expected, crc_check);
            crc_check = CommonUtilities.ComputeAdlerCRC("TestContent/CRCTest1.txt");
            Assert.Equal(expected, crc_check);

            // Not the same as another file
            CommonUtilities.CRCResult crc_check2 = CommonUtilities.ComputeAdlerCRC("TestContent/CRCTest2.txt");
            Assert.NotEqual(crc_check, crc_check2);
        }

        [Fact]
        public void TopSortRegressionTest()
        {
            Dictionary<string, int> result;
            result = CommonUtilities.TopSort(null);
            Assert.Empty(result);

            Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();
            result = CommonUtilities.TopSort(dependencies);
            Assert.Empty(result);

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
            Assert.Equal(3, result.Count);
            Assert.Equal(expected["1"], result["1"]);
            Assert.Equal(expected["2"], result["2"]);
            Assert.Equal(expected["3"], result["3"]);

            dependencies.Add("4", new List<string>() { "5" });
            dependencies.Add("5", new List<string>() { "4" });
            Assert.Throws<NotSupportedException>(() => CommonUtilities.TopSort(dependencies));
        }

        [Fact]
        public void GetEmbeddedResourceContentRegressionTest()
        {
            string? s = CommonUtilities.GetEmbeddedResourceContent(null);
            Assert.Null(s);

            s = CommonUtilities.GetEmbeddedResourceContent("garbage.file");
            Assert.Null(s);

            s = CommonUtilities.GetEmbeddedResourceContent("template.up");
            Assert.NotNull(s);

            s = CommonUtilities.GetEmbeddedResourceContent("template.down");
            Assert.NotNull(s);
        }
    }
}
