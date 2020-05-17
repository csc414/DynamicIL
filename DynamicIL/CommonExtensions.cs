using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DynamicIL
{
    internal static class CommonExtensions
    {
        internal static bool HasDefaultValueByAttributes(this ParameterInfo parameter)
        {
            // parameter.HasDefaultValue will throw a FormatException when parameter is DateTime type with default value
            return (parameter.Attributes & ParameterAttributes.HasDefault) != 0;
        }

        internal static bool IsNullableType(this Type type)
        {
            return type.GetTypeInfo().IsGenericType &&
                   type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        internal static bool IsVisible(this TypeInfo typeInfo)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            if (typeInfo.IsNested)
            {
                if (!typeInfo.DeclaringType.GetTypeInfo().IsVisible())
                {
                    return false;
                }
                if (!typeInfo.IsVisible || !typeInfo.IsNestedPublic)
                {
                    return false;
                }
            }
            else
            {
                if (!typeInfo.IsVisible || !typeInfo.IsPublic)
                {
                    return false;
                }
            }
            if (typeInfo.IsGenericType && !typeInfo.IsGenericTypeDefinition)
            {
                foreach (var argument in typeInfo.GenericTypeArguments)
                {
                    if (!argument.GetTypeInfo().IsVisible())
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
