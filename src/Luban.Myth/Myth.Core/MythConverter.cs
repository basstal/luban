namespace Myth;

public class MythConverter
{
    public static string CompareOpToString(MythCompareOp op)
    {
        switch (op)
        {
            case MythCompareOp.Equal:
                return "==";
            case MythCompareOp.NotEqual:
                return "!=";
            case MythCompareOp.Greater:
                return ">";
            case MythCompareOp.GreaterEqual:
                return ">=";
            case MythCompareOp.Less:
                return "<";
            case MythCompareOp.LessEqual:
                return "<=";
        }

        return "/*UNKNOWN*/";
    }


    public static string GetEvalFunctionByFunctionSignature(FunctionSignature functionSignature)
    {
        var returnType = functionSignature.ReturnType;
        var haveParams = functionSignature.ParamTypes.Count > 0;
        switch (returnType)
        {
            case MythValueType.Int:
            case MythValueType.IntTenThousandth:
            case MythValueType.Float:
            {
                if (!haveParams)
                {
                    return "GetInt";
                }

                return "EvalFunction";
            }
            case MythValueType.Bool:
            {
                if (!haveParams)
                {
                    return "GetBool";
                }

                return "EvalFunctionReturnBool";
            }
        }

        throw new NotImplementedException("GetEvalFunctionByFunctionSignature failed!");
    }
}
