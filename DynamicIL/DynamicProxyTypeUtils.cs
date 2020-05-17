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
                DefineMethod(method, InterfaceMethodAttributes);
            }
            //foreach (var item in additionalInterfaces)
            //{
            //    foreach (var method in item.GetTypeInfo().DeclaredMethods.Where(x => !x.IsPropertyBinding()))
            //    {
            //        DefineExplicitMethod(method, targetType, typeDesc);
            //    }
            //}

            void DefineMethod(MethodInfo method, MethodAttributes attributes)
            {
                var parameters = method.GetParameters();

                var methodBuilder = typeBuilder.DefineMethod(method.Name, attributes, method.CallingConvention, method.ReturnType, parameters.Select(parame => parame.ParameterType).ToArray());

                GenericParameterUtils.DefineGenericParameter(method, methodBuilder);

                foreach (var customAttributeData in method.CustomAttributes)
                {
                    methodBuilder.SetCustomAttribute(CustomAttributeBuildeUtils.DefineCustomAttribute(customAttributeData));
                }



                typeBuilder.DefineMethodOverride(methodBuilder, method);
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

            return default;
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

        private class ParameterBuilderUtils
        {
            public static void DefineParameters(MethodInfo targetMethod, MethodBuilder methodBuilder)
            {
                var parameters = targetMethod.GetParameters();
                if (parameters.Length > 0)
                {
                    const int paramOffset = 1; // 1
                    foreach (var parameter in parameters)
                    {
                        var parameterBuilder = methodBuilder.DefineParameter(parameter.Position + paramOffset, parameter.Attributes, parameter.Name);
                        // if (parameter.HasDefaultValue) // parameter.HasDefaultValue will throw a FormatException when parameter is DateTime type with default value
                        if (parameter.HasDefaultValueByAttributes())
                        {
                            // if (!(parameter.ParameterType.GetTypeInfo().IsValueType && parameter.DefaultValue == null)) 
                            // we can comment above line safely, and CopyDefaultValueConstant will handle this case.
                            // parameter.DefaultValue will throw a FormatException when parameter is DateTime type with default value
                            {
                                // parameterBuilder.SetConstant(parameter.DefaultValue);
                                try
                                {
                                    CopyDefaultValueConstant(from: parameter, to: parameterBuilder);
                                }
                                catch
                                {
                                    // Default value replication is a nice-to-have feature but not essential,
                                    // so if it goes wrong for one parameter, just continue.
                                }
                            }
                        }
                        foreach (var attribute in parameter.CustomAttributes)
                        {
                            parameterBuilder.SetCustomAttribute(CustomAttributeBuildeUtils.DefineCustomAttribute(attribute));
                        }
                    }
                }

                var returnParamter = targetMethod.ReturnParameter;
                var returnParameterBuilder = methodBuilder.DefineParameter(0, returnParamter.Attributes, returnParamter.Name);
                foreach (var attribute in returnParamter.CustomAttributes)
                {
                    returnParameterBuilder.SetCustomAttribute(CustomAttributeBuildeUtils.DefineCustomAttribute(attribute));
                }
            }

            // Code from https://github.com/castleproject/Core/blob/master/src/Castle.Core/DynamicProxy/Generators/Emitters/MethodEmitter.cs
            private static void CopyDefaultValueConstant(ParameterInfo from, ParameterBuilder to)
            {
                object defaultValue;
                try
                {
                    defaultValue = from.DefaultValue;
                }
                catch (FormatException) when (from.ParameterType == typeof(DateTime))
                {
                    // This catch clause guards against a CLR bug that makes it impossible to query
                    // the default value of an optional DateTime parameter. For the CoreCLR, see
                    // https://github.com/dotnet/corefx/issues/26164.

                    // If this bug is present, it is caused by a `null` default value:
                    defaultValue = null;
                }
                catch (FormatException) when (from.ParameterType.GetTypeInfo().IsEnum)
                {
                    // This catch clause guards against a CLR bug that makes it impossible to query
                    // the default value of a (closed generic) enum parameter. For the CoreCLR, see
                    // https://github.com/dotnet/corefx/issues/29570.

                    // If this bug is present, it is caused by a `null` default value:
                    defaultValue = null;
                }

                if (defaultValue is Missing)
                {
                    // It is likely that we are reflecting over invalid metadata if we end up here.
                    // At this point, `to.Attributes` will have the `HasDefault` flag set. If we do
                    // not call `to.SetConstant`, that flag will be reset when creating the dynamic
                    // type, so `to` will at least end up having valid metadata. It is quite likely
                    // that the `Missing.Value` will still be reproduced because the `Parameter-
                    // Builder`'s `ParameterAttributes.Optional` is likely set. (If it isn't set,
                    // we'll be causing a default value of `DBNull.Value`, but there's nothing that
                    // can be done about that, short of recreating a new `ParameterBuilder`.)
                    return;
                }

                try
                {
                    to.SetConstant(defaultValue);
                }
                catch (ArgumentException)
                {
                    var parameterType = from.ParameterType;
                    var parameterNonNullableType = parameterType;
                    var isNullableType = parameterType.IsNullableType();

                    if (defaultValue == null)
                    {
                        if (isNullableType)
                        {
                            // This guards against a Mono bug that prohibits setting default value `null`
                            // for a `Nullable<T>` parameter. See https://github.com/mono/mono/issues/8504.
                            //
                            // If this bug is present, luckily we still get `null` as the default value if
                            // we do nothing more (which is probably itself yet another bug, as the CLR
                            // would "produce" a default value of `Missing.Value` in this situation).
                            return;
                        }
                        else if (parameterType.GetTypeInfo().IsValueType)
                        {
                            // This guards against a CLR bug that prohibits replicating `null` default
                            // values for non-nullable value types (which, despite the apparent type
                            // mismatch, is perfectly legal and something that the Roslyn compilers do).
                            // For the CoreCLR, see https://github.com/dotnet/corefx/issues/26184.

                            // If this bug is present, the best we can do is to not set the default value.
                            // This will cause a default value of `Missing.Value` (if `ParameterAttributes-
                            // .Optional` is set) or `DBNull.Value` (otherwise, unlikely).
                            return;
                        }
                    }
                    else if (isNullableType)
                    {
                        parameterNonNullableType = from.ParameterType.GetGenericArguments()[0];
                        if (parameterNonNullableType.GetTypeInfo().IsEnum || parameterNonNullableType.IsInstanceOfType(defaultValue))
                        {
                            // This guards against two bugs:
                            //
                            // * On the CLR and CoreCLR, a bug that makes it impossible to use `ParameterBuilder-
                            //   .SetConstant` on parameters of a nullable enum type. For CoreCLR, see
                            //   https://github.com/dotnet/coreclr/issues/17893.
                            //
                            //   If this bug is present, there is no way to faithfully reproduce the default
                            //   value. This will most likely cause a default value of `Missing.Value` or
                            //   `DBNull.Value`. (To better understand which of these, see comment above).
                            //
                            // * On Mono, a bug that performs a too-strict type check for nullable types. The
                            //   value passed to `ParameterBuilder.SetConstant` must have a type matching that
                            //   of the parameter precisely. See https://github.com/mono/mono/issues/8597.
                            //
                            //   If this bug is present, there's no way to reproduce the default value because
                            //   we cannot actually create a value of type `Nullable<>`.
                            return;
                        }
                    }

                    // Finally, we might have got here because the metadata constant simply doesn't match
                    // the parameter type exactly. Some code generators other than the .NET compilers
                    // might produce such metadata. Make a final attempt to coerce it to the required type:
                    try
                    {
                        var coercedDefaultValue = Convert.ChangeType(defaultValue, parameterNonNullableType, CultureInfo.InvariantCulture);
                        to.SetConstant(coercedDefaultValue);

                        return;
                    }
                    catch
                    {
                        // We don't care about the error thrown by an unsuccessful type coercion.
                    }

                    throw;
                }
            }

            internal static void DefineParameters(ConstructorInfo constructor, ConstructorBuilder constructorBuilder)
            {
                constructorBuilder.DefineParameter(1, ParameterAttributes.None, "aspectContextFactory");
                var parameters = constructor.GetParameters();
                if (parameters.Length > 0)
                {
                    var paramOffset = 2;    //ParameterTypes.Length - parameters.Length + 1
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var parameter = parameters[i];
                        var parameterBuilder = constructorBuilder.DefineParameter(i + paramOffset, parameter.Attributes, parameter.Name);
                        if (parameter.HasDefaultValue)
                        {
                            parameterBuilder.SetConstant(parameter.DefaultValue);
                        }
                        parameterBuilder.SetCustomAttribute(CustomAttributeBuildeUtils.DefineCustomAttribute(typeof(DynamicallyAttribute)));
                        foreach (var attribute in parameter.CustomAttributes)
                        {
                            parameterBuilder.SetCustomAttribute(CustomAttributeBuildeUtils.DefineCustomAttribute(attribute));
                        }
                    }
                }
            }
        }

        private class CustomAttributeBuildeUtils
        {
            public static CustomAttributeBuilder DefineCustomAttribute(Type attributeType)
            {
                return new CustomAttributeBuilder(attributeType.GetTypeInfo().GetConstructor(Type.EmptyTypes), new object[0]);
            }

            public static CustomAttributeBuilder DefineCustomAttribute(CustomAttributeData customAttributeData)
            {
                if (customAttributeData.NamedArguments != null)
                {
                    var attributeTypeInfo = customAttributeData.AttributeType.GetTypeInfo();
                    var constructor = customAttributeData.Constructor;
                    //var constructorArgs = customAttributeData.ConstructorArguments.Select(c => c.Value).ToArray();
                    var constructorArgs = customAttributeData.ConstructorArguments
                        .Select(ReadAttributeValue)
                        .ToArray();
                    var namedProperties = customAttributeData.NamedArguments
                            .Where(n => !n.IsField)
                            .Select(n => attributeTypeInfo.GetProperty(n.MemberName))
                            .ToArray();
                    var propertyValues = customAttributeData.NamedArguments
                             .Where(n => !n.IsField)
                             .Select(n => ReadAttributeValue(n.TypedValue))
                             .ToArray();
                    var namedFields = customAttributeData.NamedArguments.Where(n => n.IsField)
                             .Select(n => attributeTypeInfo.GetField(n.MemberName))
                             .ToArray();
                    var fieldValues = customAttributeData.NamedArguments.Where(n => n.IsField)
                             .Select(n => ReadAttributeValue(n.TypedValue))
                             .ToArray();
                    return new CustomAttributeBuilder(customAttributeData.Constructor, constructorArgs
                       , namedProperties
                       , propertyValues, namedFields, fieldValues);
                }
                else
                {
                    return new CustomAttributeBuilder(customAttributeData.Constructor,
                        customAttributeData.ConstructorArguments.Select(c => c.Value).ToArray());
                }
            }

            private static object ReadAttributeValue(CustomAttributeTypedArgument argument)
            {
                var value = argument.Value;
                if (argument.ArgumentType.GetTypeInfo().IsArray == false)
                {
                    return value;
                }
                //special case for handling arrays in attributes
                //the actual type of "value" is ReadOnlyCollection<CustomAttributeTypedArgument>.
                var arguments = ((IEnumerable<CustomAttributeTypedArgument>)value)
                    .Select(m => m.Value)
                    .ToArray();
                return arguments;
            }
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
