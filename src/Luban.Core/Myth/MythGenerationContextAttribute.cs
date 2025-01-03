using Luban.CustomBehaviour;

namespace Luban.Schema;

[AttributeUsage(AttributeTargets.Class)]
public class MythGenerationContextAttribute(string name) : BehaviourBaseAttribute(name);
