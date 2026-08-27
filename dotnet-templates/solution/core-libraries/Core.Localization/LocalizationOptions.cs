using System.Reflection;

namespace Core.Localization;

public class LocalizationOptions
{
    public Assembly ResourceAssembly { get; set; } = default!;

    public string ResourceBaseName { get; set; } = "Messages";
}
