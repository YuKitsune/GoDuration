#if NETSTANDARD2_0

using System;

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;
        public bool ReturnValue { get; }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

#endif
