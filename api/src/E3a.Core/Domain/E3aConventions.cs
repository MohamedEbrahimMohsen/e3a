namespace E3a.Core.Domain;

public static class E3aConventions
{
    // Wire-format invariant, not configuration: published plugin names ("e3a-{login}-{slug}")
    // are baked into installed marketplaces and immutable zip URLs — changing this prefix
    // would orphan every previously installed plugin.
    public const string PluginNamePrefix = "e3a";
}
