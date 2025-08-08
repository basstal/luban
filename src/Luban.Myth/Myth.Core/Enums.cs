namespace Myth;

public enum MythValueType
{
    Unknown,
    Int,
    IntTenThousandth,
    Float,
    Bool,
    String,
    Enum,
    ValueArray,
}

public enum MythCompareOp
{
    Unknown,
    Equal,
    NotEqual,
    Greater,
    GreaterEqual,
    Less,
    LessEqual,
}

public enum MythLogicalOp
{
    Unknown,
    And,
    Or
}

public enum MythTokenType
{
    Unknown = 0,
    Identifier = 1,
    IntLiteral = 2,
    FloatLiteral = 18,
    StringLiteral = 3,
    BoolLiteral = 4,

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
    Semicolon = 16, // ;
    End = 17 // 用于表示结束或未知
}
