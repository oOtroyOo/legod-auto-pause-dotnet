using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}

namespace LeigodPause
{
    /// <summary>
    /// 模块初始化器：在程序集加载的最早时机注册全局未处理异常日志，
    /// 连“静态字段初始化/类型加载”阶段抛出的异常也能捕获（此时 App 的构造函数尚未执行）。
    /// </summary>
    internal static class CrashLoggerInitializer
    {
        [ModuleInitializer]
        public static void Init()
        {
            System.AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
                App.LogFatal(ev.ExceptionObject as System.Exception, "AppDomain.UnhandledException");
        }
    }
}
