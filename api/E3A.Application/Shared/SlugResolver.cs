using Core.Utilities.Generator;
using E3A.Domain.SharedKernel;

namespace E3A.Application.Shared;

public static class SlugResolver
{
    public static async Task<string> ResolveUniqueAsync(string baseSlug, Func<string, CancellationToken, Task<bool>> isSlugExistsAsync, IGenerator generator, int slugMaxLength, int slugSuffixSize, CancellationToken cancellationToken)
    {
        if (!await isSlugExistsAsync(baseSlug, cancellationToken).ConfigureAwait(false))
        {
            return baseSlug;
        }

        // Re-normalize shorter so "{prefix}-{suffix}" can never exceed SlugMaxLength.
        var prefix = SlugGenerator.Normalize(baseSlug, slugMaxLength - slugSuffixSize - 1);
        string candidateSlug;

        do
        {
            // Core IGenerator always emits the separator before the empty suffix, leaving a trailing hyphen.
            candidateSlug = generator.Generate(prefix: prefix, size: slugSuffixSize).TrimEnd('-');
        } while (await isSlugExistsAsync(candidateSlug, cancellationToken).ConfigureAwait(false));

        return candidateSlug;
    }
}
