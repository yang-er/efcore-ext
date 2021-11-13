using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    /// <summary>
    /// The extension of batch operations.
    /// </summary>
    public abstract class BatchOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo _info;

        /// <inheritdoc />
        public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

        /// <summary> The name of extension. </summary>
        public abstract string Name { get; }

        /// <inheritdoc cref="IDbContextOptionsExtension.ApplyServices(IServiceCollection)" />
        protected abstract void ApplyServices(BatchServicesBuilder services);

        /// <inheritdoc />
        public void ApplyServices(IServiceCollection services)
        {
            services.AddScoped<BulkQueryCompilationContextDependencies>();
            var builder = new BatchServicesBuilder(services, GetRequiredServices(), GetServiceLifetimes());
            ApplyServices(builder);
            builder.Validate();
        }

        /// <summary>
        /// Gets the service lifetime mapping.
        /// </summary>
        internal virtual Dictionary<Type, ServiceLifetime> GetServiceLifetimes()
        {
            return new Dictionary<Type, ServiceLifetime>
            {
#if EFCORE31 || EFCORE50
                { typeof(IBulkQueryableMethodTranslatingExpressionVisitorFactory), ServiceLifetime.Singleton },
                { typeof(IBulkQueryTranslationPostprocessorFactory), ServiceLifetime.Singleton },
                { typeof(IBulkQueryTranslationPreprocessorFactory), ServiceLifetime.Singleton },
                { typeof(IBulkShapedQueryCompilingExpressionVisitorFactory), ServiceLifetime.Singleton },
#elif EFCORE60
                { typeof(IBulkQueryableMethodTranslatingExpressionVisitorFactory), ServiceLifetime.Scoped },
                { typeof(IBulkQueryTranslationPostprocessorFactory), ServiceLifetime.Scoped },
                { typeof(IBulkQueryTranslationPreprocessorFactory), ServiceLifetime.Scoped },
                { typeof(IBulkShapedQueryCompilingExpressionVisitorFactory), ServiceLifetime.Scoped },
#endif
                { typeof(IBulkQueryCompilationContextFactory), ServiceLifetime.Scoped },
                { typeof(IQueryCompiler), ServiceLifetime.Scoped },
            };
        }

        /// <summary>
        /// Gets the services that required a <see cref="IServiceAnnotation{TService, TImplementation}"/>.
        /// </summary>
        internal virtual HashSet<Type> GetRequiredServices()
        {
            return new HashSet<Type>
            {
                typeof(IBulkQueryableMethodTranslatingExpressionVisitorFactory),
                typeof(IBulkQueryTranslationPostprocessorFactory),
                typeof(IBulkQueryTranslationPreprocessorFactory),
                typeof(IBulkShapedQueryCompilingExpressionVisitorFactory),
                typeof(IBulkQueryCompilationContextFactory),
                typeof(IQueryCompiler),
            };
        }

        /// <inheritdoc />
        public void Validate(IDbContextOptions options)
        {
        }

        /// <summary>
        /// Build the services.
        /// </summary>
        protected sealed class BatchServicesBuilder
        {
            private IReadOnlyDictionary<Type, ServiceLifetime> ServiceLifetimes { get; }

            private HashSet<Type> ShouldHaveAnnotation { get; }

            private HashSet<Type> NonHandledServices { get; }

            private IServiceCollection ServiceCollection { get; }

            internal BatchServicesBuilder(
                IServiceCollection services,
                HashSet<Type> unhandled,
                IReadOnlyDictionary<Type, ServiceLifetime> serviceLifetimes)
            {
                ServiceCollection = services;
                ShouldHaveAnnotation = unhandled;
                NonHandledServices = new HashSet<Type>(unhandled);
                ServiceLifetimes = serviceLifetimes;
            }

            internal void Validate()
            {
                if (NonHandledServices.Count > 0)
                {
                    throw new InvalidOperationException(
                        "The following services are not served yet: \r\n"
                        + string.Join("\r\n", NonHandledServices));
                }
            }

            /// <summary>
            /// Try add the service with implementation into EFCore's DIC.
            /// </summary>
            public void TryAdd<TService, TImplementation>()
                where TService : class
                where TImplementation : TService
            {
                var serviceType = typeof(TService);
                var implementationType = typeof(TImplementation);

                if (!ServiceLifetimes.TryGetValue(serviceType, out var lifetime))
                {
                    throw new InvalidOperationException("Unknown service type.");
                }

                var iface = implementationType.GetInterface("IServiceAnnotation`2");
                if (ShouldHaveAnnotation.Contains(serviceType)
                    && implementationType != typeof(BypassBulkQueryTranslationPostprocessorFactory)
                    && implementationType != typeof(BypassBulkShapedQueryCompilingExpressionVisitorFactory)
                    && iface == null)
                {
                    throw new InvalidOperationException("Require service annotation.");
                }

                bool useReplace = false;
                if (iface != null)
                {
                    var previousTypes = iface.GetGenericArguments();
                    useReplace = serviceType == previousTypes[0];
                    var sd = ServiceCollection.Single(s => s.ServiceType == previousTypes[0]);
                    if (sd.ImplementationType != previousTypes[1])
                    {
                        throw new InvalidOperationException(
                            "Seems a extension conflict occurred. " +
                            "Please contact the author.");
                    }
                }

                var descriptor = ServiceDescriptor.Describe(serviceType, implementationType, lifetime);
                NonHandledServices.Remove(serviceType);
                if (useReplace)
                {
                    ServiceCollection.Replace(descriptor);
                }
                else
                {
                    ServiceCollection.Add(descriptor);
                }
            }
        }

        private class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            private readonly string _name;

            public ExtensionInfo(
                BatchOptionsExtension extension)
                : base(extension)
            {
                _name = extension.Name;
                LogFragment = $"using {_name} ";
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment { get; }

#if EFCORE31 || EFCORE50
            public override long GetServiceProviderHashCode() => 0;
#elif EFCORE60
            public override int GetServiceProviderHashCode() => 0;

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;
#endif

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                => debugInfo[_name] = "1";
        }
    }
}
