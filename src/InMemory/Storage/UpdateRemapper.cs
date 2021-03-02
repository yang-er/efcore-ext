namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public class UpdateRemapper<TEntity>
    {
        public UpdateRemapper(TEntity origin, TEntity update)
        {
            Origin = origin;
            Update = update;
        }

        public TEntity Origin { get; }

        public TEntity Update { get; }
    }
}
