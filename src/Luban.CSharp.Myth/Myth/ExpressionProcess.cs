using System.Text;
using Luban.Defs;

// ReSharper disable CollectionNeverQueried.Global

public struct ExpressionProcessResult
{
    public HashSet<string> constDefinitions;

    public List<string> methods;

    // public List<string> mappings;
    public string valueCallMappings;
    public Dictionary<string, DelegateType> delegateTypesMapping;
    public Dictionary<string, List<string>> constValues;
    public DefBean bean;
}

public struct DelegateType
{
    public string item1;
    public int item2;
    public string item3;
    public ParameterType item4;
}

public struct ExpressionInfo
{
    public string expression;
    public string functionName;
    public ParameterType parameterType;
}
