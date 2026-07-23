using Microsoft.Extensions.DependencyInjection;

namespace YF_Manager
{
    /// <summary>
    /// 全局 DI 容器持有者
    /// 在应用启动时由 YFrame 项目构建并设置 Provider
    /// 插件可通过 YF_Di.Get&lt;T&gt;() 解析服务
    /// 
    /// 设计原则：
    ///   YF_Manager 的 AOP 服务（YF_Messenger、YF_FileHelper、YF_TcpHelper）
    ///   同时提供静态 Instance 属性（向后兼容插件）和 DI 注册（供 YFrame 内部使用）
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
