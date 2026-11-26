using Castle.DynamicProxy;
using YF_Manager;

namespace YFrame.Tests.YF_Manager.Common.Interceptors;

/// <summary>
/// LogInterceptor 异常语义测试：AOP 记录日志后必须保持原始异常。
/// </summary>
public class LogInterceptorTests
{
    private readonly LogInterceptorTarget _proxy;

    /// <summary>
    /// 创建带日志拦截器的目标代理。
    /// </summary>
    public LogInterceptorTests()
    {
        _proxy = new ProxyGenerator().CreateClassProxy<LogInterceptorTarget>(
            new LogInterceptor());
    }

    /// <summary>
    /// 同步方法抛异常时，调用方必须能捕获原始异常。
    /// </summary>
    [Fact]
    public void SynchronousException_IsRethrown()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => _proxy.ThrowSynchronously());

        Assert.Equal("sync failure", exception.Message);
    }

    /// <summary>
    /// 无返回值异步方法抛异常时，await 必须收到原始异常。
    /// </summary>
    [Fact]
    public async Task TaskException_IsRethrown()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _proxy.ThrowTaskAsync());

        Assert.Equal("task failure", exception.Message);
    }

    /// <summary>
    /// 有返回值异步方法抛异常时，await 必须收到原始异常。
    /// </summary>
    [Fact]
    public async Task GenericTaskException_IsRethrown()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _proxy.ThrowTaskResultAsync());

        Assert.Equal("task result failure", exception.Message);
    }

    /// <summary>
    /// 异步方法在返回 Task 之前同步抛异常时，也必须原样抛出。
    /// </summary>
    [Fact]
    public async Task ExceptionBeforeReturningTask_IsRethrown()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _proxy.ThrowBeforeReturningTask());

        Assert.Equal("before task failure", exception.Message);
    }
}

/// <summary>
/// 供 Castle 代理测试的目标类。
/// 所有需要拦截的方法必须是 public virtual。
/// </summary>
public class LogInterceptorTarget
{
    /// <summary>同步抛异常。</summary>
    [Log(Message = "同步异常测试")]
    public virtual void ThrowSynchronously()
        => throw new InvalidOperationException("sync failure");

    /// <summary>异步无返回值抛异常。</summary>
    [Log(Message = "Task 异常测试")]
    public virtual async Task ThrowTaskAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("task failure");
    }

    /// <summary>异步有返回值抛异常。</summary>
    [Log(Message = "Task<T> 异常测试")]
    public virtual async Task<int> ThrowTaskResultAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("task result failure");
    }

    /// <summary>返回 Task 之前同步抛异常。</summary>
    [Log(Message = "Task 同步异常测试")]
    public virtual Task ThrowBeforeReturningTask()
        => throw new InvalidOperationException("before task failure");
}
