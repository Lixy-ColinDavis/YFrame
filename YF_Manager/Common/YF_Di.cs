using Microsoft.Extensions.DependencyInjection;

namespace YF_Manager
{
    /// <summary>
    /// 全局 DI 容器
    /// </summary>
    public static class YF_Di
    {
        /// <summary>DI 容器提供者（由 App.xaml.cs 初始化）</summary>
        public static IServiceProvider? Provider { get; set; }

        /// <summary>
        /// 从 DI 容器中解析指定类型的服务
        /// </summary>
        /// <typeparam name="T">要解析的服务类型</typeparam>
        /// <returns>服务实例</returns>
        public static T Get<T>() where T : notnull => Provider!.GetRequiredService<T>();
    }
}
