using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <summary>
    /// The query executor for non-query commands.
    /// </summary>
    public interface IBulkQueryExecutor
    {
        /// <summary>
        /// Executes the non-query command.
        /// </summary>
        /// <returns>The count of affected rows.</returns>
        int Execute();

        /// <summary>
        /// Attaches the <paramref name="cancellationToken"/> to current command.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="IBulkQueryExecutor"/> to chain the operations.</returns>
        IBulkQueryExecutor WithCancellationToken(CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously executes the non-query command.
        /// </summary>
        /// <returns>The task for executing this command, returning count of affected rows.</returns>
        Task<int> ExecuteAsync();
    }
}
