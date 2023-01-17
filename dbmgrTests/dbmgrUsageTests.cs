using dbmgr.utilities;

namespace dbmgr.utilities.Tests
{
    public class dbmgrUsageTests
    {
        [Fact]
        public void TestSetDbTypeToSQL()
        {
            string[] args = new string[] { "-d", "mssql" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
            Assert.IsType<dbmgr.utilities.db.SQLServerScripts>(dbmgrConsole.GetDatabaseType());
        }

        [Fact]
        public void TestSetDbTypeToOracle()
        {
            string[] args = new string[] { "-d", "oracle" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
            Assert.IsType<dbmgr.utilities.db.OracleScripts>(dbmgrConsole.GetDatabaseType());
        }

        [Fact]
        public void TestSetDbTypeToBadValue()
        {
            // Invalid database type specific, so return an error
            string[] args = new string[] { "-d", "abc" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [Fact]
        public void TestDoNotSetDbType()
        {
            string[] args = new string[] { };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
            Assert.IsType<dbmgr.utilities.db.SQLServerScripts>(dbmgrConsole.GetDatabaseType());
        }

        [Fact]
        public void TestGenerateGoodParameter()
        {
            string[] args = new string[] { "-g", "create_admin_table" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_SUCCESS);
        }

        [Fact]
        public void TestCreateSchemaNoParameter()
        {
            string[] args = new string[] { "-c" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [Fact]
        public void TestCreateSchemaBadConnectionStringParameter()
        {
            string[] args = new string[] { "--ci", "something" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
        }

        [Fact]
        public void TestCreateSchemaTooManyDelimitersConnectionStringParameter()
        {
            string[] args = new string[] { "--ci", "@@@something" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
        }

        [Fact]
        public void TestGoodSchemaDelimitersConnectionStringParameter()
        {
            string[] args = new string[] { "--ci", @"user:password@myServer\instanceName" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_ARGUMENT_ERROR);
        }

        [Fact]
        public void TestGoodParsedVault()
        {
            string[] args = new string[] { "-t", "-f", @"TestContent/vault_parse.txt" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [Fact]
        public void TestGoodLoadedVault()
        {
            string[] args = new string[] { "-t", "-f", @"TestContent/vault_load.txt" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }

        [Fact]
        public void TestGoodLoadedWAVault()
        {
            string[] args = new string[] { "-t", "-f", @"TestContent/vault_load_wa.txt" };
            int result = dbmgrConsole.Main(args);
            Assert.Equal(result, dbmgrConsole.EXIT_CODE_GENERAL_ERROR);
        }
    }
}
