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
        /// Asynchronously executes the non-query command.
        /// </summary>
        /// <returns>The task for executing this command, returning count of affected rows.</returns>
        Task<int> ExecuteAsync();
    }
}
