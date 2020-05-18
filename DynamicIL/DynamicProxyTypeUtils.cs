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

        public Type CreateInterfaceProxyType(Type interfaceType, Type implementType)
        {
            var additionalInterfaces = implementType.GetInterfaces(interfaceType).ToArray();
            var interfaces = new [] { typeof(IProxyInstance), interfaceType }.Concat(additionalInterfaces).Distinct().ToArray();
            var typeBuilder = _builder.DefineType($"{ASSEMBLY_NAME}.{interfaceType.Name}Proxy", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(object), interfaces.ToArray());

            DefineGenericParameter(interfaceType, typeBuilder);

            var _instance = typeBuilder.DefineField("_instance", interfaceType, FieldAttributes.Private);

            DefineInterfaceProxyConstructor();

            DefineProxyInstance();

            var tokens = new HashSet<int>();
            foreach (var property in interfaceType.GetTypeInfo().DeclaredProperties)
            {
                DefineProperty(property, property.Name, InterfaceMethodAttributes, tokens);
            }
            foreach (var additionalInterface in additionalInterfaces)
            {
                foreach (var property in additionalInterface.GetTypeInfo().DeclaredProperties)
                {
                    DefineProperty(property, $"{additionalInterface.Name}.{property.Name}", ExplicitMethodAttributes, tokens);
                }
            }

            foreach (var method in interfaceType.GetTypeInfo().DeclaredMethods)
            {
                if (tokens.Contains(method.MetadataToken))
                    continue;

                DefineMethod(method, method.Name, InterfaceMethodAttributes, builder => EmitMethodBody(builder, method));
            }
            foreach (var additionalInterface in additionalInterfaces)
            {
                foreach (var method in additionalInterface.GetTypeInfo().DeclaredMethods)
                {
                    if (tokens.Contains(method.MetadataToken))
                        continue;

                    DefineMethod(method, $"{additionalInterface.Name}.{method.Name}", ExplicitMethodAttributes, builder => EmitMethodBody(builder, method));
                }
            }

            return typeBuilder.CreateType();

            void DefineProxyInstance()
            {
                var method = typeof(IProxyInstance).GetTypeInfo().DeclaredMethods.First();
                DefineMethod(method, method.Name, ExplicitMethodAttributes, builder => 
                {
                    var il = builder.GetILGenerator();
                    il.PushField(_instance);
                    il.Ret();
                });
            }

            void DefineProperty(PropertyInfo propertyInfo, string name, MethodAttributes attributes, ICollection<int> tokens)
            {
                var propertyBuilder = typeBuilder.DefineProperty(name, propertyInfo.Attributes, propertyInfo.PropertyType, Type.EmptyTypes);

                if (propertyInfo.CanRead)
                {
                    var method = propertyInfo.GetMethod;
                    var getMethod = DefineMethod(method, method.Name, attributes, builder => EmitMethodBody(builder, method));
                    propertyBuilder.SetGetMethod(getMethod);
                    tokens.Add(method.MetadataToken);
                }
                if (propertyInfo.CanWrite)
                {
                    var method = propertyInfo.SetMethod;
                    var setMethod = DefineMethod(method, method.Name, attributes, builder => EmitMethodBody(builder, method));
                    propertyBuilder.SetGetMethod(setMethod);
                    tokens.Add(method.MetadataToken);
                }
            }

            MethodBuilder DefineMethod(MethodInfo method, string name, MethodAttributes attributes, Action<MethodBuilder> bodyBuilder)
            {
                var parameters = method.GetParameters();

                var methodBuilder = typeBuilder.DefineMethod(name, attributes, method.CallingConvention, method.ReturnType, parameters.Select(parame => parame.ParameterType).ToArray());

                DefineGenericParameter(method, methodBuilder);

                typeBuilder.DefineMethodOverride(methodBuilder, method);

                bodyBuilder.Invoke(methodBuilder);

                return methodBuilder;
            }

            void EmitMethodBody(MethodBuilder methodBuilder, MethodInfo method)
            {
                var parameters = method.GetParameters();
                var il = methodBuilder.GetILGenerator();
                il.PushField(_instance);
                for (int i = 1; i <= parameters.Length; i++)
                {
                    il.PushArg(i);
                }
                il.Emit(OpCodes.Callvirt, method);
                il.Ret();
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

        void DefineGenericParameter(Type targetType, TypeBuilder typeBuilder)
        {
            if (!targetType.GetTypeInfo().IsGenericTypeDefinition)
                return;

            var arguments = targetType.GetTypeInfo().GetGenericArguments().Select(t => t.GetTypeInfo()).ToArray();
            var argumentsBuilders = typeBuilder.DefineGenericParameters(arguments.Select(a => a.Name).ToArray());
            for (var index = 0; index < arguments.Length; index++)
            {
                argumentsBuilders[index].SetGenericParameterAttributes(ToClassGenericParameterAttributes(arguments[index].GenericParameterAttributes));
                foreach (var constraint in arguments[index].GetGenericParameterConstraints().Select(t => t.GetTypeInfo()))
                {
                    if (constraint.IsClass) 
                        argumentsBuilders[index].SetBaseTypeConstraint(constraint.AsType());
                    if (constraint.IsInterface) 
                        argumentsBuilders[index].SetInterfaceConstraints(constraint.AsType());
                }
            }

            GenericParameterAttributes ToClassGenericParameterAttributes(GenericParameterAttributes attributes)
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

        void DefineGenericParameter(MethodInfo tergetMethod, MethodBuilder methodBuilder)
        {
            if (!tergetMethod.IsGenericMethod)
                return;

            var arguments = tergetMethod.GetGenericArguments().Select(t => t.GetTypeInfo()).ToArray();
            var argumentsBuilders = methodBuilder.DefineGenericParameters(arguments.Select(a => a.Name).ToArray());
            for (var index = 0; index < arguments.Length; index++)
            {
                argumentsBuilders[index].SetGenericParameterAttributes(arguments[index].GenericParameterAttributes);
                foreach (var constraint in arguments[index].GetGenericParameterConstraints().Select(t => t.GetTypeInfo()))
                {
                    if (constraint.IsClass) 
                        argumentsBuilders[index].SetBaseTypeConstraint(constraint.AsType());
                    if (constraint.IsInterface) 
                        argumentsBuilders[index].SetInterfaceConstraints(constraint.AsType());
                }
            }
        }
    }
}
