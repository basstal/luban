using System.Runtime.Serialization;

namespace Luban.Protobuf.Shimmer;

class InvalidExcelDataException : Exception
{
    public InvalidExcelDataException()
    {
    }

    public InvalidExcelDataException(string message) : base(message)
    {
    }

    public InvalidExcelDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
