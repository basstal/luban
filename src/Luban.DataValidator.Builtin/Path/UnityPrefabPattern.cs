namespace Luban.DataValidator.Builtin.Path;

class UnityPrefabPattern : IPathPattern
{
    public bool EmptyAble { get; set; }

    public UnityPrefabPattern()
    {
    }

    public string[] supportPrefabExtensions = new string[] { ".prefab", ".fbx" };

    public bool ExistPath(string rootDir, string subFile)
    {
        foreach (var ext in supportPrefabExtensions)
        {
            string path = System.IO.Path.Combine(rootDir, subFile + ext);
            if (File.Exists(path))
            {
                return true;
            }
        }
        return false;
    }
}
