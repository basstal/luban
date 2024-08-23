using Google.Protobuf.Reflection;
using Luban.DataLoader;
using Luban.Datas;
using Luban.Defs;
using Luban.Types;
using Luban.Utils;

namespace Luban.Protobuf.Shimmer;

[DataLoader("pb")]
public class ProtoDescriptorDataLoader : DataLoaderBase
{
    public static List<DescriptorProto> xlsxMessages = new List<DescriptorProto>();


    public override Record ReadOne(TBean type)
    {
        throw new NotImplementedException();
    }


    public override List<Record> ReadMulti(TBean type)
    {
        var records = new List<Record>();
        try
        {
            // var xlsxTablesDescriptorProto = xlsxMessages.Find((proto => proto.Name == "xlsxTables"));
            // if (xlsxTablesDescriptorProto == null)
            // {
            //     throw new Exception("message xlsxTables was not found in any proto file");
            // }

            // 对于每一个 xlsxMessage 在生成对应的 xlsx DBean 结构，以便后续能读取对应的 xlsx
            foreach (var descriptorProto in xlsxMessages)
            {
                string tagStr = ""; //TODO:r.Tag;
                if (DataUtil.IsIgnoreTag(tagStr))
                {
                    continue;
                }

                var data = XlsxDescriptorToDBean(descriptorProto, type);
                records.Add(new Record(data, descriptorProto.Name, DataUtil.ParseTags(tagStr)));
            }
        }
        catch (DataCreateException dce)
        {
            // TODO:
            // dce.OriginDataLocation = sheet.UrlWithParams;
            throw;
        }
        catch (Exception e)
        {
            throw new Exception($"{e}\n{e.StackTrace}");
        }

        return records;
    }

    private DBean XlsxDescriptorToDBean(DescriptorProto descriptorProto, TBean type)
    {
        // var data = (DBean)type.Apply(SheetDataCreator.Ins, sheet, descriptorProto);
        List<DType> fields = new List<DType>(type.DefBean.Fields.Count);
        foreach (var defBeanField in type.DefBean.Fields)
        {
            Console.WriteLine($"field.Name : {defBeanField.Name}");
            var name = descriptorProto.Name;
            var camelCaseFieldName = $"{char.ToUpper(name[0])}{name.Substring(1)}";
            if (defBeanField.Name == "input")
            {
                fields.Add(DString.ValueOf(TString.Create(false, null), $"{name}.xlsx"));
            }
            else if (defBeanField.Name == "value_type")
            {
                fields.Add(DString.ValueOf(TString.Create(false, null), camelCaseFieldName));
            }
            else if (defBeanField.Name == "full_name")
            {
                fields.Add(DString.ValueOf(TString.Create(false, null), $"{name}.Tb{camelCaseFieldName}"));
            }
            else // 未使用的字段，只是为了跟 __tables__ 的对齐
            {
                fields.Add(defBeanField.Type == "string" ? DString.ValueOf(TString.Create(false, null), "") : DBool.ValueOf(false));
            }
        }

        return new DBean(type, type.DefBean, fields);
    }

    public override void Load(string rawUrl, string sheetName, Stream stream)
    {
        try
        {
            // 创建一个 FileDescriptorSet 实例来解析流
            FileDescriptorSet descriptorSet = FileDescriptorSet.Parser.ParseFrom(stream);

            // 遍历每个 FileDescriptorProto
            foreach (var fileDescriptorProto in descriptorSet.File)
            {
                // 使用 FileDescriptor.BuildFrom 来构建每个描述符
                // FileDescriptor descriptor = FileDescriptor.BuildFrom(fileDescriptorProto, new FileDescriptor[] { }, true);

                // 输出文件名和包名
                // Console.WriteLine($"Processing file: {fileDescriptorProto.Name}, Package: {fileDescriptorProto.Package}");

                // 遍历描述符中的所有消息类型
                foreach (var message in fileDescriptorProto.MessageType)
                {
                    // // 只添加 isXlsx 的 message
                    // if (!message.Options.HasExtension(Fd2.OptionsExtensions.IsXlsx))
                    // {
                    //     continue;
                    // }

                    xlsxMessages.Add(message);
                    // 输出消息类型的详细信息
                    Console.WriteLine($"Message: {message.Name}");
                    foreach (var field in message.Field)
                    {
                        Console.WriteLine($"  Field: {field.Name} ({field.TypeName}), Number: {field.Number}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing descriptor file: {ex.Message}");
            throw; // 重新抛出异常以便外部处理
        }
    }
}
