using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class NullBulkQueryExecutor : IBulkQueryExecutor
    {
        public int Execute()
        {
            return 0;
        }

        public Task<int> ExecuteAsync()
        {
            return Task.FromResult(0);
        }

        public IBulkQueryExecutor WithCancellationToken(CancellationToken cancellationToken)
        {
            return this;
        }
    }
}
