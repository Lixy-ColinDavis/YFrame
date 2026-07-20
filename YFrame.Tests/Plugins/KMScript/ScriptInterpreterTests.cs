using YF_KMScript.Services;
using static YF_KMScript.Model.LogType;

namespace YFrame.Tests.Plugins.KMScript
{
    /// <summary>
    /// ScriptInterpreter 单元测试 — DSL 命令解析与执行逻辑
    /// 通过 OnOutput 事件捕获输出来验证行为
    /// </summary>
    public class ScriptInterpreterTests
    {
        /// <summary>
        /// 辅助方法：执行脚本并收集所有输出
        /// </summary>
        private List<(string Message, YF_KMScript.Model.LogType Type)> ExecuteScript(
            string script, bool shouldStop = false)
        {
            var outputs = new List<(string Message, YF_KMScript.Model.LogType Type)>();
            var interpreter = new ScriptInterpreter();
            interpreter.OnOutput += (msg, type) => outputs.Add((Message: msg, Type: type));

            interpreter.Execute(script, () => shouldStop);

            return outputs;
        }

        #region 定义 命令测试

        /// <summary>
        /// 定义 整数变量
        /// </summary>
        [Fact]
        public void Define_IntVariable_OutputsSuccess()
        {
            var outputs = ExecuteScript("定义 count = 10");

            Assert.Contains(outputs, o => o.Message.Contains("定义变量") && o.Message.Contains("count"));
            Assert.Contains(outputs, o => o.Message.Contains("10"));
        }

        /// <summary>
        /// 定义 字符串变量（引号包裹）
        /// </summary>
        [Fact]
        public void Define_StringVariable_OutputsSuccess()
        {
            var outputs = ExecuteScript("定义 name = \"hello world\"");

            Assert.Contains(outputs, o => o.Message.Contains("name"));
            Assert.Contains(outputs, o => o.Message.Contains("hello world"));
        }

        /// <summary>
        /// 定义 浮点数变量
        /// </summary>
        [Fact]
        public void Define_DoubleVariable_OutputsSuccess()
        {
            var outputs = ExecuteScript("定义 pi = 3.14");

            Assert.Contains(outputs, o => o.Message.Contains("pi"));
            Assert.Contains(outputs, o => o.Message.Contains("3.14"));
        }

        /// <summary>
        /// 定义 格式错误时输出错误信息
        /// </summary>
        [Fact]
        public void Define_InvalidFormat_OutputsError()
        {
            var outputs = ExecuteScript("定义");

            Assert.Contains(outputs, o => o.Message.Contains("格式错误"));
        }

        /// <summary>
        /// 定义 覆盖已有变量
        /// </summary>
        [Fact]
        public void Define_OverrideVariable_Works()
        {
            var outputs = ExecuteScript("""
                定义 x = 5
                定义 x = 99
                """);

            Assert.Contains(outputs, o => o.Message.Contains("x") && o.Message.Contains("5"));
            Assert.Contains(outputs, o => o.Message.Contains("x") && o.Message.Contains("99"));
        }

        #endregion

        #region 输出 命令测试

        /// <summary>
        /// 输出 命令触发 Output 类型事件
        /// </summary>
        [Fact]
        public void Output_Command_TriggersOutputEvent()
        {
            var outputs = ExecuteScript("输出 Hello World");

            var outputEntries = outputs.Where(o => o.Type == Output).ToList();
            Assert.NotEmpty(outputEntries);
            Assert.Contains(outputEntries, o => o.Message.Contains("Hello World"));
        }

        /// <summary>
        /// 输出 空消息
        /// </summary>
        [Fact]
        public void Output_EmptyMessage_Works()
        {
            var outputs = ExecuteScript("输出 ");

            Assert.Contains(outputs, o => o.Type == Output);
        }

        #endregion

        #region 等待 命令测试

        /// <summary>
        /// 等待 数字毫秒
        /// </summary>
        [Fact]
        public void Wait_Milliseconds_CompletesInTime()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var outputs = ExecuteScript("等待 50");
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds >= 40); // 至少等待了约 50ms
            Assert.Contains(outputs, o => o.Message.Contains("⏱") && o.Message.Contains("50"));
        }

        /// <summary>
        /// 等待 变量名（使用之前定义的变量）
        /// </summary>
        [Fact]
        public void Wait_VariableReference_UsesVariableValue()
        {
            var outputs = ExecuteScript("""
                定义 delay = 30
                等待 delay
                """);

            Assert.Contains(outputs, o => o.Message.Contains("⏱") && o.Message.Contains("30"));
        }

        /// <summary>
        /// 等待 无参数格式错误
        /// </summary>
        [Fact]
        public void Wait_InvalidFormat_OutputsError()
        {
            var outputs = ExecuteScript("等待");

            Assert.Contains(outputs, o => o.Message.Contains("格式错误"));
        }

        #endregion

        #region 如果/否则 条件命令测试

        /// <summary>
        /// 如果 存在 — 变量存在时执行子块
        /// </summary>
        [Fact]
        public void If_Exists_True_ExecutesChildren()
        {
            var outputs = ExecuteScript("""
                定义 flag = 1
                如果 flag 存在
                    输出 变量存在
                """);

            Assert.Contains(outputs, o => o.Message.Contains("存在") && o.Message.Contains("是"));
            Assert.Contains(outputs, o => o.Message.Contains("变量存在") && o.Type == Output);
        }

        /// <summary>
        /// 如果 存在 — 变量不存在时跳过子块
        /// </summary>
        [Fact]
        public void If_Exists_False_SkipsChildren()
        {
            var outputs = ExecuteScript("""
                如果 nonexist 存在
                    输出 不应该出现
                """);

            Assert.Contains(outputs, o => o.Message.Contains("否"));
            Assert.DoesNotContain(outputs, o => o.Message.Contains("不应该出现"));
        }

        /// <summary>
        /// 如果 等于 — 数值相等时执行
        /// </summary>
        [Fact]
        public void If_Equal_True_ExecutesChildren()
        {
            var outputs = ExecuteScript("""
                定义 score = 100
                如果 score 等于 100
                    输出 满分
                """);

            Assert.Contains(outputs, o => o.Message.Contains("成立"));
            Assert.Contains(outputs, o => o.Message.Contains("满分") && o.Type == Output);
        }

        /// <summary>
        /// 如果 等于 — 不相等时跳过
        /// </summary>
        [Fact]
        public void If_Equal_False_SkipsChildren()
        {
            var outputs = ExecuteScript("""
                定义 score = 50
                如果 score 等于 100
                    输出 不会出现
                """);

            Assert.Contains(outputs, o => o.Message.Contains("不成立"));
            Assert.DoesNotContain(outputs, o => o.Message.Contains("不会出现"));
        }

        /// <summary>
        /// 如果-否则 结构：条件成立走如果分支
        /// </summary>
        [Fact]
        public void IfElse_True_ExecutesIfBranch()
        {
            var outputs = ExecuteScript("""
                定义 val = 10
                如果 val 大于 5
                    输出 大于5
                否则
                    输出 不大于5
                """);

            Assert.Contains(outputs, o => o.Message.Contains("大于5") && o.Type == Output);
            Assert.DoesNotContain(outputs, o => o.Message.Contains("不大于5"));
        }

        /// <summary>
        /// 如果-否则 结构：条件不成立走否则分支
        /// </summary>
        [Fact]
        public void IfElse_False_ExecutesElseBranch()
        {
            var outputs = ExecuteScript("""
                定义 val = 3
                如果 val 大于 5
                    输出 分支A
                否则
                    输出 分支B
                """);

            Assert.DoesNotContain(outputs, o => o.Message == "分支A" && o.Type == Output);
            Assert.Contains(outputs, o => o.Message == "分支B" && o.Type == Output);
        }

        /// <summary>
        /// 否则 单独使用（不跟在如果之后）应报错
        /// </summary>
        [Fact]
        public void Else_Standalone_OutputsError()
        {
            var outputs = ExecuteScript("否则");

            Assert.Contains(outputs, o => o.Message.Contains("❌") && o.Message.Contains("否则必须跟在如果之后"));
        }

        /// <summary>
        /// 如果 字符串等于
        /// </summary>
        [Fact]
        public void If_StringEqual_Works()
        {
            var outputs = ExecuteScript("""
                定义 color = "red"
                如果 color 等于 "red"
                    输出 匹配成功
                """);

            Assert.Contains(outputs, o => o.Message.Contains("成立"));
            Assert.Contains(outputs, o => o.Message.Contains("匹配成功"));
        }

        /// <summary>
        /// 如果 字符串不等于
        /// </summary>
        [Fact]
        public void If_StringNotEqual_Works()
        {
            var outputs = ExecuteScript("""
                定义 color = "blue"
                如果 color 不等于 "red"
                    输出 不匹配
                """);

            Assert.Contains(outputs, o => o.Message.Contains("不匹配"));
        }

        /// <summary>
        /// 如果 比较运算符：大于、小于、大于等于、小于等于
        /// </summary>
        [Theory]
        [InlineData("大于", 10, 5, true)]
        [InlineData("大于", 5, 10, false)]
        [InlineData("小于", 3, 10, true)]
        [InlineData("小于", 10, 3, false)]
        [InlineData("大于等于", 10, 10, true)]
        [InlineData("大于等于", 5, 10, false)]
        [InlineData("小于等于", 5, 10, true)]
        [InlineData("小于等于", 10, 5, false)]
        public void If_ComparisonOperators(string op, int left, int right, bool shouldMatch)
        {
            var outputs = ExecuteScript($"""
                定义 x = {left}
                如果 x {op} {right}
                    输出 匹配
                """);

            if (shouldMatch)
                Assert.Contains(outputs, o => o.Message.Contains("匹配") && o.Type == Output);
            else
                Assert.DoesNotContain(outputs, o => o.Message.Contains("匹配") && o.Type == Output);
        }

        #endregion

        #region 循环 命令测试

        /// <summary>
        /// 循环 N 次 — 执行 N 次子块
        /// </summary>
        [Fact]
        public void Loop_FixedCount_ExecutesCorrectTimes()
        {
            var outputs = ExecuteScript("""
                循环 3 次
                    输出 执行
                """);

            var execCount = outputs.Count(o => o.Message.Contains("执行") && o.Type == Output);
            Assert.Equal(3, execCount);
        }

        /// <summary>
        /// 循环 中可以使用 循环次数 变量
        /// </summary>
        [Fact]
        public void Loop_UsesLoopIndexVariable()
        {
            var outputs = ExecuteScript("""
                循环 2 次
                    输出 循环次数变量
                """);

            Assert.Contains(outputs, o => o.Message.Contains("第 1/2 次循环"));
            Assert.Contains(outputs, o => o.Message.Contains("第 2/2 次循环"));
        }

        /// <summary>
        /// 循环 使用变量作为次数
        /// </summary>
        [Fact]
        public void Loop_VariableReference_UsesVariable()
        {
            var outputs = ExecuteScript("""
                定义 n = 2
                循环 n 次
                    输出 执行
                """);

            var execCount = outputs.Count(o => o.Message.Contains("执行") && o.Type == Output);
            Assert.Equal(2, execCount);
        }

        /// <summary>
        /// 循环 无效次数格式报错
        /// </summary>
        [Fact]
        public void Loop_InvalidCount_OutputsError()
        {
            var outputs = ExecuteScript("循环 abc 次");

            Assert.Contains(outputs, o => o.Message.Contains("无效的循环次数"));
        }

        /// <summary>
        /// 循环 格式错误
        /// </summary>
        [Fact]
        public void Loop_MalformedFormat_OutputsError()
        {
            var outputs = ExecuteScript("循环");

            Assert.Contains(outputs, o => o.Message.Contains("格式错误"));
        }

        /// <summary>
        /// 循环 可以通过 shouldStop 中途停止
        /// </summary>
        [Fact]
        public void Loop_ShouldStop_StopsEarly()
        {
            var outputs = new List<(string Message, YF_KMScript.Model.LogType Type)>();
            var interpreter = new ScriptInterpreter();
            interpreter.OnOutput += (msg, type) => outputs.Add((Message: msg, Type: type));

            int callCount = 0;
            interpreter.Execute("""
                循环 100 次
                    输出 执行
                """, () => { callCount++; return callCount >= 3; });

            var execCount = outputs.Count(o => o.Message.Contains("执行") && o.Type == Output);
            Assert.True(execCount < 10); // 应该在几次循环内就停止了
        }

        #endregion

        #region 嵌套结构测试

        /// <summary>
        /// 如果内嵌套循环
        /// </summary>
        [Fact]
        public void Nested_IfWithLoop_Works()
        {
            var outputs = ExecuteScript("""
                定义 x = 1
                如果 x 等于 1
                    循环 2 次
                        输出 嵌套执行
                """);

            var execCount = outputs.Count(o => o.Message.Contains("嵌套执行") && o.Type == Output);
            Assert.Equal(2, execCount);
        }

        /// <summary>
        /// 循环内嵌套如果
        /// </summary>
        [Fact]
        public void Nested_LoopWithIf_Works()
        {
            var outputs = ExecuteScript("""
                定义 val = 5
                循环 3 次
                    如果 val 大于 0
                        输出 大于零
                """);

            var count = outputs.Count(o => o.Message.Contains("大于零") && o.Type == Output);
            Assert.Equal(3, count);
        }

        #endregion

        #region 注释和空白行

        /// <summary>
        /// 注释行被忽略
        /// </summary>
        [Fact]
        public void Comments_AreIgnored()
        {
            var outputs = ExecuteScript("""
                // 这是一个注释
                输出 实际输出
                // 另一个注释
                """);

            // 不应该有任何注释解析的错误，且只有一条输出
            var outputEntries = outputs.Where(o => o.Type == Output).ToList();
            Assert.Single(outputEntries);
            Assert.Contains(outputEntries, o => o.Message.Contains("实际输出"));
        }

        /// <summary>
        /// 空行被忽略
        /// </summary>
        [Fact]
        public void EmptyLines_AreIgnored()
        {
            var outputs = ExecuteScript("""

                输出 after empty lines

                """);

            Assert.Contains(outputs, o => o.Message.Contains("after empty lines") && o.Type == Output);
        }

        #endregion

        #region 缩进类型检测

        /// <summary>
        /// Tab 缩进被正确识别
        /// </summary>
        [Fact]
        public void Indentation_Tabs_DetectedCorrectly()
        {
            // 使用 Tab 缩进
            var script = "定义 x = 1\r\n如果 x 等于 1\r\n\t输出 tab缩进";

            var outputs = ExecuteScript(script);

            Assert.Contains(outputs, o => o.Message.Contains("tab缩进") && o.Type == Output);
        }

        /// <summary>
        /// 空格缩进被正确识别（2 空格 = 1 层）
        /// </summary>
        [Fact]
        public void Indentation_Spaces_DetectedCorrectly()
        {
            var script = "定义 x = 1\r\n如果 x 等于 1\r\n  输出 空格缩进";

            var outputs = ExecuteScript(script);

            Assert.Contains(outputs, o => o.Message.Contains("空格缩进") && o.Type == Output);
        }

        #endregion

        #region 未知命令

        /// <summary>
        /// 未知命令输出警告
        /// </summary>
        [Fact]
        public void UnknownCommand_OutputsWarning()
        {
            var outputs = ExecuteScript("不存在的命令 arg1");

            Assert.Contains(outputs, o => o.Message.Contains("⚠") && o.Message.Contains("未知命令"));
        }

        #endregion

        #region 找图错误格式

        /// <summary>
        /// 找图 格式错误时输出错误信息（不实际执行图片查找）
        /// </summary>
        [Fact]
        public void FindImage_InvalidFormat_OutputsError()
        {
            var outputs = ExecuteScript("找图");

            Assert.Contains(outputs, o => o.Message.Contains("找图格式错误"));
        }

        /// <summary>
        /// 找图 引用未定义的变量
        /// </summary>
        [Fact]
        public void FindImage_UndefinedVariable_OutputsError()
        {
            var outputs = ExecuteScript("找图 undefinedVar");

            Assert.Contains(outputs, o => o.Message.Contains("变量未定义"));
        }

        #endregion

        #region 点击错误格式

        /// <summary>
        /// 点击 格式错误
        /// </summary>
        [Fact]
        public void Click_InvalidFormat_OutputsError()
        {
            var outputs = ExecuteScript("点击");

            Assert.Contains(outputs, o => o.Message.Contains("点击格式错误"));
        }

        /// <summary>
        /// 点击 未定义的位置变量
        /// </summary>
        [Fact]
        public void Click_UndefinedVariable_OutputsError()
        {
            var outputs = ExecuteScript("点击 unknownPos");

            Assert.Contains(outputs, o => o.Message.Contains("位置变量未定义"));
        }

        #endregion

        #region 截图错误格式

        /// <summary>
        /// 截图 格式错误
        /// </summary>
        [Fact]
        public void Capture_InvalidFormat_OutputsError()
        {
            var outputs = ExecuteScript("截图");

            Assert.Contains(outputs, o => o.Message.Contains("截图格式错误"));
        }

        #endregion

        #region 默认阈值

        /// <summary>
        /// DefaultThreshold 默认值为 0.80
        /// </summary>
        [Fact]
        public void DefaultThreshold_IsCorrect()
        {
            var interpreter = new ScriptInterpreter();
            Assert.Equal(0.80, interpreter.DefaultThreshold);
        }

        /// <summary>
        /// DefaultThreshold 可以修改
        /// </summary>
        [Fact]
        public void DefaultThreshold_CanBeChanged()
        {
            var interpreter = new ScriptInterpreter { DefaultThreshold = 0.95 };
            Assert.Equal(0.95, interpreter.DefaultThreshold);
        }

        #endregion
    }
}
