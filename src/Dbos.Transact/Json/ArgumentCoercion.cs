using System.Reflection;
using System.Text.Json;

namespace Dbos.Transact.Json;

/// <summary>
/// Coerces a raw <c>object?[]</c> argument array so each element matches the declared
/// parameter type of <paramref name="method"/>. Incompatible types are converted via a
/// STJ JSON round-trip — the same strategy Jackson uses in the Java runtime.
/// </summary>
public static class ArgumentCoercion
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static object?[] Coerce(object?[] args, MethodInfo method)
    {
        var parameters = method.GetParameters();
        if (args.Length != parameters.Length)
        {
            throw new ArgumentException(
                $"Expected {parameters.Length} argument(s) but got {args.Length} for method '{method.Name}'.");
        }

        var coerced = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is not { } arg)
            {
                coerced[i] = null;
                continue;
            }
            var targetType = parameters[i].ParameterType;
            if (targetType.IsInstanceOfType(arg))
            {
                coerced[i] = arg;
                continue;
            }

            try
            {
                var json = JsonSerializer.Serialize(arg, Options);
                coerced[i] = JsonSerializer.Deserialize(json, targetType, Options);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Cannot convert argument {i} from '{arg.GetType().Name}' to '{targetType.Name}' for method '{method.Name}': {ex.Message}",
                    ex);
            }
        }

        return coerced;
    }
}
