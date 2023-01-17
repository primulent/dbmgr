using Microsoft.Extensions.Configuration;
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
        public NetDatabaseConfiguration(string dbkey, IConfigurationRoot configuration)
        {
            ConnectionString = configuration.GetConnectionString(dbkey);
            ConnectionProviderName = configuration.GetConnectionString(string.Concat(dbkey, "Provider"));
            DefaultCommandTimeoutSecs = 30;
            if (Int32.TryParse(configuration["App:DefaultCommandTimeoutSecs"], out int resultcmd))
            {
                DefaultCommandTimeoutSecs = resultcmd;
            }
            DefaultTransactionTimeoutMins = TransactionManager.MaximumTimeout.Minutes;
            if (Int32.TryParse(configuration["App:DefaultTransactionTimeoutMins"], out int resulttrans))
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
