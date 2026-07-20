using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using YF_Manager; 

namespace YFrame.Tests.YF_Manager
{
    public class YF_RelayCommandTests
    {
        /// <summary>
        ///  验证 Execute 真的执行了
        /// </summary>
        [Fact]                                // ← [Fact] 表示这是一个无参数测试
        public void Execute_InvokesAction()   // ← 方法名：被测方法_条件_预期结果
        {
            // 一个标记变量，用来"捕获" Execute 是否被调用
            bool wasCalled = false;

            // 创建一个 RelayCommand，当它被 Execute 时，把标记设为 true
            var command = new YF_RelayCommand(() => wasCalled = true);

            // 调用被测方法
            command.Execute(null!);

            // 不为真则测试失败
            Assert.True(wasCalled);
        }

        /// <summary>
        /// 验证 null 防护
        /// </summary>
        [Fact]
        public void Constructor_Throws_WhenExecuteIsNull()
        {
            // Act & Assert 合一：断言对 null 输入会抛出 ArgumentNullException
            var ex = Assert.Throws<ArgumentNullException>(() => new YF_RelayCommand(null!));

            // 还可以进一步断言异常的参数名
            Assert.Equal("execute", ex.ParamName);
        }

        /// <summary>
        /// 验证接口实现
        /// </summary>
        [Fact]
        public void Implements_ICommand()
        {
            var command = new YF_RelayCommand(() => { });

            Assert.IsAssignableFrom<ICommand>(command);
        }

        [Fact]
        public void CanExecute_NoCondition_ReturnsTrue()
        {
            var command = new YF_RelayCommand(() => { });
            bool canExec = command.CanExecute(null!);
            Assert.True(canExec);
        }

    }
}