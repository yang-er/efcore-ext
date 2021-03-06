namespace Microsoft.EntityFrameworkCore.Bulk
{
    /// <summary>
    /// An interface for annotate the original service and implementation.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    public interface IServiceAnnotation<TService, TImplementation>
        where TImplementation : TService
        where TService : class
    {
    }
}
