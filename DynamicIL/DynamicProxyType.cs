using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace DynamicIL
{
    public static class DynamicProxyType
    {
        private static readonly ConcurrentDictionary<Type, Type> _cacheTypes = new ConcurrentDictionary<Type, Type>();

        public static Type GetOrCreateInterfaceProxyType<TInterface, TDynamicProxy>() where TDynamicProxy : DynamicProxy
        {
            return default;
        }
    }
}
