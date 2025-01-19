using System.Text;
using Xunit;

namespace Myth.Test
{
    public class MythParserTests
    {
        [Fact]
        public void ParseExpression_ValidOrExpression_ReturnsLogicalNode()
        {
            // Arrange
            var tokens = new List<MythToken>
            {
                new MythToken(MythTokenType.IntLiteral, "1"), new MythToken(MythTokenType.OrOr, "||"), new MythToken(MythTokenType.IntLiteral, "2")
            };
            var parser = new MythParser(tokens);

            // Act
            var result = parser.ParseExpressionAndAnalyzeAST();


            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicalNode>(result);
            var logicalNode = (LogicalNode)result;
            Assert.Equal(MythLogicalOp.Or, logicalNode.Operator);
        }

        [Fact]
        public void ParseExpression_ValidAndExpression_ReturnsLogicalNode()
        {
            // Arrange
            var tokens = new List<MythToken>
            {
                new MythToken(MythTokenType.IntLiteral, "1"), new MythToken(MythTokenType.AndAnd, "&&"), new MythToken(MythTokenType.IntLiteral, "2")
            };
            var parser = new MythParser(tokens);

            // Act
            var result = parser.ParseExpressionAndAnalyzeAST();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicalNode>(result);
            var logicalNode = (LogicalNode)result;
            Assert.Equal(MythLogicalOp.And, logicalNode.Operator);
        }

        [Fact]
        public void ParseExpression_ValidComparisonExpression_ReturnsComparisonNode()
        {
            // Arrange
            var tokens = new List<MythToken>
            {
                new MythToken(MythTokenType.IntLiteral, "1"), new MythToken(MythTokenType.Greater, ">"), new MythToken(MythTokenType.IntLiteral, "2")
            };
            var parser = new MythParser(tokens);

            // Act
            var result = parser.ParseExpressionAndAnalyzeAST();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ComparisonNode>(result);
            var comparisonNode = (ComparisonNode)result;
            Assert.Equal(MythCompareOp.Greater, comparisonNode.Operator);
        }

        // [Fact]
        // public void ParseExpression_InvalidExpression_ReturnsNull()
        // {
        //     // Arrange
        //     var tokens = new List<MythToken> { new MythToken(MythTokenType.IntLiteral, "1"), new MythToken(MythTokenType.Unknown, "?") };
        //     var parser = new MythParser(tokens);
        //
        //     // Act
        //     var result = parser.ParseExpressionAndAnalyzeAST();
        //
        //     // Assert
        //     Assert.Null(result);
        // }

        [Fact]
        public void ParseExpression_ValidParenthesisExpression_ReturnsExpressionNode()
        {
            // 准备一个示例文本
            string conditionText = @"房间翻新等级(Room7,Room8) >=1 &&房间升级完成（Room7,Room8)";

            // 1. 词法分析
            var lexer = new MythLexer(conditionText);
            var tokens = lexer.Tokenize();

            // 2. 语法分析 -> AST
            var parser = new MythParser(tokens);
            MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();

            // 3. 验证
            Assert.NotNull(ast);
            Assert.IsType<LogicalNode>(ast);
            var logicalNode = (LogicalNode)ast;
            Assert.Equal(MythLogicalOp.And, logicalNode.Operator);
            Assert.IsType<FunctionCallNode>(logicalNode.Right);
            Assert.IsType<ComparisonNode>(logicalNode.Left);
            ComparisonNode comparisonNode = (ComparisonNode)logicalNode.Left;
            Assert.Equal(MythCompareOp.GreaterEqual, comparisonNode.Operator);
            LiteralNode literalNode = (LiteralNode)comparisonNode.Right;
            Assert.Equal("1", literalNode.RawValue);
            FunctionCallNode comparisonNodeLeft = (FunctionCallNode)comparisonNode.Left;
            Assert.Equal("房间翻新等级", comparisonNodeLeft.FuncName);
            Assert.Equal(2, comparisonNodeLeft.Arguments.Count);
            Assert.Equal(MythValueType.Int, comparisonNodeLeft.ReturnType);
            comparisonNodeLeft.Arguments.ForEach(argument =>
            {
                Assert.Equal(MythValueType.String, argument.ValueType);
            });

            FunctionCallNode functionCallNode = (FunctionCallNode)comparisonNode.Left;
            Assert.Equal("房间翻新等级", functionCallNode.FuncName);
            Assert.Equal(2, functionCallNode.Arguments.Count);
            Assert.Equal(MythValueType.Int, functionCallNode.ReturnType);
            FunctionCallNode rightFunctionCallNode = (FunctionCallNode)logicalNode.Right;
            Assert.Equal("房间升级完成", rightFunctionCallNode.FuncName);
            Assert.Equal(2, rightFunctionCallNode.Arguments.Count);
            Assert.Equal(MythValueType.Bool, rightFunctionCallNode.ReturnType);
        }


        [Fact]
        public void ParseExpression1()
        {
            // 准备一个示例文本
            string conditionText = @"专业等级（健身）>=2";

            // 1. 词法分析
            var lexer = new MythLexer(conditionText);
            var tokens = lexer.Tokenize();

            // 2. 语法分析 -> AST
            var parser = new MythParser(tokens);
            MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();

            // 3. 验证
            Assert.NotNull(ast);
            Assert.IsType<ComparisonNode>(ast);
            var comparisonNode = (ComparisonNode)ast;
            Assert.Equal(MythCompareOp.GreaterEqual, comparisonNode.Operator);
            Assert.IsType<FunctionCallNode>(comparisonNode.Left);
            Assert.IsType<LiteralNode>(comparisonNode.Right);
            FunctionCallNode functionCallNode = (FunctionCallNode)comparisonNode.Left;
            Assert.Equal("专业等级", functionCallNode.FuncName);
            Assert.Single(functionCallNode.Arguments);
            Assert.Equal(MythValueType.Enum, functionCallNode.Arguments[0].ValueType);
            LiteralNode literalNode = (LiteralNode)comparisonNode.Right;
            Assert.Equal("2", literalNode.RawValue);
        }


        // [Fact]
        // public void ParseExpression2()
        // {
        //     // 准备一个示例文本
        //     string conditionText = @"最大网络小说阅读量>=100";
        //
        //     // 1. 词法分析
        //     var lexer = new MythLexer(conditionText);
        //     var tokens = lexer.Tokenize();
        //
        //     // 2. 语法分析 -> AST
        //     var parser = new MythParser(tokens);
        //     MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();
        [Fact]
        public void ParseExpression2()
        {
            // 准备一个示例文本
            string conditionText = @"最大网络小说阅读量>=100";

            // 1. 词法分析
            var lexer = new MythLexer(conditionText);
            var tokens = lexer.Tokenize();

            // 2. 语法分析 -> AST
            var parser = new MythParser(tokens);
            MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();

            // 3. 验证
            Assert.NotNull(ast);
            Assert.IsType<ComparisonNode>(ast);
            var comparisonNode = (ComparisonNode)ast;
            Assert.Equal(MythCompareOp.GreaterEqual, comparisonNode.Operator);
            Assert.IsType<LiteralNode>(comparisonNode.Left);
            Assert.IsType<LiteralNode>(comparisonNode.Right);
            LiteralNode literalNode = (LiteralNode)comparisonNode.Left;
            Assert.Equal("最大网络小说阅读量", literalNode.RawValue);
            // Assert.True(literalNode.ValueType == MythValueType.NoArgumentFunctionCall);
            LiteralNode literalNodeRight = (LiteralNode)comparisonNode.Right;
            Assert.Equal("100", literalNodeRight.RawValue);
        }

        [Fact]
        public void ParseExpression3()
        {
            // 将会解析 MyFunctions.txt 中的每一行
            MythFunctionTable.LoadFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Myth.Core/FunctionCallMapping.txt"));

            // 查看结果
            foreach (var kv in MythFunctionTable.Signatures)
            {
                var funcName = kv.Key;
                var sig = kv.Value;
                Console.WriteLine($"函数名: {funcName}, 返回: {sig.ReturnType}, " +
                                  $"参数({sig.ParamTypes.Count}): [{string.Join(", ", sig.ParamTypes)}], " +
                                  $"IsParams={sig.IsParams}");
            }
        }

        [Fact]
        public void ParseExpression4()
        {
            // 准备一个示例文本
            string conditionText = @"最大亲密度>=100";

            // 1. 词法分析
            var lexer = new MythLexer(conditionText);
            var tokens = lexer.Tokenize();

            // 2. 语法分析 -> AST
            var parser = new MythParser(tokens);
            MythExprNode ast = parser.ParseExpressionAndAnalyzeAST();


            // 3. 验证
            Assert.NotNull(ast);
            Assert.IsType<ComparisonNode>(ast);
            var comparisonNode = (ComparisonNode)ast;
            Assert.Equal(MythCompareOp.GreaterEqual, comparisonNode.Operator);
            Assert.IsType<LiteralNode>(comparisonNode.Left);
            Assert.IsType<LiteralNode>(comparisonNode.Right);
            LiteralNode literalNode = (LiteralNode)comparisonNode.Left;
            Assert.Equal("最大亲密度", literalNode.RawValue);
            // Assert.True(literalNode.ValueType == MythValueType.NoArgumentFunctionCall);
            LiteralNode literalNodeRight = (LiteralNode)comparisonNode.Right;
            Assert.Equal("100", literalNodeRight.RawValue);
        }

        [Fact]
        public void ParseExpression5()
        {
            // 准备一个示例文本
            string conditionText = @"房间翻新等级(Room7,Room8) >=1 &&房间升级完成（Room7,Room8)";

            // 1. 词法分析
            var lexer = new MythLexer(conditionText);
            var tokens = lexer.Tokenize();

            // 2. 语法分析 -> AST
            var parser = new MythParser(tokens);
            MythExprNode ast = parser.ParseExpression();

            // 3. 生成 C# 代码
            string methodName = "Condition001";
            string code = MythCodeGenerator.GenerateMethodCode(methodName, "IContext", ast);

            // // 4. 收集元信息
            // List<MythMetadata> meta = MythMetadataCollector.Collect(ast);
            //
            // // 5. (可选) 你可以把生成的 code 存到 .cs 文件，再编译到某个 .dll 中
            // //    或者用 CSharpCodeProvider 等动态编译，再获取到可执行的方法
            //
            // StringBuilder stringBuilder = new StringBuilder();
            //
            // stringBuilder.AppendLine("========== Generated C# Code ==========");
            // stringBuilder.AppendLine(code);
            //
            // stringBuilder.AppendLine("========== Metadata Info ==============");
            // // stringBuilder.AppendLine($"Key = {meta.ConditionKey}, OriginalText = {meta.OriginalText}");
            // // stringBuilder.AppendLine($"Identifiers = {string.Join(", ", meta.Identifiers)}");
            // // stringBuilder.AppendLine($"Functions   = {string.Join(", ", meta.Function)}");
            // // stringBuilder.AppendLine($"IntLiterals = {string.Join(", ", meta.IntLiterals)}");
            // // stringBuilder.AppendLine($"StringLiterals = {string.Join(", ", meta.StringLiterals)}");
            // // stringBuilder.AppendLine($"BoolLiterals = {string.Join(", ", meta.BoolLiterals)}");
            //
            //
            // File.WriteAllText("Test001.txt", stringBuilder.ToString());
        }
    }
}
