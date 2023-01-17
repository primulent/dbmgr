using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dbmgr.utilities.Db
{
    public interface IDatabaseConfiguration
    {
        string ConnectionString { get; set; }
        string ConnectionProviderName { get; set; }
        int DefaultTransactionTimeoutMins { get; set; }
        int DefaultCommandTimeoutSecs { get; set; }
    }
}
