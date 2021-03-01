using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
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

        /// <summary> Whether the current database provider is relational. </summary>
        public abstract bool Relational { get; }

        /// <inheritdoc cref="IDbContextOptionsExtension.ApplyServices(IServiceCollection)" />
        internal abstract void ApplyServices(BatchServicesBuilder services);

        /// <inheritdoc />
        public void ApplyServices(IServiceCollection services)
        {
            var builder = new BatchServicesBuilder(services, Relational);
            ApplyServices(builder);
            builder.Validate();
        }

        /// <inheritdoc />
        public void Validate(IDbContextOptions options)
        {
        }

        internal sealed class BatchServicesBuilder
        {
            private const string IRelationalParameterBasedSqlProcessorFactory = nameof(IRelationalParameterBasedSqlProcessorFactory);
            private const string IMethodCallTranslatorPlugin = nameof(IMethodCallTranslatorPlugin);
            private const string IMemberTranslatorPlugin = nameof(IMemberTranslatorPlugin);
            private const string IQueryCompiler = nameof(IQueryCompiler);
            private const string IQuerySqlGeneratorFactory = nameof(IQuerySqlGeneratorFactory);

            private static readonly Dictionary<string, ServiceLifetime> ServiceLifetimes
                = new Dictionary<string, ServiceLifetime>
                {
                    { nameof(IBatchOperationProvider), ServiceLifetime.Singleton },
                    { nameof(IBulkQueryableMethodTranslatingExpressionVisitorFactory), ServiceLifetime.Singleton },
                    { nameof(IBulkQueryTranslationPostprocessorFactory), ServiceLifetime.Singleton },
                    { nameof(IBulkQueryTranslationPreprocessorFactory), ServiceLifetime.Singleton },
                    { nameof(IBulkShapedQueryCompilingExpressionVisitorFactory), ServiceLifetime.Singleton },
                    { IRelationalParameterBasedSqlProcessorFactory, ServiceLifetime.Singleton },
                    { IQuerySqlGeneratorFactory, ServiceLifetime.Singleton },
                    { IMethodCallTranslatorPlugin, ServiceLifetime.Singleton },
                    { IMemberTranslatorPlugin, ServiceLifetime.Singleton },
                    { nameof(IBulkQueryCompilationContextFactory), ServiceLifetime.Scoped },
                    { IQueryCompiler, ServiceLifetime.Scoped },
                };

            private static readonly HashSet<string> ShouldHaveAnnotation
                = new HashSet<string>
                {
                    nameof(IBulkQueryableMethodTranslatingExpressionVisitorFactory),
                    nameof(IBulkQueryCompilationContextFactory),
                    nameof(IBulkQueryTranslationPostprocessorFactory),
                    nameof(IBulkQueryTranslationPreprocessorFactory),
                    nameof(IBulkShapedQueryCompilingExpressionVisitorFactory),
                    IQueryCompiler,
                    IQuerySqlGeneratorFactory,
#if EFCORE50
                    IRelationalParameterBasedSqlProcessorFactory,
#endif
                };

            private HashSet<string> NonHandledServices { get; }

            private IServiceCollection ServiceCollection { get; }

            public BatchServicesBuilder(IServiceCollection services, bool relational)
            {
                ServiceCollection = services;
                NonHandledServices = new HashSet<string>(ShouldHaveAnnotation);

                if (!relational)
                {
                    NonHandledServices.Remove(IQuerySqlGeneratorFactory);
                    NonHandledServices.Remove(IRelationalParameterBasedSqlProcessorFactory);
                }
            }

            public void Validate()
            {
                if (NonHandledServices.Count > 0)
                {
                    throw new InvalidOperationException(
                        "The following services are not served yet: \r\n"
                        + string.Join("\r\n", NonHandledServices));
                }
            }

            public void TryAdd<TService, TImplementation>()
                where TService : class
                where TImplementation : TService
            {
                var serviceType = typeof(TService);
                var implementationType = typeof(TImplementation);

                if (!ServiceLifetimes.TryGetValue(serviceType.Name, out var lifetime))
                {
                    throw new InvalidOperationException("Unknown service type.");
                }

                var iface = implementationType.GetInterface("IServiceAnnotation`2");
                if (ShouldHaveAnnotation.Contains(serviceType.Name)
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
                NonHandledServices.Remove(serviceType.Name);
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

            public override long GetServiceProviderHashCode() => 0;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                => debugInfo[_name] = "1";
        }
    }
}
