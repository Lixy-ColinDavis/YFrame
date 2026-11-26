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
    /// 日志拦截器 - 记录方法调用信息。
    /// AOP 只负责观测，不改变业务语义：业务异常记录后必须原样抛出。
    /// </summary>
    public class LogInterceptor : IInterceptor
    {
        /// <summary>
        /// 拦截方法调用，记录开始、参数、完成或失败信息。
        /// </summary>
        /// <param name="invocation">方法调用的上下文对象</param>
        /// <remarks>
        /// 同步方法异常会被记录后重新抛出；异步方法在返回的 Task 中记录失败并重新抛出。
        /// 日志写入本身失败时只写调试输出，不能覆盖原始业务异常。
        /// </remarks>
        public void Intercept(IInvocation invocation)
        {
            // 获取方法上标记的 LogAttribute 特性；没有标记时直接放行
            var logAttr = invocation.Method.GetCustomAttributes(typeof(LogAttribute), true)
                .FirstOrDefault() as LogAttribute;

            if (logAttr == null)
            {
                invocation.Proceed();
                return;
            }

            // 初始化计时器和获取方法名
            var stopwatch = Stopwatch.StartNew();
            var methodName = $"{invocation.Method.DeclaringType?.FullName}.{invocation.Method.Name}";

            // 使用 YF_Manager_Main 的静态 logger（由静态构造函数保证初始化）
            var logger = YF_Manager_Main.logger;
            if (logger == null)
            {
                // 极端情况下的 fallback：直接执行原方法，不记录日志
                invocation.Proceed();
                return;
            }

            // 记录开始；日志失败不能影响业务调用
            TryLog(logger, logAttr,
                $"执行开始 | {logAttr.Message} | 函数位置：" + methodName);

            // 记录参数；ToString 异常时使用占位文本，避免日志代码改变业务行为
            var parameters = invocation.Method.GetParameters();
            if (parameters.Length > 0)
            {
                var paramInfo = parameters.Zip(invocation.Arguments,
                    (p, a) => $"{p.Name}={FormatValue(a)}");
                TryLog(logger, logAttr,
                    $"执行记录 | {logAttr.Message} 参数: {string.Join(", ", paramInfo)} | 函数位置：" + methodName);
            }

            try
            {
                // 判断是否是异步方法：只识别 Task / Task<T>，ValueTask 需另加分支
                bool isAsync = invocation.Method.ReturnType == typeof(Task) ||
                       (invocation.Method.ReturnType.IsGenericType &&
                        invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

                if (isAsync)
                {
                    // 先执行原方法；同步抛出的异常由外层 catch 记录并重新抛出
                    invocation.Proceed();

                    // 再把原始 Task 包装成带日志的 Task；异步异常由 InterceptAsync 记录并重新抛出
                    invocation.ReturnValue = InterceptAsync((dynamic)invocation.ReturnValue,
                        invocation, stopwatch, logAttr, methodName, logger);
                }
                else
                {
                    invocation.Proceed();
                    stopwatch.Stop();

                    TryLog(logger, logAttr,
                        $"执行完成 | {logAttr.Message} 耗时: {stopwatch.ElapsedMilliseconds}ms | " +
                        $"返回值: {FormatValue(invocation.ReturnValue)} | 函数位置：" + methodName);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                TryLog(logger, logAttr,
                    $"执行失败 | {logAttr.Message} 耗时: {stopwatch.ElapsedMilliseconds}ms | " +
                    $"错误: {ex.GetType().Name}: {ex.Message} | 函数位置：" + methodName,
                    ex);

                // AOP 不能吞掉业务异常，使用 throw 保留原始堆栈
                throw;
            }
        }

        /// <summary>
        /// 处理无返回值的异步任务，记录完成或失败。
        /// </summary>
        /// <param name="task">原始异步任务</param>
        /// <param name="invocation">被拦截方法的完整上下文信息（保留参数以兼容现有调用）</param>
        /// <param name="stopwatch">计时器</param>
        /// <param name="logAttr">日志特性</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="logger">日志记录器</param>
        /// <returns>包装后的异步任务</returns>
        private async Task InterceptAsync(Task task, IInvocation invocation,
            Stopwatch stopwatch, LogAttribute logAttr, string methodName,
            YF_Manager_Log logger)
        {
            try
            {
                // 等待原方法执行完成；异常会进入 catch，而不是静默留在 Task 中
                await task.ConfigureAwait(false);
                stopwatch.Stop();

                TryLog(logger, logAttr,
                    $"执行完成 | {logAttr.Message} 耗时:(异步) {stopwatch.ElapsedMilliseconds}ms | " +
                    $"函数位置：" + methodName);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                TryLog(logger, logAttr,
                    $"执行失败 | {logAttr.Message} 耗时:(异步) {stopwatch.ElapsedMilliseconds}ms | " +
                    $"错误: {ex.GetType().Name}: {ex.Message} | 函数位置：" + methodName,
                    ex);

                // 保留原始异步异常及堆栈
                throw;
            }
        }

        /// <summary>
        /// 处理有返回值的异步任务，记录完成或失败。
        /// </summary>
        /// <typeparam name="T">异步返回值类型</typeparam>
        /// <param name="task">原始异步任务</param>
        /// <param name="invocation">被拦截方法的完整上下文信息（保留参数以兼容现有调用）</param>
        /// <param name="stopwatch">计时器</param>
        /// <param name="logAttr">日志特性</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="logger">日志记录器</param>
        /// <returns>原始异步返回值</returns>
        private async Task<T> InterceptAsync<T>(Task<T> task, IInvocation invocation,
            Stopwatch stopwatch, LogAttribute logAttr, string methodName,
            YF_Manager_Log logger)
        {
            try
            {
                // 等待原方法执行完成；异常会进入 catch，而不是静默留在 Task 中
                var result = await task.ConfigureAwait(false);
                stopwatch.Stop();

                TryLog(logger, logAttr,
                    $"执行完成 | {logAttr.Message} 耗时:(异步) {stopwatch.ElapsedMilliseconds}ms | " +
                    $"返回值: {FormatValue(result)} | 函数位置：" + methodName);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                TryLog(logger, logAttr,
                    $"执行失败 | {logAttr.Message} 耗时:(异步) {stopwatch.ElapsedMilliseconds}ms | " +
                    $"错误: {ex.GetType().Name}: {ex.Message} | 函数位置：" + methodName,
                    ex);

                // 保留原始异步异常及堆栈
                throw;
            }
        }

        /// <summary>
        /// 安全写拦截日志；日志系统自身异常不能覆盖业务异常。
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="logAttr">日志特性</param>
        /// <param name="message">日志内容</param>
        /// <param name="exception">业务异常；成功日志为空</param>
        private static void TryLog(YF_Manager_Log logger, LogAttribute logAttr, string message,
            Exception? exception = null)
        {
            try
            {
                logger.InterceptorsLog(message, logAttr.Level.ToString(), exception);
            }
            catch (Exception logException)
            {
                // 日志写入失败只输出调试信息，不改变业务调用结果
                Debug.WriteLine($"[LogInterceptor] 写入拦截日志失败: {logException.Message}");
            }
        }

        /// <summary>
        /// 安全格式化参数或返回值，避免 ToString 异常影响业务。
        /// </summary>
        /// <param name="value">待格式化的值</param>
        /// <returns>格式化文本；失败时返回占位文本</returns>
        private static string FormatValue(object? value)
        {
            if (value == null)
                return "null";

            try
            {
                return value.ToString() ?? "null";
            }
            catch
            {
                return "<ToString 失败>";
            }
        }
    }
}
