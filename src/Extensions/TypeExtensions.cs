using System;
using System.Diagnostics.CodeAnalysis;

internal static class TypeExtensions
{
    internal static TType CreateInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TType>([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] this Type type, params object[] args)
    {
        return (TType)Activator.CreateInstance(type, args)!;
    }
}