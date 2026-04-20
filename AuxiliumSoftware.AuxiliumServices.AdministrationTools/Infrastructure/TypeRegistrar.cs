using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumServices.AdminTools.Infrastructure
{
    internal sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
    {
        private ITypeResolver? _resolver;

        public ITypeResolver Build()
        {
            _resolver = new TypeResolver(services.BuildServiceProvider());
            return _resolver;
        }

        public void Register(Type service, Type implementation) =>
            services.AddSingleton(service, implementation);

        public void RegisterInstance(Type service, object implementation) =>
            services.AddSingleton(service, implementation);

        public void RegisterLazy(Type service, Func<object> factory) =>
            services.AddSingleton(service, _ => factory());
    }
}
