using System.Reflection;

namespace MyStreamHistory.TwitchTrackingService.Application.Tests;

/// <summary>Interface fake that answers only the methods a test names and throws on any other call.</summary>
public class Fake<T> : DispatchProxy where T : class
{
    private Dictionary<string, Func<object?[], object?>> _handlers = new();

    public static T Create(Dictionary<string, Func<object?[], object?>> handlers)
    {
        var proxy = DispatchProxy.Create<T, Fake<T>>();
        ((Fake<T>)(object)proxy)._handlers = handlers;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        _handlers.TryGetValue(targetMethod!.Name, out var handler)
            ? handler(args ?? [])
            : throw new InvalidOperationException($"Unexpected call to {typeof(T).Name}.{targetMethod.Name}");
}
