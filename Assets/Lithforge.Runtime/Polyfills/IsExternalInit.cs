// Polyfill required for C# 9 record types and init-only setters in Unity.
// Unity's runtime does not ship System.Runtime.CompilerServices.IsExternalInit.

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}