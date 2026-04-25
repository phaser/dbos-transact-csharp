using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Internal;

/// <summary>
/// Computes a deterministic version hash for a set of registered workflow methods.
/// Port of Java AppVersionComputer.
///
/// Java hashes IL bytecodes (implementation-sensitive). C# hashes method signatures
/// (return type + declaring type + method name + parameter types), which is deterministic
/// and changes whenever any method signature changes.
/// </summary>
internal static class AppVersionComputer
{
    public static string ComputeAppVersion(string dbosVersion, IEnumerable<MethodInfo> workflowMethods)
    {
        using var sha = SHA256.Create();
        UpdateHash(sha, dbosVersion);

        var sorted = workflowMethods
            .Select(m => (Fqn: GetFullyQualifiedName(m), Method: m))
            .OrderBy(x => x.Fqn, StringComparer.Ordinal);

        foreach (var (fqn, method) in sorted)
        {
            UpdateHash(sha, fqn);
            UpdateHash(sha, GetMethodDescriptor(method));
        }

        sha.TransformFinalBlock([], 0, 0);
        return BytesToHex(sha.Hash!);
    }

    internal static string GetFullyQualifiedName(MethodInfo method)
    {
        var declaringType = method.DeclaringType!;
        var classNameAttr = declaringType.GetCustomAttribute<WorkflowClassNameAttribute>();
        var className = classNameAttr?.Value ?? declaringType.FullName ?? declaringType.Name;
        return $"{className}//{method.Name}";
    }

    private static string GetMethodDescriptor(MethodInfo method)
    {
        var paramTypes = string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
        var returnType = method.ReturnType.FullName ?? method.ReturnType.Name;
        return $"({paramTypes})->{returnType}";
    }

    private static void UpdateHash(HashAlgorithm sha, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
    }

    private static string BytesToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
