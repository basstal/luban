using Luban.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class ConstantExpression
{
    public Type DataType { get; set; }
    public object Value { get; set; }
}

public class ExpressionCategory : IEquatable<ExpressionCategory>
{
    public List<string> Expressions { get; set; } = new List<string>();
    public List<ConstantExpression> Constants { get; set; } = new List<ConstantExpression>();
    public List<Type> ConstantTypesOrder { get; set; } = new List<Type>(); // 记录常量类型的顺序


    // IEquatable<ExpressionCategory> 接口实现
    public bool Equals(ExpressionCategory other)
    {
        if (other == null) return false;

        // 比较 ConstantTypesOrder 的类型和顺序是否完全一致
        return ConstantTypesOrder.SequenceEqual(other.ConstantTypesOrder);
    }

    // 重写 GetHashCode，根据 ConstantTypesOrder 的内容生成哈希值
    public override int GetHashCode()
    {
        // 使用 ConstantTypesOrder 的类型顺序来计算哈希值
        return ConstantTypesOrder.Aggregate(0, (hash, type) => hash * 31 + (type?.GetHashCode() ?? 0));
    }

    // 重写 Equals 方法，用于非 IEquatable 比较
    public override bool Equals(object obj)
    {
        if (obj is ExpressionCategory otherCategory)
        {
            return Equals(otherCategory);
        }

        return false;
    }
}

public class ExpressionCategorizer
{
    // 处理多个 SyntaxTree
    public List<ExpressionCategory> CategorizeExpressions(List<SyntaxTree> syntaxTrees)
    {
        // // 用于存储分类结果的字典，键是常量类型的顺序
        // var categories = new Dictionary<string, ExpressionCategory>();
        //
        // foreach (var tree in syntaxTrees)
        // {
        //     var category = CategorizeExpressions(tree); // 调用处理单个语法树的方法
        //
        //     // 将结果添加到 categories 字典中
        //     foreach (var cat in category)
        //     {
        //         var key = string.Join(" -> ", cat.ConstantTypesOrder.Select(t => t.Name));
        //         if (!categories.ContainsKey(key))
        //         {
        //             categories[key] = cat;
        //         }
        //         else
        //         {
        //             categories[key].Expressions.AddRange(cat.Expressions);
        //             categories[key].Constants.AddRange(cat.Constants);
        //         }
        //     }
        // }
        //
        // // 返回分类结果
        // return categories.Values.ToList();
        throw new NotImplementedException();
    }

    // 处理单个 SyntaxTree
    public static ExpressionCategory CategorizeExpressions(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetRoot();
        var category = new ExpressionCategory();

        // 遍历每个语法树，查找常量表达式
        foreach (var node in root.DescendantNodes())
        {
            if (node is LiteralExpressionSyntax literal)
            {
                // 获取常量值和类型
                var value = literal.Token.Value;
                var type = value?.GetType();

                if (type != null)
                {
                    // 添加常量数据
                    category.Constants.Add(new ConstantExpression { DataType = type, Value = value });

                    // 记录常量类型的顺序
                    category.ConstantTypesOrder.Add(type);

                    // 添加对应的表达式字符串
                    category.Expressions.Add(literal.ToString());
                }
            }
        }

        return category;
    }
}
