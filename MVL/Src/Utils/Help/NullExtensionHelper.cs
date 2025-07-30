using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MVL.Utils.Help;

/// <summary>
/// 提供了对一个引用类型进行快速Null检查的扩展方法
/// </summary>
public static partial class NullExceptionHelper
{
    /// <summary>
    /// 对当前参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="value">要进行检查的参数</param>
    /// <param name="valueName">由编译器自动填写的，在<paramref name="value"/>为Null时作为错误信息打印的参数信息</param>
    /// <param name="memberName">由编译器自动填写的，在<paramref name="value"/>为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T">需要进行检查的，参数的类型</typeparam>
    /// <returns>用于链式调用的，不为Null的参数的值 </returns>
    /// <exception cref="ValueNullException">当<paramref name="value"/>为Null时引发，包含调用方信息以及<paramref name="value"/>的变量名称</exception>
    [StackTraceHidden] [DebuggerHidden] [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(
        [NotNull] this T? value,
        [CallerArgumentExpression("value")] string? valueName = null,
        [CallerMemberName] string? memberName = null)
    {
        if (value is not null) {
            return value;
        }

        throw new NullReferenceException($"[{memberName}] 类型为 {typeof(T).Name} 的参数 {valueName} 为 Null !");
    }
}