using Luban.Defs;

namespace Myth
{
    public enum TokenKind { Unknown = 0, MetadataExpression, LogicOperator }

    public record struct Token
    {
        public TokenKind Kind;
        public int MetaIndex;   // 当 Kind==MetaCmp（指向 MythMetadata[]）
        public MythLogicalOp LogicSymbol; // "&&" "||" 时使用
    }


    public class RpnBuilder
    {
        // 输出结果
        public readonly List<Token> Tokens = new();

        // 存储所有"原子比较"折叠出的 MythMetadata，执行阶段按索引取
        public readonly List<MythMetadata> Metas = new();

        private string m_methodName;

        private List<DefEnum> m_exportEnums;
        public RpnBuilder(List<DefEnum> exportEnums)
        {
            m_exportEnums = exportEnums;
        }

        public void Build(string methodName, MythExprNode root)
        {
            m_methodName = methodName;
            PostOrder(root, null, -1);
        }

        public void Clear()
        {
            Tokens.Clear();
            Metas.Clear();
        }

        private void ToMetadata(MythExprNode node)
        {
            var meta = new MythMetadata();
            MythMetadataCollector.CollectMetadata(node, meta, m_exportEnums);
            // PostOrder(cmp.Left, cmp, 0);
            // PostOrder(cmp.Right, cmp, 1);

            Tokens.Add(new Token
            {
                Kind = TokenKind.MetadataExpression, // 或 TokKind.Compare
                MetaIndex = Metas.Count,
                // CompareOp = cmp.Operator
            });
            Metas.Add(meta);
        }


        private void PostOrder(MythExprNode node, MythExprNode? parent, int childIndex)
        {
            switch (node)
            {
                case LiteralNode literalNode:
                {
                    // ignore single literal node
                    break;
                }
                case FunctionCallNode functionCallNode:
                {
                    ToMetadata(functionCallNode);
                    break;
                }
                case ComparisonNode comparisionNode:
                {
                    ToMetadata(comparisionNode);
                    break;
                }

                // ⑤ 逻辑节点
                case LogicalNode logicNode:
                {

                    PostOrder(logicNode.Left, logicNode, 0);
                    PostOrder(logicNode.Right, logicNode, 1);

                    Tokens.Add(new Token
                    {
                        Kind = TokenKind.LogicOperator,
                        LogicSymbol = logicNode.Operator
                    });
                    break;
                }

                default:
                    throw new NotImplementedException($"Unhandled AST node {node.GetType().Name}");
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"RPN Builder State: {m_methodName}");
            sb.AppendLine("-----------------");

            // 输出 RPN 序列
            sb.AppendLine("RPN Sequence:");
            for (int i = 0; i < Tokens.Count; i++)
            {
                var token = Tokens[i];
                sb.Append($"[{i}] {token.Kind}");

                switch (token.Kind)
                {
                    case TokenKind.MetadataExpression:
                        sb.Append($" (MetaIndex: {token.MetaIndex})");
                        break;
                    case TokenKind.LogicOperator:
                        sb.Append($" (Symbol: {token.LogicSymbol})");
                        break;
                }
                sb.AppendLine();
            }

            // 输出元数据列表
            sb.AppendLine("\nMetadata List:");
            for (int i = 0; i < Metas.Count; i++)
            {
                sb.AppendLine($"[{i}] {Metas[i]}");
            }

            return sb.ToString();
        }
    }
}