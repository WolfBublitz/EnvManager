using YamlDotNet.Serialization;

namespace EnvManager.Configuration;

[YamlStaticContext]
[YamlSerializable(typeof(EnvironmentConfiguration))]
[YamlSerializable(typeof(ToolDefinition))]
public partial class YamlConfigurationContext : StaticContext
{
}
