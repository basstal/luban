using Luban.Defs;

// ReSharper disable CollectionNeverQueried.Global

public struct ExpressionProcessResult
{
    public HashSet<string> constDefinitions;

    public List<string> methods;

    public string valueCallMappings;
    public Dictionary<string, DelegateType> delegateTypesMapping;

    public Dictionary<ExpressionCategory, List<(string, string, string, string)>> constValues;

    public Dictionary<string, (string, string, bool, string)> constValueGetters;
}

public struct DelegateType
{
    public string item1;
    public int item2;
    public string item3;
    public string item4;
    public string item5;
    public bool is_empty;
}

public struct ExpressionInfo
{
    public string expression;
    public string functionName;
    public ParameterType[] parameterTypes;
}

public struct GetterInfo : IComparable, IEquatable<GetterInfo>
{
    public string[] getterKeyTypes;
    public string[] getterKeyNames;
    public DefField tableExportDefField;
    public string fieldName;

    public int CompareTo(object? obj)
    {
        var other = (GetterInfo)obj!;
        return Equals(other) ? 0 : 1;
    }

    public bool Equals(GetterInfo other)
    {
        return string.Join("", getterKeyTypes).Equals(string.Join("", other.getterKeyTypes)) && tableExportDefField.Equals(other.tableExportDefField) &&
               fieldName.Equals(other.fieldName) && string.Join("", getterKeyNames).Equals(string.Join("", other.getterKeyNames));
    }

    public override bool Equals(object? obj)
    {
        return obj is GetterInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(string.Join("", getterKeyTypes), string.Join("", getterKeyNames), tableExportDefField, fieldName);
    }
}
