using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeakCraft.World;

/// <summary>Native combat-target members, resolved once without logging failed field probes.
/// Cache metadata only: each read/write still uses the current target and native property setter.
/// Called on Unity's main thread.</summary>
internal static class CastleMobTargetAccess
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private static readonly Dictionary<(Type type, string name), Member> members = new();
    private sealed class Member
    {
        internal FieldInfo? field;
        internal PropertyInfo? property;
    }

    private static Member Resolve(Type type, string name)
    {
        var key = (type, name);
        if (members.TryGetValue(key, out var member)) return member;
        member = new Member();
        for (Type? current = type; current != null; current = current.BaseType)
        {
            member.field ??= current.GetField(name, Flags);
            member.property ??= current.GetProperty(name, Flags);
            if (member.field != null && member.property != null) break;
        }
        members.Add(key, member);
        return member;
    }

    internal static object? Get(object target, string name)
    {
        var member = Resolve(target.GetType(), name);
        return member.field?.GetValue(target) ?? member.property?.GetValue(target, null);
    }

    internal static void Set(object target, string name, object value)
    {
        var member = Resolve(target.GetType(), name);
        if (member.field != null) member.field.SetValue(target, value);
        else member.property?.SetValue(target, value, null);
    }
}
