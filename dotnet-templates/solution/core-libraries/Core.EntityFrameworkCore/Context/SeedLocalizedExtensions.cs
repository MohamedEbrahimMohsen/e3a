using Core.DDD.Entities;
using Core.DDD.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.EntityFramework.Context;

public static class SeedLocalizedExtensions
{
    public static void SeedLocalized<T>(this EntityTypeBuilder<T> builder, IEnumerable<T> entities) where T : Entity
    {
        foreach (var entity in entities)
        {
            // ---- 1️) Root table seed ----
            builder.HasData(FlattenRoot(entity));

            // ---- 2️) Owned LocalizedText seeds ----
            SeedOwnedLocalized(builder, entity);
        }
    }

    private static object FlattenRoot<T>(T entity)
    {
        var dict = new Dictionary<string, object?>();
        var type = typeof(T);

        foreach (var p in type.GetProperties())
        {
            if (p.PropertyType == typeof(LocalizedText))
                continue;

            dict[p.Name] = p.GetValue(entity);
        }

        return dict.ToAnonymous();
    }

    private static void SeedOwnedLocalized<T>(EntityTypeBuilder<T> builder, T entity) where T : Entity
    {
        var ownerName = typeof(T).Name;

        foreach (var p in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(LocalizedText)))
        {
            var lt = (LocalizedText)p.GetValue(entity)!;

            var ownedData = new Dictionary<string, object?>
        {
            { $"{ownerName}Id", entity.Id },   // correct FK convention
            { $"{p.Name}Ar", lt.Arabic },
            { $"{p.Name}En", lt.English }
        };

            builder.OwnsOne(typeof(LocalizedText), p.Name, ownedBuilder =>
            {
                ownedBuilder.HasData(ownedData.ToAnonymous());
            });
        }
    }

}

public static class DictionaryExtensions
{
    public static object ToAnonymous(this IDictionary<string, object?> values)
    {
        var expando = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;
        foreach (var kv in values)
            expando[kv.Key] = kv.Value;
        return expando;
    }
}