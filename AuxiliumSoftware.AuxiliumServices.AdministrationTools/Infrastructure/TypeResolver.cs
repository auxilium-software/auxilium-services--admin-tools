using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Infrastructure
{
    internal sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
    {
        public object? Resolve(Type? type) =>
            type is null ? null : provider.GetRequiredService(type);

        public void Dispose()
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
