using Core.Utilities.Generator;
using E3A.Application.Options;
using E3A.Domain.Engineers;

namespace E3A.Application.Engineers.Shared;

public static class EngineerSlugResolver
{
    public static async Task<string> ResolveUniqueAsync(string baseSlug, IEngineerRepository engineerRepository, IGenerator generator, EngineersOptions options, CancellationToken cancellationToken)
    {
        if (!await engineerRepository.IsSlugExistsAsync(baseSlug, cancellationToken).ConfigureAwait(false))
        {
            return baseSlug;
        }

        // Re-normalize shorter so "{prefix}-{suffix}" can never exceed SlugMaxLength.
        var prefix = EngineerSlugGenerator.Normalize(baseSlug, options.SlugMaxLength - options.SlugSuffixSize - 1);
        string candidateSlug;

        do
        {
            // Core IGenerator always emits the separator before the empty suffix, leaving a trailing hyphen.
            candidateSlug = generator.Generate(prefix: prefix, size: options.SlugSuffixSize).TrimEnd('-');
        } while (await engineerRepository.IsSlugExistsAsync(candidateSlug, cancellationToken).ConfigureAwait(false));

        return candidateSlug;
    }
}
