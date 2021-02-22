using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class NullNonQueryExecutor : INonQueryExecutor
    {
        public int Execute()
        {
            return 0;
        }

        public Task<int> ExecuteAsync()
        {
            return Task.FromResult(0);
        }

        public INonQueryExecutor WithCancellationToken(CancellationToken cancellationToken)
        {
            return this;
        }
    }
}
