using System.Reflection;
using System.Runtime.ExceptionServices;
using Castle.DynamicProxy;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Internal;

/// <summary>
/// Castle.DynamicProxy interceptor that routes <see cref="WorkflowAttribute"/> and
/// <see cref="StepAttribute"/> method calls to the DBOS executor delegate. All other
/// method calls are passed through via <c>invocation.Proceed()</c>.
/// Handles <c>void</c>, <c>Task</c>, <c>Task&lt;T&gt;</c>, and <c>ValueTask&lt;T&gt;</c> return types.
/// </summary>
public sealed class DbosInvocationInterceptor : IInterceptor
{
    /// <summary>Invoked for each intercepted workflow or step call. Returns a task resolving to the result (or null for void).</summary>
    public delegate Task<object?> InvocationHandler(
        object target,
        string? instanceName,
        MethodInfo method,
        object?[] args,
        CancellationToken ct);

    private readonly object _target;
    private readonly string? _instanceName;
    private readonly InvocationHandler _intercept;

    public DbosInvocationInterceptor(
        object target,
        string? instanceName,
        InvocationHandler intercept)
    {
        _target = target;
        _instanceName = instanceName;
        _intercept = intercept;
    }

    public void Intercept(IInvocation invocation)
    {
        // Attributes may live on the interface method (invocation.Method) or the concrete
        // method on the target. Check both; prefer the concrete method if it has the attribute,
        // otherwise fall back to the interface method so interface-only attributes are found.
        var paramTypes = invocation.Method.GetParameters().Select(p => p.ParameterType).ToArray();
        var concreteMethod = _target.GetType().GetMethod(invocation.Method.Name, paramTypes);

        bool concreteHasAttr = concreteMethod is not null &&
            (concreteMethod.IsDefined(typeof(WorkflowAttribute), inherit: true)
             || concreteMethod.IsDefined(typeof(StepAttribute), inherit: true));

        var method = concreteHasAttr ? concreteMethod! : invocation.Method;

        var isWorkflow = method.IsDefined(typeof(WorkflowAttribute), inherit: true);
        var isStep = method.IsDefined(typeof(StepAttribute), inherit: true);

        if (!isWorkflow && !isStep)
        {
            invocation.Proceed();
            return;
        }

        var ct = ExtractCancellationToken(method, invocation.Arguments);
        var resultTask = _intercept(_target, _instanceName, method, invocation.Arguments, ct);
        invocation.ReturnValue = WrapForReturnType(invocation.Method.ReturnType, resultTask);
    }

    private static CancellationToken ExtractCancellationToken(MethodInfo method, object[] args)
    {
        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length && i < args.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken))
                return (CancellationToken)(args[i] ?? CancellationToken.None);
        }
        return CancellationToken.None;
    }

    private static object? WrapForReturnType(Type returnType, Task<object?> task)
    {
        if (returnType == typeof(void))
        {
            try { task.GetAwaiter().GetResult(); }
            catch (Exception ex) { ExceptionDispatchInfo.Capture(ex).Throw(); }
            return null;
        }

        if (returnType == typeof(Task))
            return (Task)task;

        if (returnType == typeof(Task<object?>))
            return task;

        if (returnType.IsGenericType)
        {
            var def = returnType.GetGenericTypeDefinition();
            var tArg = returnType.GetGenericArguments()[0];

            if (def == typeof(Task<>))
                return CastToTypedTaskMethod.MakeGenericMethod(tArg).Invoke(null, [task]);

            if (def == typeof(ValueTask<>))
                return CastToValueTaskMethod.MakeGenericMethod(tArg).Invoke(null, [task]);
        }

        // Synchronous: block and re-throw with original stack preserved.
        try { return task.GetAwaiter().GetResult(); }
        catch (Exception ex) { ExceptionDispatchInfo.Capture(ex).Throw(); throw; }
    }

    private static readonly MethodInfo CastToTypedTaskMethod =
        typeof(DbosInvocationInterceptor)
            .GetMethod(nameof(CastToTypedTaskCore), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo CastToValueTaskMethod =
        typeof(DbosInvocationInterceptor)
            .GetMethod(nameof(CastToValueTaskCore), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static async Task<T> CastToTypedTaskCore<T>(Task<object?> task)
    {
        var result = await task.ConfigureAwait(false);
        return (T)result!;
    }

    private static async ValueTask<T> CastToValueTaskCore<T>(Task<object?> task)
    {
        var result = await task.ConfigureAwait(false);
        return (T)result!;
    }
}
