using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager
{
    /// <summary>
    /// 日志拦截器 - 记录方法调用信息
    /// </summary>
    public class LogInterceptor : IInterceptor
    {
        /// <summary>
        /// 拦截功能
        /// </summary>
        /// <param name="invocation">方法调用的上下文对象</param>
        /// <remarks>
        /// 被调用的方法
        /// 参数
        /// 参数的值
        /// 返回值是
        /// 目标对象
        /// </remarks>
        public void Intercept(IInvocation invocation)
        {
            // 获取方法上标记的LogAttribute特性，true表示包括继承的特性 (一个方法可能标记多个特性）
            var logAttr = invocation.Method.GetCustomAttributes(typeof(LogAttribute), true)
                .FirstOrDefault() as LogAttribute;

            if (logAttr == null)
            {
                // 执行原始方法
                invocation.Proceed();
                return;
            }

            // 初始化计时器和获取方法名
            var stopwatch = Stopwatch.StartNew();
            var methodName = $"{invocation.Method.DeclaringType?.Name}.{invocation.Method.Name}";

            // 记录开始
            YF_Manager_Main.logger.InterceptorsLog($"执行开始 | {logAttr.Message} | 函数位置：" + methodName, logAttr.Level.ToString());

            // 记录参数
            var parameters = invocation.Method.GetParameters();
            if (parameters.Length > 0)
            {
                var paramInfo = parameters.Zip(invocation.Arguments, (p, a) => $"{p.Name}={a ?? "null"}");
                YF_Manager_Main.logger.InterceptorsLog( $"执行记录 | {logAttr.Message} 参数: {string.Join(", ", paramInfo)} | 函数位置：" + methodName, logAttr.Level.ToString());
            }

            try
            {
                // 判断是否是异步方法
                bool isAsync = invocation.Method.ReturnType == typeof(Task) ||  // 无返回值的异步方法
                       (invocation.Method.ReturnType.IsGenericType && invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));  // 有返回值的异步方法

                if (isAsync)
                {
                    // 异步下执行原方法
                    invocation.Proceed();
                    invocation.ReturnValue = InterceptAsync((dynamic)invocation.ReturnValue,
                        invocation, stopwatch, logAttr, methodName);
                }
                else
                {
                    invocation.Proceed();
                    stopwatch.Stop();

                    YF_Manager_Main.logger.InterceptorsLog(
                        $"执行完成 | {logAttr.Message} 耗时: {stopwatch.ElapsedMilliseconds}ms | 返回值: {invocation.ReturnValue ?? "null"} | 函数位置：" +
                        methodName, logAttr.Level.ToString());
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                YF_Manager_Main.logger.InterceptorsLog(
                    $"执行失败 | {logAttr.Message} 耗时: {stopwatch.ElapsedMilliseconds}ms | 错误: {ex.Message} | 函数位置：" +
                    methodName, logAttr.Level.ToString());
                throw;
            }
        }

        /// <summary>
        /// 无返回值处理
        /// </summary>
        /// <param name="task">原始异步任务</param>
        /// <param name="invocation">被拦截方法的完整上下文信息</param>
        /// <param name="stopwatch">计时器</param>
        /// <param name="logAttr">日志特性</param>
        /// <param name="methodName">方法名称</param>
        /// <returns></returns>
        private async Task InterceptAsync(Task task, IInvocation invocation,
            Stopwatch stopwatch, LogAttribute logAttr, string methodName)
        {
            // 等待原方法执行完成
            await task.ConfigureAwait(false);
            stopwatch.Stop();

            YF_Manager_Main.logger.InterceptorsLog(
                $"执行完成 | {logAttr.Message} 耗时:(异步) {stopwatch.ElapsedMilliseconds} | 函数位置：" + 
                methodName, logAttr.Level.ToString());
        }

        /// <summary>
        /// 有返回值处理
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task">原始异步任务</param>
        /// <param name="invocation">被拦截方法的完整上下文信息</param>
        /// <param name="stopwatch">计时器</param>
        /// <param name="logAttr">日志特性</param>
        /// <param name="methodName">方法名称</param>
        /// <returns></returns>
        private async Task<T> InterceptAsync<T>(Task<T> task, IInvocation invocation,
            Stopwatch stopwatch, LogAttribute logAttr, string methodName)
        {
            // 等待原方法执行完成
            var result = await task.ConfigureAwait(false);
            stopwatch.Stop();

            YF_Manager_Main.logger.InterceptorsLog(
                $"执行完成 | {logAttr.Message} 耗时:(异步) {stopwatch.ElapsedMilliseconds}ms | 返回值: {result} | 函数位置：" + 
                methodName, logAttr.Level.ToString());

            return result;
        }
    }
}
