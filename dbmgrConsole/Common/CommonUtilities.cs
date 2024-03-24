using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;

namespace dbmgr.utilities.common
{
    /// <summary>
    /// Utility string and content methods
    /// </summary>
    public static class CommonUtilities
    {
        public const string START_TOKEN = "#{";
        public const string END_TOKEN = "}";

        /// <summary>
        /// replace tokens in the given content
        /// </summary>
        public static string ReplaceTokensInContent(string? content, Dictionary<string, string>? replacementValues)
        {
            if (replacementValues == null)
            {
                return content;
            }

            foreach (string token in replacementValues.Keys)
            {
                content = ReplaceTokenInContent(content, token, replacementValues[token]);
            }

            // Check content has no more tokens left
            if (ContentHasTokens(content))
            {
                throw new ApplicationException("unmatched tokens in content");
            }

            return content;
        }

        public static string ShowElapsedTime(this Stopwatch time)
        {
            return string.Format("{0:00}:{1:00}:{2:00}.{3:00}", time.Elapsed.Hours, time.Elapsed.Minutes, time.Elapsed.Seconds, time.Elapsed.Milliseconds / 10);
        }

        public static string Replace(this string? originalString, string? oldValue, string? newValue, StringComparison comparisonType)
        {
            if (originalString == null || oldValue == null || newValue == null)
                return originalString;

            int startIndex = 0;
            while (true)
            {
                startIndex = originalString.IndexOf(oldValue, startIndex, comparisonType);
                if (startIndex == -1)
                    break;

                originalString = originalString.Substring(0, startIndex) + newValue + originalString.Substring(startIndex + oldValue.Length);

                startIndex += newValue.Length;
            }

            return originalString;
        }

        public struct CRCResult
        {
            public uint crc;
            public ulong length;

            public CRCResult(uint crc, ulong length)
            {
                this.crc = crc;
                this.length = length;
            }
        }

        public static CRCResult ComputeAdlerCRC(string? fileName)
        {
            const ushort MOD_ADLER = 65521;

            CRCResult crcResult = new CRCResult();

            uint a = 1;
            uint b = 0;
            ulong totalLength = 0;

            try
            {
                if (File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (fs != null)
                        {
                            while (true)
                            {
                                int i = 0;
                                byte[] data = new byte[5550];
                                int len = fs.Read(data, 0, 5550);
                                totalLength += (uint)len;
                                if (len > 0)
                                {
                                    int tlen = len > 5550 ? 5550 : len;
                                    do
                                    {
                                        a += data[i++];
                                        b += a;
                                    } while (--tlen > 0);

                                    a = (a & 0xffff) + (a >> 16) * (65536 - MOD_ADLER);
                                    b = (b & 0xffff) + (b >> 16) * (65536 - MOD_ADLER);
                                }
                                else
                                {
                                    break;
                                }
                            }

                            /* It can be shown that a <= 0x1013a here, so a single subtract will do. */
                            if (a >= MOD_ADLER)
                                a -= MOD_ADLER;

                            /* It can be shown that b can reach 0xffef1 here. */
                            b = (b & 0xffff) + (b >> 16) * (65536 - MOD_ADLER);
                            if (b >= MOD_ADLER)
                                b -= MOD_ADLER;

                            crcResult.crc = (b << 16) | a;
                            crcResult.length = totalLength;
                            return crcResult;
                        }
                    }
                }
            }
            catch
            {
                totalLength = 0;
            }

            crcResult.crc = 0;
            crcResult.length = totalLength;
            return crcResult;
        }


        public static Dictionary<string, int> TopSort(Dictionary<string, List<string>>? dependencies, bool breakCycles = false)
        {
            Dictionary<string, int> newOrder = new Dictionary<string, int>();


            int order = 0;
            while (dependencies?.Count > 0)
            {
                int breaker = 0;
                List<string> noDeps = dependencies.Where(n => n.Value.Count == 0).Select(n => n.Key).ToList();
                while (breakCycles && noDeps.Count == 0 && breaker < 20)
                {
                    breaker++;
                    noDeps = dependencies.Where(n => n.Value.Count == breaker).Select(n => n.Key).ToList();
                }

                if (noDeps.Count == 0)
                {
                    foreach (string k in dependencies.Keys)
                    {
                        List<string> dependents = dependencies[k];
                        foreach (string d in dependents)
                        {
                            Log.Logger.Error("Circular dependency in {0} which depends on {1}", d, k);
                        }
                    }
                    throw new NotSupportedException("There is a circular dependency in the current scripts.  Review the log for details.  Please correct before migrating.");
                }

                order++;
                foreach (string s in noDeps)
                {
                    newOrder.Add(s, order);

                    // Pull this out of anyone that depends on it
                    foreach (List<string> l in dependencies.Values)
                    {
                        if (l.Contains(s))
                        {
                            l.Remove(s);
                        }
                    }

                    dependencies.Remove(s);
                }
            }

            return newOrder;
        }

        public static string? GetEmbeddedResourceContent(string? resourceName)
        {
            if (resourceName == null)
                return null;

            Assembly assembly = Assembly.GetExecutingAssembly();
            if (assembly != null)
            {
                string? filename = assembly.GetManifestResourceNames().Where(n => n.Contains(resourceName)).FirstOrDefault();
                if (filename != null)
                {
                    using (Stream? s = assembly.GetManifestResourceStream(filename))
                    {
                        if (s != null)
                        {
                            // HACK: Had to hack this since first 3 bytes seem wrong... and didn't want to spend time on this
                            byte[] bytes = new byte[s.Length - 3];
                            s.Position = 3;
                            s.Read(bytes, 0, (int)s.Length - 3);
                            return Encoding.Default.GetString(bytes);
                        }
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// replace token in the given content
        /// </summary>
        private static string ReplaceTokenInContent(string content, string token, string value)
        {
            if (String.IsNullOrEmpty(token) || token.StartsWith("#"))
            {
                return content;
            }

            token = $"{START_TOKEN}{token}{END_TOKEN}";
            value = value ?? "";
            value = value.Replace("\r", "");

            if (content != null && content.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) > -1)
            {
                Log.Logger.Debug($"Replacing token '{token}' with value '{value}'");
                content = content.Replace(token, value, StringComparison.InvariantCultureIgnoreCase);
            }

            return content;
        }

        /// <summary>
        /// validates that all tokens have been replaced in the content
        /// </summary>
        private static bool ContentHasTokens(string content)
        {
            // Check content has no more tokens left
            MatchCollection tokensLeft = Regex.Matches(content, START_TOKEN + @"\w+" + END_TOKEN);

            if (tokensLeft.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Match m in tokensLeft)
                {
                    sb.Append(m.Value);
                    sb.Append(Environment.NewLine);
                }
                Log.Logger.Error("ERROR!  There are unmatched tokens left in the file: {0}", sb.ToString());

                return true;
            }

            return false;
        }
    }
}
