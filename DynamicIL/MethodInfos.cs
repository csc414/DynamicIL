using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DynamicIL
{
    internal static class MethodInfos
    {
        internal static readonly ConstructorInfo ObjectCtor = typeof(object).GetConstructor(Type.EmptyTypes);

        internal static MethodInfo GetMethodFromHandle = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) });

        internal static MethodInfo GetTypeFromHandle = typeof(MethodBase).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeMethodHandle) });

        internal static MethodInfo DynamicProxyInvoke = typeof(DynamicProxy).GetTypeInfo().DeclaredMethods.FirstOrDefault(o => o.Name == "Invoke");
    }
}
