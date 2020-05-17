using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DynamicIL
{
    public static class ILGeneratorExtensions
    {
        /// <summary>
        /// 保存计算栈顶中的值到局部变量
        /// </summary>
        /// <param name="il"></param>
        /// <param name="type"></param>
        public static void StoreLocal(this ILGenerator il, LocalBuilder localBuilder)
        {
            il.Emit(OpCodes.Stloc, localBuilder);
        }

        /// <summary>
        /// 将已装箱的值类型拆箱后推送到计算栈
        /// </summary>
        /// <param name="il"></param>
        /// <param name="type"></param>
        public static void UnBox(this ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Unbox, type);
        }

        /// <summary>
        /// 将已装箱的值拆箱后推送到计算栈, 不管是值类型或引用类型
        /// </summary>
        /// <param name="il"></param>
        /// <param name="type"></param>
        public static void UnBoxAny(this ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Unbox_Any, type);
        }

        /// <summary>
        /// 将值类型装箱后推送到计算栈
        /// </summary>
        /// <param name="il"></param>
        /// <param name="type"></param>
        public static void Box(this ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Box, type);
        }

        /// <summary>
        /// 如果是值类型, 则将值类型装箱后推送到计算栈
        /// </summary>
        /// <param name="il"></param>
        public static void BoxIfNeed(this ILGenerator il, Type type)
        {
            if(type.IsValueType)
                il.Box(type);
        }

        /// <summary>
        /// 复制计算堆栈上当前最顶端的值，然后将复制的值推送到计算栈。
        /// </summary>
        /// <param name="il"></param>
        /// <param name="elementType"></param>
        /// <param name="length"></param>
        public static void CopyStackTop(this ILGenerator il)
        {
            il.Emit(OpCodes.Dup);
        }

        /// <summary>
        /// 移除当前计算堆栈顶的值。
        /// </summary>
        public static void Pop(this ILGenerator il)
        {
            il.Emit(OpCodes.Pop);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="il"></param>
        public static void Ret(this ILGenerator il)
        {
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// 创建一个固定长度的数组, 将数组引用push到计算栈
        /// </summary>
        /// <param name="il"></param>
        /// <param name="elementType"></param>
        /// <param name="length"></param>
        public static void NewArray(this ILGenerator il, Type elementType, int length)
        {
            il.PushInt(length);
            il.Emit(OpCodes.Newarr, elementType);
        }

        /// <summary>
        /// 推送当前对象引用到计算栈, 在可实例化类中 arg_0 是当前对象的指针
        /// </summary>
        /// <param name="il"></param>
        public static void PushThis(this ILGenerator il) => il.PushArg(0);

        /// <summary>
        /// 推送当前对象的指定字段到计算栈
        /// </summary>
        /// <param name="il"></param>
        /// <param name="fieldInfo"></param>
        public static void PushField(this ILGenerator il, FieldInfo fieldInfo)
        {
            il.PushThis();
            il.Emit(OpCodes.Ldfld, fieldInfo);
        }

        /// <summary>
        /// 推送指定局部变量到计算栈
        /// </summary>
        /// <param name="il"></param>
        /// <param name="localBuilder"></param>
        public static void PushLocal(this ILGenerator il, LocalBuilder localBuilder)
        {
            il.Emit(OpCodes.Ldloc, localBuilder);
        }

        /// <summary>
        /// 推送指定参数到计算栈
        /// </summary>
        /// <param name="il"></param>
        /// <param name="index"></param>
        public static void PushArg(this ILGenerator il, int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "index 必须大于等于 0");

            switch (index)
            {
                case 0:
                    il.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    if (index <= byte.MaxValue)
                    {
                        il.Emit(OpCodes.Ldarg_S, (byte)index);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldarg, index);
                    }
                    break;
            }
        }

        /// <summary>
        /// 推送整数到计算栈
        /// </summary>
        /// <param name="il"></param>
        /// <param name="value"></param>
        public static void PushInt(this ILGenerator il, int value)
        {
            OpCode c;
            switch (value)
            {
                case -1:
                    c = OpCodes.Ldc_I4_M1;
                    break;
                case 0:
                    c = OpCodes.Ldc_I4_0;
                    break;
                case 1:
                    c = OpCodes.Ldc_I4_1;
                    break;
                case 2:
                    c = OpCodes.Ldc_I4_2;
                    break;
                case 3:
                    c = OpCodes.Ldc_I4_3;
                    break;
                case 4:
                    c = OpCodes.Ldc_I4_4;
                    break;
                case 5:
                    c = OpCodes.Ldc_I4_5;
                    break;
                case 6:
                    c = OpCodes.Ldc_I4_6;
                    break;
                case 7:
                    c = OpCodes.Ldc_I4_7;
                    break;
                case 8:
                    c = OpCodes.Ldc_I4_8;
                    break;
                default:
                    if (value >= -128 && value <= 127)
                    {
                        il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldc_I4, value);
                    }
                    return;
            }
            il.Emit(c);
        }
    }
}
