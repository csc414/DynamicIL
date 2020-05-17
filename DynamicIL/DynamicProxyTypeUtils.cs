using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DynamicIL
{
    public class DynamicProxyTypeUtils
    {
        const string ASSEMBLY_NAME = "DynamicProxy.Types";

        static MethodInfo GetMethodFromHandle = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] { typeof(RuntimeMethodHandle) });

        static MethodInfo GetTypeFromHandle = typeof(MethodBase).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeMethodHandle) });

        static MethodInfo DynamicProxyInvoke = typeof(DynamicProxy).GetTypeInfo().DeclaredMethods.FirstOrDefault(o => o.Name == "Invoke");

        private ModuleBuilder _builder;

        public DynamicProxyTypeUtils()
        {
            var assemblyName = new AssemblyName(ASSEMBLY_NAME);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
            _builder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
        }

        public Type CreateInterfaceProxyType<TInterface, TDynamicProxy>() where TDynamicProxy : DynamicProxy
        {
            var interfaceType = typeof(TInterface);
            var parentType = typeof(TDynamicProxy);
            var objType = typeof(object);
            var typeBuilder = _builder.DefineType($"{ASSEMBLY_NAME}.{interfaceType.Name}Proxy", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, parentType, new[] { interfaceType });
            var proxyObj = typeBuilder.DefineField("_proxyObj", objType, FieldAttributes.Private | FieldAttributes.InitOnly);
            var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard | CallingConventions.HasThis, new[] { objType });
            //创建构造函数
            var ctorIL = constructorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Stfld, proxyObj);
            ctorIL.Emit(OpCodes.Ret);
            //创建代理方法
            var methods = interfaceType.GetMethods();
            foreach (var method in methods)
            {
                var parameterTypes = method.GetParameters().Select(o => o.ParameterType).ToArray();
                var isVoid = method.ReturnType == typeof(void);
                var methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual, method.ReturnType, parameterTypes);

                //支持泛型
                if(method.IsGenericMethod)
                {
                    var genericArgs = method.GetGenericArguments().Select(t => t.GetTypeInfo()).ToArray();
                    var genericArgsBuilders = methodBuilder.DefineGenericParameters(genericArgs.Select(a => a.Name).ToArray());
                    for (int i = 0; i < genericArgs.Length; i++)
                    {
                        genericArgsBuilders[i].SetGenericParameterAttributes(genericArgs[i].GenericParameterAttributes);
                        foreach (var constraint in genericArgs[i].GetGenericParameterConstraints().Select(t => t.GetTypeInfo()))
                        {
                            if (constraint.IsClass)
                                genericArgsBuilders[i].SetBaseTypeConstraint(constraint.AsType());
                            if (constraint.IsInterface)
                                genericArgsBuilders[i].SetInterfaceConstraints(constraint.AsType());
                        }
                    }   
                }
                var methodIL = methodBuilder.GetILGenerator();
                var argsLocal = methodIL.DeclareLocal(typeof(object[]));
                methodIL.NewArray(typeof(object), parameterTypes.Length);
                for (int i = 0; i < parameterTypes.Length; i++)
                {
                    var parameterType = parameterTypes[i];
                    methodIL.CopyStackTop();
                    methodIL.PushInt(i);
                    methodIL.PushArg(i + 1);
                    methodIL.BoxIfNeed(parameterType);
                    methodIL.Emit(OpCodes.Stelem_Ref);
                }
                methodIL.StoreLocal(argsLocal);
                methodIL.PushThis();
                methodIL.PushField(proxyObj);
                methodIL.Emit(OpCodes.Ldtoken, method);
                methodIL.Emit(OpCodes.Call, GetMethodFromHandle);
                methodIL.Emit(OpCodes.Castclass, typeof(MethodInfo));
                methodIL.PushLocal(argsLocal);
                methodIL.Emit(OpCodes.Callvirt, DynamicProxyInvoke);
                if (isVoid)
                    methodIL.Pop();
                else
                    methodIL.UnBoxAny(method.ReturnType);
                methodIL.Emit(OpCodes.Ret);
            }
            return typeBuilder.CreateType();
        }
    }
}
