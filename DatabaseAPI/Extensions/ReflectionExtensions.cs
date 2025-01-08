using System;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;

namespace DatabaseAPI.Extensions;

public static class ReflectionExtensions
{
    private static volatile ConcurrentDictionary<Type, Tuple<MethodInfo, MethodInfo>[]> _properties = new ConcurrentDictionary<Type, Tuple<MethodInfo, MethodInfo>[]>();
    private static volatile ConcurrentDictionary<Type, FieldInfo[]> _fields = new ConcurrentDictionary<Type, FieldInfo[]>();

    public static void CopyFieldsAndProperties<T>(this T source, T target)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        if (target is null)
            throw new ArgumentNullException(nameof(target));

        if (!_properties.TryGetValue(typeof(T), out var props))
        {
            props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(x => new Tuple<MethodInfo, MethodInfo>(x.GetSetMethod(true), x.GetGetMethod(true))).ToArray();
            
            _properties.TryAdd(typeof(T), props);
        }

        if (!_fields.TryGetValue(typeof(T), out var fields))
        {
            fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            _fields.TryAdd(typeof(T), fields);
        }

        var buffer = new object[1];

        for (int i = 0; i < props.Length; i++)
        {
            var prop = props[i];

            var setter = prop.Item1;
            var getter = prop.Item2;
            
            if (setter is null || getter is null)
                continue;

            buffer[0] = getter.Invoke(source, null);
            setter.Invoke(target, buffer);
        }

        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            
            if (field.IsInitOnly)
                continue;

            field.SetValue(target, field.GetValue(source));
        }
    }
}