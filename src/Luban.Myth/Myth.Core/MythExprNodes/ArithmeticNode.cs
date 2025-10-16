namespace Myth
{
    public class ArithmeticNode : MythExprNode
    {
        public MythExprNode Left { get; set; }
        public MythArithmeticOp Op { get; set; }
        public MythExprNode Right { get; set; }

        public ArithmeticNode(MythExprNode left, MythArithmeticOp op, MythExprNode right)
        {
            Left = left;
            Op = op;
            Right = right;
        }

        public override MythValueType ValueType
        {
            get
            {
                if (Left.ValueType == MythValueType.Float || Right.ValueType == MythValueType.Float)
                {
                    return MythValueType.Float;
                }
                if (Left.ValueType == MythValueType.IntTenThousandth || Right.ValueType == MythValueType.IntTenThousandth)
                {
                    return MythValueType.IntTenThousandth;
                }
                return MythValueType.Int;
            }
        }
    }
}
