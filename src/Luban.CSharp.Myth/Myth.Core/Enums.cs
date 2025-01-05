namespace Myth;

public enum MythValueType
{
    Int,
    Bool,
    String,
    Enum,
    NoArgumentFunctionCall,
    Unknown
}

public enum MythCompareOp
{
    Equal,
    NotEqual,
    Greater,
    GreaterEq,
    Less,
    LessEq
}

public enum MythLogicalOp
{
    And,
    Or
}

public enum MythTokenType
{
    Unknown = 0,
    Identifier = 1,
    IntLiteral = 2,
    StringLiteral = 3,
    BoolLiteral = 4,
    EnumLiteral = 17,

    // 运算符
    Equal = 5, // ==
    NotEqual = 6, // !=
    Greater = 7, // >
    GreaterEq = 8, // >=
    Less = 9, // <
    LessEq = 10, // <=
    AndAnd = 11, // &&
    OrOr = 12, // ||

    // 其他符号
    LParen = 13, // (
    RParen = 14, // )
    Comma = 15, // ,
    End = 16 // 用于表示结束或未知
}
