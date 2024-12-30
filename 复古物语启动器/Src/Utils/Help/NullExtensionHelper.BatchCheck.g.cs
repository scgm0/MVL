#nullable enable

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace 复古物语启动器.Utils.Help;

public static partial class NullExceptionHelper
{
    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="arg9">需要进行检查的第9个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName9">由编译器自动填写的，在<paramref name="arg9"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    /// <typeparam name="T9">第9个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [NotNull] T9 arg9,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerArgumentExpression(nameof(arg9))] string? argName9 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
        arg9.NotNull(argName9, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="arg9">需要进行检查的第9个值</param>
    /// <param name="arg10">需要进行检查的第10个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName9">由编译器自动填写的，在<paramref name="arg9"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName10">由编译器自动填写的，在<paramref name="arg10"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    /// <typeparam name="T9">第9个值的类型</typeparam>
    /// <typeparam name="T10">第10个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [NotNull] T9 arg9,
        [NotNull] T10 arg10,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerArgumentExpression(nameof(arg9))] string? argName9 = null,
        [CallerArgumentExpression(nameof(arg10))] string? argName10 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
        arg9.NotNull(argName9, memberName);
        arg10.NotNull(argName10, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="arg9">需要进行检查的第9个值</param>
    /// <param name="arg10">需要进行检查的第10个值</param>
    /// <param name="arg11">需要进行检查的第11个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName9">由编译器自动填写的，在<paramref name="arg9"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName10">由编译器自动填写的，在<paramref name="arg10"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName11">由编译器自动填写的，在<paramref name="arg11"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    /// <typeparam name="T9">第9个值的类型</typeparam>
    /// <typeparam name="T10">第10个值的类型</typeparam>
    /// <typeparam name="T11">第11个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [NotNull] T9 arg9,
        [NotNull] T10 arg10,
        [NotNull] T11 arg11,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerArgumentExpression(nameof(arg9))] string? argName9 = null,
        [CallerArgumentExpression(nameof(arg10))] string? argName10 = null,
        [CallerArgumentExpression(nameof(arg11))] string? argName11 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
        arg9.NotNull(argName9, memberName);
        arg10.NotNull(argName10, memberName);
        arg11.NotNull(argName11, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="arg9">需要进行检查的第9个值</param>
    /// <param name="arg10">需要进行检查的第10个值</param>
    /// <param name="arg11">需要进行检查的第11个值</param>
    /// <param name="arg12">需要进行检查的第12个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName9">由编译器自动填写的，在<paramref name="arg9"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName10">由编译器自动填写的，在<paramref name="arg10"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName11">由编译器自动填写的，在<paramref name="arg11"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName12">由编译器自动填写的，在<paramref name="arg12"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    /// <typeparam name="T9">第9个值的类型</typeparam>
    /// <typeparam name="T10">第10个值的类型</typeparam>
    /// <typeparam name="T11">第11个值的类型</typeparam>
    /// <typeparam name="T12">第12个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [NotNull] T9 arg9,
        [NotNull] T10 arg10,
        [NotNull] T11 arg11,
        [NotNull] T12 arg12,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerArgumentExpression(nameof(arg9))] string? argName9 = null,
        [CallerArgumentExpression(nameof(arg10))] string? argName10 = null,
        [CallerArgumentExpression(nameof(arg11))] string? argName11 = null,
        [CallerArgumentExpression(nameof(arg12))] string? argName12 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
        arg9.NotNull(argName9, memberName);
        arg10.NotNull(argName10, memberName);
        arg11.NotNull(argName11, memberName);
        arg12.NotNull(argName12, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="arg9">需要进行检查的第9个值</param>
    /// <param name="arg10">需要进行检查的第10个值</param>
    /// <param name="arg11">需要进行检查的第11个值</param>
    /// <param name="arg12">需要进行检查的第12个值</param>
    /// <param name="arg13">需要进行检查的第13个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName9">由编译器自动填写的，在<paramref name="arg9"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName10">由编译器自动填写的，在<paramref name="arg10"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName11">由编译器自动填写的，在<paramref name="arg11"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName12">由编译器自动填写的，在<paramref name="arg12"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName13">由编译器自动填写的，在<paramref name="arg13"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    /// <typeparam name="T9">第9个值的类型</typeparam>
    /// <typeparam name="T10">第10个值的类型</typeparam>
    /// <typeparam name="T11">第11个值的类型</typeparam>
    /// <typeparam name="T12">第12个值的类型</typeparam>
    /// <typeparam name="T13">第13个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [NotNull] T9 arg9,
        [NotNull] T10 arg10,
        [NotNull] T11 arg11,
        [NotNull] T12 arg12,
        [NotNull] T13 arg13,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerArgumentExpression(nameof(arg9))] string? argName9 = null,
        [CallerArgumentExpression(nameof(arg10))] string? argName10 = null,
        [CallerArgumentExpression(nameof(arg11))] string? argName11 = null,
        [CallerArgumentExpression(nameof(arg12))] string? argName12 = null,
        [CallerArgumentExpression(nameof(arg13))] string? argName13 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
        arg9.NotNull(argName9, memberName);
        arg10.NotNull(argName10, memberName);
        arg11.NotNull(argName11, memberName);
        arg12.NotNull(argName12, memberName);
        arg13.NotNull(argName13, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="arg9">需要进行检查的第9个值</param>
    /// <param name="arg10">需要进行检查的第10个值</param>
    /// <param name="arg11">需要进行检查的第11个值</param>
    /// <param name="arg12">需要进行检查的第12个值</param>
    /// <param name="arg13">需要进行检查的第13个值</param>
    /// <param name="arg14">需要进行检查的第14个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName9">由编译器自动填写的，在<paramref name="arg9"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName10">由编译器自动填写的，在<paramref name="arg10"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName11">由编译器自动填写的，在<paramref name="arg11"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName12">由编译器自动填写的，在<paramref name="arg12"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName13">由编译器自动填写的，在<paramref name="arg13"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName14">由编译器自动填写的，在<paramref name="arg14"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    /// <typeparam name="T9">第9个值的类型</typeparam>
    /// <typeparam name="T10">第10个值的类型</typeparam>
    /// <typeparam name="T11">第11个值的类型</typeparam>
    /// <typeparam name="T12">第12个值的类型</typeparam>
    /// <typeparam name="T13">第13个值的类型</typeparam>
    /// <typeparam name="T14">第14个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [NotNull] T9 arg9,
        [NotNull] T10 arg10,
        [NotNull] T11 arg11,
        [NotNull] T12 arg12,
        [NotNull] T13 arg13,
        [NotNull] T14 arg14,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerArgumentExpression(nameof(arg9))] string? argName9 = null,
        [CallerArgumentExpression(nameof(arg10))] string? argName10 = null,
        [CallerArgumentExpression(nameof(arg11))] string? argName11 = null,
        [CallerArgumentExpression(nameof(arg12))] string? argName12 = null,
        [CallerArgumentExpression(nameof(arg13))] string? argName13 = null,
        [CallerArgumentExpression(nameof(arg14))] string? argName14 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
        arg9.NotNull(argName9, memberName);
        arg10.NotNull(argName10, memberName);
        arg11.NotNull(argName11, memberName);
        arg12.NotNull(argName12, memberName);
        arg13.NotNull(argName13, memberName);
        arg14.NotNull(argName14, memberName);
    }

    /// <summary>
    /// 对所有传入参数的值进行检查，如果为Null则引发异常
    /// </summary>
    /// <param name="arg1">需要进行检查的第1个值</param>
    /// <param name="arg2">需要进行检查的第2个值</param>
    /// <param name="arg3">需要进行检查的第3个值</param>
    /// <param name="arg4">需要进行检查的第4个值</param>
    /// <param name="arg5">需要进行检查的第5个值</param>
    /// <param name="arg6">需要进行检查的第6个值</param>
    /// <param name="arg7">需要进行检查的第7个值</param>
    /// <param name="arg8">需要进行检查的第8个值</param>
    /// <param name="arg9">需要进行检查的第9个值</param>
    /// <param name="arg10">需要进行检查的第10个值</param>
    /// <param name="arg11">需要进行检查的第11个值</param>
    /// <param name="arg12">需要进行检查的第12个值</param>
    /// <param name="arg13">需要进行检查的第13个值</param>
    /// <param name="arg14">需要进行检查的第14个值</param>
    /// <param name="arg15">需要进行检查的第15个值</param>
    /// <param name="argName1">由编译器自动填写的，在<paramref name="arg1"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName2">由编译器自动填写的，在<paramref name="arg2"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName3">由编译器自动填写的，在<paramref name="arg3"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName4">由编译器自动填写的，在<paramref name="arg4"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName5">由编译器自动填写的，在<paramref name="arg5"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName6">由编译器自动填写的，在<paramref name="arg6"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName7">由编译器自动填写的，在<paramref name="arg7"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName8">由编译器自动填写的，在<paramref name="arg8"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName9">由编译器自动填写的，在<paramref name="arg9"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName10">由编译器自动填写的，在<paramref name="arg10"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName11">由编译器自动填写的，在<paramref name="arg11"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName12">由编译器自动填写的，在<paramref name="arg12"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName13">由编译器自动填写的，在<paramref name="arg13"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName14">由编译器自动填写的，在<paramref name="arg14"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="argName15">由编译器自动填写的，在<paramref name="arg15"/>为Null时作为错误信息打印的调用方信息</param>
    /// <param name="memberName">由编译器自动填写的，在任一参数为Null时作为错误信息打印的调用方信息</param>
    /// <typeparam name="T1">第1个值的类型</typeparam>
    /// <typeparam name="T2">第2个值的类型</typeparam>
    /// <typeparam name="T3">第3个值的类型</typeparam>
    /// <typeparam name="T4">第4个值的类型</typeparam>
    /// <typeparam name="T5">第5个值的类型</typeparam>
    /// <typeparam name="T6">第6个值的类型</typeparam>
    /// <typeparam name="T7">第7个值的类型</typeparam>
    /// <typeparam name="T8">第8个值的类型</typeparam>
    /// <typeparam name="T9">第9个值的类型</typeparam>
    /// <typeparam name="T10">第10个值的类型</typeparam>
    /// <typeparam name="T11">第11个值的类型</typeparam>
    /// <typeparam name="T12">第12个值的类型</typeparam>
    /// <typeparam name="T13">第13个值的类型</typeparam>
    /// <typeparam name="T14">第14个值的类型</typeparam>
    /// <typeparam name="T15">第15个值的类型</typeparam>
    [StackTraceHidden, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotNull<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(
        [NotNull] T1 arg1,
        [NotNull] T2 arg2,
        [NotNull] T3 arg3,
        [NotNull] T4 arg4,
        [NotNull] T5 arg5,
        [NotNull] T6 arg6,
        [NotNull] T7 arg7,
        [NotNull] T8 arg8,
        [NotNull] T9 arg9,
        [NotNull] T10 arg10,
        [NotNull] T11 arg11,
        [NotNull] T12 arg12,
        [NotNull] T13 arg13,
        [NotNull] T14 arg14,
        [NotNull] T15 arg15,
        [CallerArgumentExpression(nameof(arg1))] string? argName1 = null,
        [CallerArgumentExpression(nameof(arg2))] string? argName2 = null,
        [CallerArgumentExpression(nameof(arg3))] string? argName3 = null,
        [CallerArgumentExpression(nameof(arg4))] string? argName4 = null,
        [CallerArgumentExpression(nameof(arg5))] string? argName5 = null,
        [CallerArgumentExpression(nameof(arg6))] string? argName6 = null,
        [CallerArgumentExpression(nameof(arg7))] string? argName7 = null,
        [CallerArgumentExpression(nameof(arg8))] string? argName8 = null,
        [CallerArgumentExpression(nameof(arg9))] string? argName9 = null,
        [CallerArgumentExpression(nameof(arg10))] string? argName10 = null,
        [CallerArgumentExpression(nameof(arg11))] string? argName11 = null,
        [CallerArgumentExpression(nameof(arg12))] string? argName12 = null,
        [CallerArgumentExpression(nameof(arg13))] string? argName13 = null,
        [CallerArgumentExpression(nameof(arg14))] string? argName14 = null,
        [CallerArgumentExpression(nameof(arg15))] string? argName15 = null,
        [CallerMemberName] string? memberName = null)
    {
        arg1.NotNull(argName1, memberName);
        arg2.NotNull(argName2, memberName);
        arg3.NotNull(argName3, memberName);
        arg4.NotNull(argName4, memberName);
        arg5.NotNull(argName5, memberName);
        arg6.NotNull(argName6, memberName);
        arg7.NotNull(argName7, memberName);
        arg8.NotNull(argName8, memberName);
        arg9.NotNull(argName9, memberName);
        arg10.NotNull(argName10, memberName);
        arg11.NotNull(argName11, memberName);
        arg12.NotNull(argName12, memberName);
        arg13.NotNull(argName13, memberName);
        arg14.NotNull(argName14, memberName);
        arg15.NotNull(argName15, memberName);
    }

}
