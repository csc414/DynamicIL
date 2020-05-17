using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DynamicIL
{
    public class DynamicProxyTypeUtils
    {
        const string ASSEMBLY_NAME = "DynamicProxy.Types";

        const MethodAttributes ExplicitMethodAttributes = MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;

        const MethodAttributes InterfaceMethodAttributes = MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;

        private ModuleBuilder _builder;

        public DynamicProxyTypeUtils()
        {
            var assemblyName = new AssemblyName(ASSEMBLY_NAME);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
            _builder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
        }

        public IEnumerable<Type> GetInterfaces(Type type, params Type[] exceptInterfaces)
        {
            var hashSet = new HashSet<Type>(exceptInterfaces);
            foreach (var interfaceType in type.GetTypeInfo().GetInterfaces().Distinct())
            {
                if (!interfaceType.GetTypeInfo().IsVisible())
                {
                    continue;
                }
                if (!hashSet.Contains(interfaceType))
                {
                    if (interfaceType.GetTypeInfo().ContainsGenericParameters && type.GetTypeInfo().ContainsGenericParameters)
                    {
                        if (!hashSet.Contains(interfaceType.GetGenericTypeDefinition()))
                            yield return interfaceType;
                    }
                    else
                    {
                        yield return interfaceType;
                    }
                }
            }
        }

        public Type CreateInterfaceProxyType(Type interfaceType, Type implementType)
        {
            var additionalInterfaces = GetInterfaces(implementType, interfaceType).ToArray();
            var interfaces = new Type[] { interfaceType }.Concat(additionalInterfaces).Distinct().ToArray();
            var typeBuilder = _builder.DefineType($"{ASSEMBLY_NAME}.{interfaceType.Name}Proxy", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(object), interfaces);

            GenericParameterUtils.DefineGenericParameter(interfaceType, typeBuilder);

            var _instance = typeBuilder.DefineField("_instance", interfaceType, FieldAttributes.Private);

            DefineInterfaceProxyConstructor();

            foreach (var method in interfaceType.GetTypeInfo().DeclaredMethods)
            {
                DefineMethod(method, method.Name, InterfaceMethodAttributes);
            }
            foreach (var additionalInterface in additionalInterfaces)
            {
                foreach (var method in additionalInterface.GetTypeInfo().DeclaredMethods)
                {
                    DefineMethod(method, $"{additionalInterface.Name}.{method.Name}", ExplicitMethodAttributes);
                }
            }

            return typeBuilder.CreateType();

            void DefineMethod(MethodInfo method, string name, MethodAttributes attributes)
            {
                var parameters = method.GetParameters();

                var methodBuilder = typeBuilder.DefineMethod(name, attributes, method.CallingConvention, method.ReturnType, parameters.Select(parame => parame.ParameterType).ToArray());

                GenericParameterUtils.DefineGenericParameter(method, methodBuilder);

                typeBuilder.DefineMethodOverride(methodBuilder, method);

                EmitMethodBody();

                void EmitMethodBody()
                {
                    var il = methodBuilder.GetILGenerator();
                    il.PushField(_instance);
                    for (int i = 1; i <= parameters.Length; i++)
                    {
                        il.PushArg(i);
                    }
                    il.Emit(OpCodes.Callvirt, method);
                    il.Ret();
                }
            }

            void DefineInterfaceProxyConstructor()
            {
                var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, MethodInfos.ObjectCtor.CallingConvention, new Type[] { interfaceType });

                var il = constructorBuilder.GetILGenerator();
                il.PushThis();
                il.Emit(OpCodes.Call, MethodInfos.ObjectCtor);

                il.PushThis();
                il.PushArg(1);
                il.Emit(OpCodes.Stfld, _instance);

                il.Emit(OpCodes.Ret);
            }
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
                methodIL.Emit(OpCodes.Call, MethodInfos.GetMethodFromHandle);
                methodIL.Emit(OpCodes.Castclass, typeof(MethodInfo));
                methodIL.PushLocal(argsLocal);
                methodIL.Emit(OpCodes.Callvirt, MethodInfos.DynamicProxyInvoke);
                if (isVoid)
                    methodIL.Pop();
                else
                    methodIL.UnBoxAny(method.ReturnType);
                methodIL.Emit(OpCodes.Ret);
            }
            return typeBuilder.CreateType();
        }

        private class GenericParameterUtils
        {
            internal static void DefineGenericParameter(Type targetType, TypeBuilder typeBuilder)
            {
                if (!targetType.GetTypeInfo().IsGenericTypeDefinition)
                {
                    return;
                }
                var genericArguments = targetType.GetTypeInfo().GetGenericArguments().Select(t => t.GetTypeInfo()).ToArray();
                var genericArgumentsBuilders = typeBuilder.DefineGenericParameters(genericArguments.Select(a => a.Name).ToArray());
                for (var index = 0; index < genericArguments.Length; index++)
                {
                    genericArgumentsBuilders[index].SetGenericParameterAttributes(ToClassGenericParameterAttributes(genericArguments[index].GenericParameterAttributes));
                    foreach (var constraint in genericArguments[index].GetGenericParameterConstraints().Select(t => t.GetTypeInfo()))
                    {
                        if (constraint.IsClass) genericArgumentsBuilders[index].SetBaseTypeConstraint(constraint.AsType());
                        if (constraint.IsInterface) genericArgumentsBuilders[index].SetInterfaceConstraints(constraint.AsType());
                    }
                }
            }

            internal static void DefineGenericParameter(MethodInfo tergetMethod, MethodBuilder methodBuilder)
            {
                if (!tergetMethod.IsGenericMethod)
                {
                    return;
                }
                var genericArguments = tergetMethod.GetGenericArguments().Select(t => t.GetTypeInfo()).ToArray();
                var genericArgumentsBuilders = methodBuilder.DefineGenericParameters(genericArguments.Select(a => a.Name).ToArray());
                for (var index = 0; index < genericArguments.Length; index++)
                {
                    genericArgumentsBuilders[index].SetGenericParameterAttributes(genericArguments[index].GenericParameterAttributes);
                    foreach (var constraint in genericArguments[index].GetGenericParameterConstraints().Select(t => t.GetTypeInfo()))
                    {
                        if (constraint.IsClass) genericArgumentsBuilders[index].SetBaseTypeConstraint(constraint.AsType());
                        if (constraint.IsInterface) genericArgumentsBuilders[index].SetInterfaceConstraints(constraint.AsType());
                    }
                }
            }

            private static GenericParameterAttributes ToClassGenericParameterAttributes(GenericParameterAttributes attributes)
            {
                if (attributes == GenericParameterAttributes.None)
                {
                    return GenericParameterAttributes.None;
                }
                if (attributes.HasFlag(GenericParameterAttributes.SpecialConstraintMask))
                {
                    return GenericParameterAttributes.SpecialConstraintMask;
                }
                if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                {
                    return GenericParameterAttributes.NotNullableValueTypeConstraint;
                }
                if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
                {
                    return GenericParameterAttributes.ReferenceTypeConstraint | GenericParameterAttributes.DefaultConstructorConstraint;
                }
                if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                {
                    return GenericParameterAttributes.ReferenceTypeConstraint;
                }
                if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
                {
                    return GenericParameterAttributes.DefaultConstructorConstraint;
                }
                return GenericParameterAttributes.None;
            }
        }
    }
}
