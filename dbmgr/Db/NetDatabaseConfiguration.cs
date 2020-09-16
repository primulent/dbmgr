using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace dbmgr.utilities.Db
{
    public class NetDatabaseConfiguration : IDatabaseConfiguration
    {
        public NetDatabaseConfiguration(string dbkey)
        {
            ConnectionString = ConfigurationManager.ConnectionStrings[dbkey].ConnectionString;
            ConnectionProviderName = ConfigurationManager.ConnectionStrings[dbkey].ProviderName;
            DefaultCommandTimeoutSecs = 30;
            if (Int32.TryParse(ConfigurationManager.AppSettings["DefaultCommandTimeoutSecs"], out int resultcmd))
            {
                DefaultCommandTimeoutSecs = resultcmd;
            }
            DefaultTransactionTimeoutMins = TransactionManager.MaximumTimeout.Minutes;
            if (Int32.TryParse(ConfigurationManager.AppSettings["DefaultTransactionTimeoutMins"], out int resulttrans))
            {
                DefaultTransactionTimeoutMins = resulttrans;
            }
        }
        public string ConnectionString { get; set; }
        public string ConnectionProviderName { get; set; }
        public int DefaultTransactionTimeoutMins { get; set; }
        public int DefaultCommandTimeoutSecs { get; set; }
    }
}
