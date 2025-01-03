namespace Luban.Myth;

public interface IMythGenerationContextEnhance
{
    public void EnhanceScheme(GenerationContext ctx);
    public void EnhanceLoadDatasAndValidate(GenerationContext genCtx);
}
