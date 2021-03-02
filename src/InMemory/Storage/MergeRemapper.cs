namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public class MergeRemapper<TOuter, TInner>
    {
        public MergeRemapper(TOuter outer, TInner inner)
        {
            Outer = outer;
            Inner = inner;
        }

        public TOuter Outer { get; }

        public TInner Inner { get; }
    }
}
