using Google.Protobuf.Reflection;
using Luban.Datas;
using Luban.RawDefs;
using Luban.Schema;
using Luban.Utils;

namespace Luban.Protobuf.Shimmer;

[BeanSchemaLoader("pb")]
public class ProtoBeanSchemaLoader : IBeanSchemaLoader
{
    public DType DescriptorFieldToDType(FieldDescriptorProto fieldDescriptorProto)
    {
        switch (fieldDescriptorProto.Type)
        {
            case FieldDescriptorProto.Types.Type.Double:
                return DFloat.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Float:
                return DFloat.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Int32:
                return DInt.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Int64:
                return DLong.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Uint32:
                return DInt.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Uint64:
                return DLong.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Sint32:
                return DInt.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Sint64:
                return DLong.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Fixed32:
                return DInt.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Fixed64:
                return DLong.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Sfixed32:
                return DInt.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Sfixed64:
                return DLong.ValueOf(0);
            case FieldDescriptorProto.Types.Type.Bool:
                return DBool.ValueOf(false);
            case FieldDescriptorProto.Types.Type.String:
                // TODO: 可能是解析时间等其他字符串用的？
                return DString.ValueOf(null, "");
            // case FieldDescriptorProto.Types.Type.Bytes:
            //     return DBytes.ValueOf(new byte[0]);
            // case FieldDescriptorProto.Types.Type.Enum:
            //     return DInt.ValueOf(0);
            // case FieldDescriptorProto.Types.Type.Message:
            //     return DBean.ValueOf(null);
            default:
                throw new Exception($"unknown field type:{fieldDescriptorProto.Type}");
        }
    }

    public RawBean Load(string fileName, string beanFullName)
    {
        return LoadTableValueTypeDefineFromFile(fileName, beanFullName);
    }

    public static RawBean LoadTableValueTypeDefineFromFile(string fileName, string valueTypeFullName)
    {
        var valueTypeNamespace = TypeUtil.GetNamespace(valueTypeFullName);
        string valueTypeName = TypeUtil.GetName(valueTypeFullName);
        var cb = new RawBean()
        {
            Namespace = valueTypeNamespace,
            Name = valueTypeName,
            Comment = "",
            Parent = "",
            Groups = new(),
            Fields = new(),
        };

        // (var actualFile, var sheetName) = FileUtil.SplitFileAndSheetName(FileUtil.Standardize(fileName));
        // using var inputStream = new FileStream(actualFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        // var tableDefInfo = SheetLoadUtil.LoadSheetTableDefInfo(actualFile, sheetName, inputStream);
        var descriptorProto = ProtoDescriptorDataLoader.xlsxMessages.Find(descriptorProto => descriptorProto.Name == valueTypeName);
        if (descriptorProto == null)
        {
            throw new Exception($"file:{fileName} title:'{valueTypeName}' not found!");
        }

        foreach (var fieldDescriptorProto in descriptorProto.Field)
        {
            var name = fieldDescriptorProto.Name;
            var cf = new RawField() { Name = name, Groups = new List<string>(), };

            string[] attrs = fieldDescriptorProto.Type.ToString().Trim().Split('&').Select(s => s.Trim()).ToArray();

            if (attrs.Length == 0 || string.IsNullOrWhiteSpace(attrs[0]))
            {
                throw new Exception($"file:{fileName} title:'{name}' type missing!");
            }

            // TODO:
            // cf.Comment = fieldDescriptorProto.Desc;
            cf.Type = attrs[0].ToLower(); // NOTE:这里 ToLower 是因为 Luban.Core 中类型判断用的是写死的全小写，这里做一下适配;
            for (int i = 1; i < attrs.Length; i++)
            {
                var pair = attrs[i].Split('=', 2);
                if (pair.Length != 2)
                {
                    throw new Exception($"file:{fileName} title:'{name}' attr:'{attrs[i]}' is invalid!");
                }

                var attrName = pair[0].Trim();
                var attrValue = pair[1].Trim();
                switch (attrName)
                {
                    case "index":
                    case "ref":
                    case "path":
                    case "range":
                    case "sep":
                    case "regex":
                    {
                        throw new Exception($"file:{fileName} title:'{name}' attr:'{attrName}' 属于type的属性，必须用#分割，尝试'{cf.Type}#{attrs[i]}'");
                    }
                    case "group":
                    {
                        cf.Groups = attrValue.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                        break;
                    }
                    case "comment":
                    {
                        cf.Comment = attrValue;
                        break;
                    }
                    case "tags":
                    {
                        cf.Tags = DefUtil.ParseAttrs(attrValue);
                        break;
                    }
                    default:
                    {
                        throw new Exception($"file:{fileName} title:'{name}' attr:'{attrs[i]}' is invalid!");
                    }
                }
            }

            // TODO:
            // if (!string.IsNullOrEmpty(fieldDescriptorProto.Groups))
            // {
            //     cf.Groups = fieldDescriptorProto.Groups.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            // }

            cb.Fields.Add(cf);
        }

        return cb;
    }
}
