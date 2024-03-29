﻿using dbmgr.utilities;
using dbmgr.utilities.data;
using dbmgr.utilities.db;
using dbmgr.utilities.Db;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace dbmgr.utilities.Tests
{
    public class dbmgrDataMigrationTests
    {
        public dbmgrDataMigrationTests()
        {            
        }

        private Mock<NetDatabaseConfiguration> GetMockConfiguration(string dbkey)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.IsNullOrWhiteSpace(environment)) { environment = "Production"; }

            // Set working directory to where the binaries are
            string? cwd = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            Directory.SetCurrentDirectory(cwd ?? ".");
            string baseDirectory = Directory.GetCurrentDirectory();

            IConfigurationRoot _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .Build();

            var mock = new Mock<NetDatabaseConfiguration>(dbkey, _configuration);
            return mock;
        }

        private Mock<dbmgrDataMigration> GetMockDataMigration(SQLServerScripts ss)
        {
            var mock = new Mock<dbmgrDataMigration>(GetMockConfiguration(ss.DbConnectionKey).Object, "Tests", ss, null, null, null);
            return mock;
        }

        [Fact]
        public void TestCreateScriptFileHasValidFileName()
        {
            string expected_filename = "_create_admin_table";

            var mock = GetMockDataMigration(new SQLServerScripts());
            mock.Setup(foo => foo.CurrentTimestamp).Returns(DateTime.UtcNow);
            mock.Setup(foo => foo.WriteTextToFile(It.Is<string>(i => i.Contains(expected_filename)), It.Is<string>(j => !string.IsNullOrWhiteSpace(j))));

            string result = mock.Object.CreateScriptFile("create admin table");
            Assert.EndsWith(expected_filename, result);
        }

        [Fact]
        public void TestHaveConnectivityNoConnectionString()
        {
            var ss = new SQLServerScripts();
            dbmgrDataMigration db = new dbmgrDataMigration(GetMockConfiguration(ss.DbConnectionKey).Object, ".", ss, null, null);
            Assert.Throws<ArgumentException>(() => db.HaveConnectivity());
        }

        [Fact]
        public void TestHaveConnectivity()
        {
            SQLServerScripts ss = new SQLServerScripts();
            string expectedSQL = ss.GetTestConnectionSQL();

            var mock = GetMockDataMigration(ss);
            var dcmock = new Mock<DataContext>(null, "Mock Connection String", null, 20, 20);
            dcmock.Setup(foo => foo.ExecuteScalar(It.Is<string>(i => i != expectedSQL), It.IsAny<List<IDbDataParameter>>())).Throws(new Exception());
            mock.Setup(foo => foo.GetDataContext()).Returns(dcmock.Object);

            Assert.True(mock.Object.HaveConnectivity());
        }

        [Fact]
        public void TestValidateSchemaOnValidSchemaNoConnectionString()
        {
            var ss = new SQLServerScripts();
            dbmgrDataMigration db = new dbmgrDataMigration(GetMockConfiguration(ss.DbConnectionKey).Object, ".", ss, null, null);
            Assert.Throws<ArgumentException>(() => db.ValidateSchema());
        }

        [Fact]
        public void TestValidateSchemaOnValidSchema()
        {
            SQLServerScripts ss = new SQLServerScripts();
            string expectedSQL = ss.GetCheckMigrationStructureSQL();
            string expectedSQL2 = ss.GetCheckCurrentStructureSQL();

            var mock = GetMockDataMigration(ss);
            var dcmock = new Mock<DataContext>(null, "Mock Connection String", null, 20, 20);
            dcmock.Setup(foo => foo.ExecuteScalar(It.Is<string>(i => i != expectedSQL && i != expectedSQL2), It.IsAny<List<IDbDataParameter>>())).Throws(new Exception());
            mock.Setup(foo => foo.GetDataContext()).Returns(dcmock.Object);

            Assert.True(mock.Object.ValidateSchema());
        }

        [Fact]
        public void TestCreateSchemaOnValidSchemaNoConnectionString()
        {
            var ss = new SQLServerScripts();
            dbmgrDataMigration db = new dbmgrDataMigration(GetMockConfiguration(ss.DbConnectionKey).Object, ".", ss, null, null);
            Assert.Throws<ArgumentException>(() => db.CreateSchema());
        }

        [Fact]
        public void TestCreateSchema()
        {
            SQLServerScripts ss = new SQLServerScripts();
            string expectedSQL = ss.GetCreateMigrationTrackingSQL();
            string expectedSQL2 = ss.GetCreateCurrentTrackingSQL();

            var mock = GetMockDataMigration(ss);
            var dcmock = new Mock<DataContext>(null, "Mock Connection String", null, 20, 20);
            dcmock.Setup(foo => foo.ExecuteNonQuery(It.Is<string>(i => i != expectedSQL && i != expectedSQL2), null, null)).Throws(new Exception());
            mock.Setup(foo => foo.GetDataContext()).Returns(dcmock.Object);

            Assert.True(mock.Object.CreateSchema());
        }

        [Fact]
        public void DeployDeltasRegressionTest()
        {
            SQLServerScripts ss = new SQLServerScripts();
            string expectedSQL = ss.GetInsertMigrationRecordSQL();

            var mock = GetMockDataMigration(ss);
            var dcmock = new Mock<DataContext>(null, "Mock Connection String", null, 20, 20);
            dcmock.Setup(foo => foo.ExecuteScalar(It.IsAny<string>(), It.IsAny<List<IDbDataParameter>>())).Returns(0);
            dcmock.Setup(foo => foo.ExecuteScript(It.Is<string>(i => !i.Contains("_test.up") && !i.Contains("_test1.up")), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).Throws(new Exception());
            dcmock.Setup(foo => foo.ExecuteNonQuery(It.Is<string>(i => i != expectedSQL), It.IsAny<List<IDbDataParameter>>(), null)).Throws(new Exception());
            mock.Setup(foo => foo.GetDataContext()).Returns(dcmock.Object);

            try
            {
                // Setup the test
                mock.Object.DeleteStandardDirectories();
                string location = mock.Object.CreateStandardDirectories();
                string basedeltas = Path.Combine(location, "Deltas");

                // Works if there are no deltas to deploy
                Assert.False(mock.Object.DeployDeltas());

                // Test basic functionality with two scripts
                mock.CallBase = true;
                string file1 = mock.Object.CreateScriptFile("test") + ".up";
                string file2 = mock.Object.CreateScriptFile("test1") + ".up";
                mock.CallBase = false;

                // Make sure dry run works
                mock.Object.NoDbUpdates = true;
                Assert.False(mock.Object.DeployDeltas());
                // Make sure non-dry run works
                mock.Object.NoDbUpdates = false;
                Assert.True(mock.Object.DeployDeltas());
                Assert.Equal(file1, mock.Object.ScriptHistory.First());
                Assert.Equal(file2, mock.Object.ScriptHistory.Last());

                // Test the sort order resolves properly if there are subdirectories
                DirectoryInfo sub2 = Directory.CreateDirectory(Path.Combine(basedeltas, "sub2"));
                File.Move(Path.Combine(basedeltas, file1), Path.Combine(sub2.FullName, file1));
                Assert.True(mock.Object.DeployDeltas());
                Assert.Equal(file1, mock.Object.ScriptHistory.First());
                Assert.Equal(file2, mock.Object.ScriptHistory.Last());

                // Test that a SQL error would fail the deltas
                mock.CallBase = true;
                mock.Object.CreateScriptFile("error_script");
                mock.CallBase = false;
                Assert.Throws<SystemException>(() => mock.Object.DeployDeltas());
            }
            finally
            {
                mock.Object.DeleteStandardDirectories();
            }
        }

        [Fact]
        public void DeployPostsRegressionTest()
        {
            SQLServerScripts ss = new SQLServerScripts();

            var mock = GetMockDataMigration(ss);
            var dcmock = new Mock<DataContext>(null, "Mock Connection String", null, 20, 20);
            dcmock.Setup(foo => foo.ExecuteScript(It.Is<string>(i => !i.EndsWith("test1.sql") && !i.EndsWith("test5.sql") && !i.EndsWith("test8.sql")), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).Throws(new Exception());
            mock.Setup(foo => foo.GetDataContext()).Returns(dcmock.Object);

            try
            {
                // Setup the test
                mock.Object.DeleteStandardDirectories();
                string location = mock.Object.CreateStandardDirectories();
                string basepost = Path.Combine(location, "Post");
                string file1 = Path.Combine(basepost, "test5.sql");
                string file2 = Path.Combine(basepost, "test8.sql");
                string file3 = Path.Combine(basepost, "test1.sql");
                string file4 = Path.Combine(basepost, "test9.sql");
                string file_e = Path.Combine(basepost, "error_script.sql");

                // Works if there are no post to deploy
                Assert.False(mock.Object.DeployPost());

                // Test basic functionality with two scripts
                File.WriteAllText(file1, "");
                File.WriteAllText(file2, "");

                // Make sure dry run works
                mock.Object.NoDbUpdates = true;
                Assert.False(mock.Object.DeployPost());
                // Make sure non-dry run works
                mock.Object.NoDbUpdates = false;
                Assert.True(mock.Object.DeployPost());
                Assert.Equal(file1, mock.Object.ScriptHistory.First());
                Assert.Equal(file2, mock.Object.ScriptHistory.Last());

                // Try a dependency
                mock.Object.ScriptHistory.Clear();
                File.WriteAllText(file3, "{{test8}}");
                Assert.True(mock.Object.DeployPost());
                Assert.Equal(file2, mock.Object.ScriptHistory.First());
                Assert.Equal(file1, mock.Object.ScriptHistory.Last());

                // Try a dependency we can't find
                File.WriteAllText(file4, "{{zoinks}}");
                Assert.Throws<NotSupportedException>(() => mock.Object.DeployPost());
                File.Delete(file4);

                // Test that a SQL error would fail the post
                File.WriteAllText(file_e, "");
                Assert.Throws<SystemException>(() => mock.Object.DeployPost());
            }
            finally
            {
                mock.Object.DeleteStandardDirectories();
            }
        }

        [Fact]
        public void DeployCurrentRegressionTest()
        {
            const string f1 = "vw_test5.sql";
            const string f2 = "fn_test8.sql";
            const string f3 = "fn_test1.sql";
            const string f4 = "tr_test9.sql";

            SQLServerScripts ss = new SQLServerScripts();

            var mock = GetMockDataMigration(ss);
            var dcmock = new Mock<DataContext>(null, "Mock Connection String", null, 20, 20);
            dcmock.Setup(foo => foo.ExecuteScript(It.Is<string>(i => !i.EndsWith(f3) && !i.EndsWith(f1) && !i.EndsWith(f2)), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).Throws(new Exception());
            mock.Setup(foo => foo.GetDataContext()).Returns(dcmock.Object);
            mock.Setup(foo => foo.GetExistingScriptInfo(dcmock.Object)).Returns(new Dictionary<string, Tuple<int, long>>());

            try
            {
                // Setup the test
                mock.Object.DeleteStandardDirectories();
                string location = mock.Object.CreateStandardDirectories();
                string basepost = Path.Combine(location, "Current");
                string file1 = Path.Combine(basepost, f1);
                string file2 = Path.Combine(basepost, f2);
                string file3 = Path.Combine(basepost, f3);
                string file4 = Path.Combine(basepost, f4);

                // Works if there are no current to deploy
                Assert.False(mock.Object.DeployCurrent());

                // Test basic functionality with two scripts
                File.WriteAllText(file1, "");
                File.WriteAllText(file2, "");

                // Make sure dry run works
                mock.Object.NoDbUpdates = true;
                Assert.False(mock.Object.DeployCurrent());
                // Make sure non-dry run works
                mock.Object.NoDbUpdates = false;
                Assert.True(mock.Object.DeployCurrent());
                Assert.Equal(file2, mock.Object.ScriptHistory.First());
                Assert.Equal(file1, mock.Object.ScriptHistory.Last());

                // Try a dependency
                mock.Object.ScriptHistory.Clear();
                File.WriteAllText(file3, "{{fn_test8}}");
                Assert.True(mock.Object.DeployCurrent());
                Assert.Equal(file2, mock.Object.ScriptHistory.First());
                Assert.Equal(file1, mock.Object.ScriptHistory.Last());

                // Test that a script file is ignored with a non-compliant name
                string file_i = Path.Combine(basepost, "error_script.sql");
                File.WriteAllText(file_i, "");
                Assert.True(mock.Object.DeployCurrent());
                Assert.Equal(file2, mock.Object.ScriptHistory.First());
                Assert.Equal(file1, mock.Object.ScriptHistory.Last());

                // Test that a script file is ignored with a non-compliant name
                string file_e = Path.Combine(basepost, "tr_error_script.sql");
                File.WriteAllText(file_e, "");
                Assert.Throws<SystemException>(() => mock.Object.DeployCurrent());

                // Try a circular dependency 
                File.Delete(file2);
                File.WriteAllText(file2, "{{fn_test1}}");
                Assert.Throws<NotSupportedException>(() => mock.Object.DeployCurrent());
                File.Delete(file2);

                // Try a dependency we can't find
                File.WriteAllText(file4, "{{zoinks}}");
                Assert.Throws<NotSupportedException>(() => mock.Object.DeployCurrent());
                File.Delete(file4);
            }
            finally
            {
                mock.Object.DeleteStandardDirectories();
            }
        }
    }
}