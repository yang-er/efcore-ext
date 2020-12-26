using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    public interface IFakeSubselectExpression
    {
        TableExpressionBase FakeTable { get; }

        void Update(TableExpressionBase real, SelectExpression fake, Dictionary<string, string> columnMapping);

        void AddUpsertField(bool insert, SqlExpression sqlExpression, string columnName);
    }
}
