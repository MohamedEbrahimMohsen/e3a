using System.Reflection;
using System.Text.RegularExpressions;
using E3A.Application.Publishing.Security;
using FluentAssertions;
using Xunit;

namespace E3A.Tests.Publishing.Security;

public sealed class ScanRuleCatalogueTests
{
    private static readonly Regex NestedUnboundedQuantifier = new(@"\([^()]*[*+][^()]*\)\s*[*+]", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly List<string> DeclaredRuleIds = [.. typeof(ScanRuleIds).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Select(x => (string)x.GetRawConstantValue()!)];

    [Fact]
    public void AllRules_ShouldDeclareMatchTimeout_WhenCompiled()
    {
        ScanRuleCatalogue.AllRules.Should().OnlyContain(x => x.Pattern.MatchTimeout == ScanRuleCatalogue.MatchTimeout);
        ScanRuleCatalogue.AllRules.Should().OnlyContain(x => x.Pattern.MatchTimeout != Regex.InfiniteMatchTimeout);
    }

    [Fact]
    public void AllRules_ShouldBeCompiledAndCultureInvariant_WhenDeclared()
    {
        ScanRuleCatalogue.AllRules.Should().OnlyContain(x => x.Pattern.Options.HasFlag(RegexOptions.Compiled) && x.Pattern.Options.HasFlag(RegexOptions.IgnoreCase) && x.Pattern.Options.HasFlag(RegexOptions.CultureInvariant));
    }

    [Fact]
    public void AllRules_ShouldNotContainNestedUnboundedQuantifiers_WhenInspected()
    {
        NestedUnboundedQuantifier.IsMatch("(a+)+").Should().BeTrue();
        NestedUnboundedQuantifier.IsMatch("(.*)*").Should().BeTrue();

        ScanRuleCatalogue.AllRules.Should().OnlyContain(x => !NestedUnboundedQuantifier.IsMatch(x.Pattern.ToString()));
    }

    [Fact]
    public void AllRules_ShouldHaveUniqueRuleIds_WhenDeclared()
    {
        ScanRuleCatalogue.AllRules.Select(x => x.RuleId).Should().OnlyHaveUniqueItems();
        ScanRuleCatalogue.AllRules.Should().OnlyContain(x => DeclaredRuleIds.Contains(x.RuleId));
    }

    [Fact]
    public void TextRules_ShouldBeDeclaredInAscendingRuleIdOrder_WhenListed()
    {
        ScanRuleCatalogue.TextRules.Select(x => x.RuleId).Should().BeInAscendingOrder(StringComparer.Ordinal);
        ScanRuleCatalogue.ScriptRules.Select(x => x.RuleId).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void RulesFor_ShouldExcludeScriptRules_WhenFileIsNotScript()
    {
        var scriptRuleIds = ScanRuleCatalogue.ScriptRules.Select(x => x.RuleId).ToList();

        ScanRuleCatalogue.RulesFor(false).Select(x => x.RuleId).Should().NotIntersectWith(scriptRuleIds);
        ScanRuleCatalogue.RulesFor(true).Select(x => x.RuleId).Should().Contain(ScanRuleCatalogue.TextRules.Select(x => x.RuleId)).And.Contain(scriptRuleIds);
    }
}
