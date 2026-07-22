namespace YF_Manager
{
    /// <summary>
    /// 轻量级消息中介（Mediator 模式）
    /// 实现组件间的松耦合通信：发送者和接收者通过消息类型关联，彼此不直接引用
    /// 线程安全，基于 Castle.Core AOP 单例模式
    /// 
    /// 使用示例：
    ///   // 订阅
    ///   YF_Messenger.Instance.Register<LogAppendMessage>(msg => Console.WriteLine(msg.Text));
    ///   // 发送
    ///   YF_Messenger.Instance.Send(new LogAppendMessage("Hello"));
    ///   // 取消订阅
    ///   YF_Messenger.Instance.Unregister<LogAppendMessage>(handler);
    /// </summary>
    public class YF_Messenger
    {
        #region AOP单例
        private static readonly Lazy<YF_Messenger> _instance = new Lazy<YF_Messenger>(
            () => new Castle.DynamicProxy.ProxyGenerator()
                .CreateClassProxy<YF_Messenger>(new LogInterceptor()));

        public static YF_Messenger Instance => _instance.Value;
        #endregion

        #region 订阅表
        /// <summary>
        /// 消息类型 → 订阅者列表的映射
        /// Key: 消息类型 (如 typeof(LogAppendMessage))
        /// Value: 该类型的处理委托列表
        /// </summary>
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        /// <summary>
        /// 线程锁，保护 _subscribers 的并发访问
        /// </summary>
        private readonly object _lock = new();
        #endregion

        public YF_Messenger() { }

        /// <summary>
        /// 订阅指定类型的消息
        /// </summary>
        /// <typeparam name="TMessage">消息类型（建议使用 record 类型）</typeparam>
        /// <param name="handler">消息处理委托</param>
        [Log(Level = LogLevel.Debug, Message = "注册消息订阅")]
        public virtual void Register<TMessage>(Action<TMessage> handler)
        {
            var messageType = typeof(TMessage);
            lock (_lock)
            {
                // 如果键存在，则在委托函数列表里面加一个，
                // 如果键不存在，则创建这个键和对应的一个空的委托函数列表，然后在委托函数列表中加入当前的函数
                if (!_subscribers.TryGetValue(messageType, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _subscribers[messageType] = handlers;
                }
                handlers.Add(handler);
            }
        }

        /// <summary>
        /// 取消订阅指定类型的消息
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="handler">之前注册的处理委托</param>
        [Log(Level = LogLevel.Debug, Message = "取消消息订阅")]
        public virtual void Unregister<TMessage>(Action<TMessage> handler)
        {
            var messageType = typeof(TMessage);
            lock (_lock)
            {
                if (_subscribers.TryGetValue(messageType, out var handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                        _subscribers.Remove(messageType);
                }
            }
        }

        /// <summary>
        /// 发送消息：通知所有订阅了 TMessage 类型的处理器
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="message">消息实例</param>
        [Log(Level = LogLevel.Debug, Message = "发送消息")]
        public virtual void Send<TMessage>(TMessage message)
        {
            List<Delegate>? handlersCopy;
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(typeof(TMessage), out var handlers))
                    return;
                // 复制一份，避免在遍历期间被修改
                handlersCopy = new List<Delegate>(handlers);
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<TMessage>)handler)(message);
                }
                catch (Exception ex)
                {
                    // 某个订阅者的异常不应影响其他订阅者
                    YF_Manager_Main.logger?.ErrorInfo(
                        $"YF_Messenger.Send<{typeof(TMessage).Name}>", ex.Message);
                }
            }
        }
    }
}
