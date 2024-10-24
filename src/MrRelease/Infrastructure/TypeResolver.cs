using Spectre.Console.Cli;

namespace MrRelease.Infrastructure;

public sealed class TypeResolver(IServiceProvider serviceProvider) : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    public object? Resolve(Type? type)
    {
        return type is null
            ? null
            : _serviceProvider.GetService(type);
    }
}
