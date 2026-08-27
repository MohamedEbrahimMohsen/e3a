using Core.DDD.Models;
using Core.EntityFrameworkCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace Core.EntityFrameworkCore.Context;

public static class ModelBuilderExtensions
{
    //public static void ConfigureLocalized<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, LocalizedText>> navigationExpression, string? columnPrefix = null)where T : class
    //{
    //    // Derive column prefix from expression if not provided
    //    var prefix = columnPrefix;
    //    if (string.IsNullOrWhiteSpace(prefix))
    //    {
    //        var body = navigationExpression.Body;
    //        var member = body as MemberExpression ?? (body is UnaryExpression unary ? unary.Operand as MemberExpression : null);
            
    //        if (member == null)
    //        {
    //            throw new InfrastructureException(ErrorCodes.ModelBuilderLocalizedTextNavigationExpressionInvalid, $"The navigation expression must be a simple member access (e.g. x => x.Property) {nameof(navigationExpression)}");
    //        }

    //        prefix = member.Member.Name;
    //    }

    //    builder.OwnsOne(navigationExpression!, owned =>
    //    {
    //        owned.Property(p => p.Arabic)
    //             .HasColumnName(prefix + "Ar")
    //             .IsRequired();

    //        owned.Property(p => p.English)
    //             .HasColumnName(prefix + "En")
    //             .IsRequired();
    //    });

    //    builder.Navigation(navigationExpression!).IsRequired();
    //}

    public static void ConfigureLocalized<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, LocalizedText?>> navigationExpression, bool isRequired = true, string? columnPrefix = null) where T : class
    {
        // Derive column prefix from expression if not provided
        var prefix = columnPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            var body = navigationExpression.Body;
            var member = body as MemberExpression ?? (body is UnaryExpression unary ? unary.Operand as MemberExpression : null);

            if (member == null)
            {
                throw new InfrastructureCoreException(ErrorCodes.ModelBuilderLocalizedTextNavigationExpressionInvalid, $"The navigation expression must be a simple member access (e.g. x => x.Property) {nameof(navigationExpression)}");
            }

            prefix = member.Member.Name;
        }

        builder.OwnsOne(navigationExpression, owned =>
        {
            owned.Property(p => p.Arabic)
                 .HasColumnName(prefix + "Ar")
                 .IsRequired(isRequired);

            owned.Property(p => p.English)
                 .HasColumnName(prefix + "En")
                 .IsRequired(isRequired);
        });

        builder.Navigation(navigationExpression)
               .IsRequired(isRequired);
    }
}
