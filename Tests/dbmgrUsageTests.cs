using dbmgr.utilities;
using dbmgr.utilities.db;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Tests
{
    [TestClass]
    public class dbmgrUsageTests
    {
        [TestMethod]
        public void TestSetDbTypeToSQL()
        {
            string[] args = new string[] { "-d", "mssql" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
            Assert.IsInstanceOfType(dbmgrConsole.GetDatabaseType(), typeof(SQLServerScripts));
        }

        [TestMethod]
        public void TestSetDbTypeToOracle()
        {
            string[] args = new string[] { "-d", "oracle" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
            Assert.IsInstanceOfType(dbmgrConsole.GetDatabaseType(), typeof(OracleScripts));
        }

        [TestMethod]
        public void TestSetDbTypeToBadValue()
        {
            // Invalid database type specific, so return an error
            string[] args = new string[] { "-d", "abc" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [TestMethod]
        public void TestDoNotSetDbType()
        {
            string[] args = new string[] { };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
            Assert.IsInstanceOfType(dbmgrConsole.GetDatabaseType(), typeof(SQLServerScripts));
        }

        [TestMethod]
        public void TestGenerateNoParameter()
        {
            string[] args = new string[] { "-g" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [TestMethod]
        public void TestGenerateGoodParameter()
        {
            string[] args = new string[] { "-g", "create_admin_table" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_SUCCESS);
        }

        [TestMethod]
        public void TestCreateSchemaNoParameter()
        {
            string[] args = new string[] { "-c" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [TestMethod]
        public void TestCreateSchemaBadConnectionStringParameter()
        {
            string[] args = new string[] { "-c", "something" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
        }

        [TestMethod]
        public void TestCreateSchemaTooManyDelimitersConnectionStringParameter()
        {
            string[] args = new string[] { "-c", "@@@something" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
        }

        [TestMethod]
        public void TestGoodSchemaDelimitersConnectionStringParameter()
        {
            string[] args = new string[] { "-c", @"user:password@myServer\instanceName" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [TestMethod]
        public void TestGoodParsedVault()
        {
            string[] args = new string[] { "-t", "-f", @"TestContent/vault_parse.txt" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [TestMethod]
        public void TestGoodLoadedVault()
        {
            string[] args = new string[] { "-t", "-f", @"TestContent/vault_load.txt" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [TestMethod]
        public void TestGoodLoadedWAVault()
        {
            string[] args = new string[] { "-t", "-f", @"TestContent/vault_load_wa.txt" };
            int result = dbmgrConsole.Main(args);
            Assert.AreEqual(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

    }
}
