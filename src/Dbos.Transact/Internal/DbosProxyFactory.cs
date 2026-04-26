using Castle.DynamicProxy;

namespace Dbos.Transact.Internal;

/// <summary>
/// Creates Castle.DynamicProxy interface proxies that route <c>[Workflow]</c> and <c>[Step]</c>
/// method calls through <see cref="DbosInvocationInterceptor"/>.
/// </summary>
public sealed class DbosProxyFactory
{
    private static readonly ProxyGenerator Generator = new();

    /// <summary>
    /// Create a proxy for interface <typeparamref name="T"/> backed by <paramref name="target"/>.
    /// Every call to a <c>[Workflow]</c> or <c>[Step]</c> method is routed to
    /// <paramref name="intercept"/>; all other calls pass through to the target.
    /// </summary>
    public static T CreateProxy<T>(
        T target,
        string? instanceName,
        DbosInvocationInterceptor.InvocationHandler intercept)
        where T : class
    {
        if (!typeof(T).IsInterface)
            throw new ArgumentException($"{typeof(T).Name} must be an interface type.", nameof(target));

        var interceptor = new DbosInvocationInterceptor(target, instanceName, intercept);
        return Generator.CreateInterfaceProxyWithTarget(target, interceptor);
    }
}
