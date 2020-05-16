using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DynamicIL
{
    /// <summary>
    /// 动态代理基类
    /// </summary>
    public abstract class DynamicProxy
    {
        protected abstract object Invoke(object instance, MethodInfo targetMethod, object[] args);
    }
}
