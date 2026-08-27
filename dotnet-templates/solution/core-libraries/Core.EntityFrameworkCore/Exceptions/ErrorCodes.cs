namespace Core.EntityFrameworkCore.Exceptions;

public static class ErrorCodes
{
    public static readonly InfrastructureErrorCode RepositoryAddEntityNull =
        new("INFRA_REPOSITORY_ADD_ENTITY_NULL", "E001");

    public static readonly InfrastructureErrorCode RepositoryUpdateEntityNull =
        new("INFRA_REPOSITORY_UPDATE_ENTITY_NULL", "E002");

    public static readonly InfrastructureErrorCode RepositoryDeleteEntityNull =
        new("INFRA_REPOSITORY_DELETE_ENTITY_NULL", "E003");

    public static readonly InfrastructureErrorCode RepositoryAddRangeEntitiesNull =
        new("INFRA_REPOSITORY_ADDRANGE_ENTITIES_NULL", "E004");

    public static readonly InfrastructureErrorCode RepositoryUpdateRangeEntitiesNull =
        new("INFRA_REPOSITORY_UPDATERANGE_ENTITIES_NULL", "E005");

    public static readonly InfrastructureErrorCode RepositoryDeleteRangeEntitiesNull =
        new("INFRA_REPOSITORY_DELETERANGE_ENTITIES_NULL", "E006");

    public static readonly InfrastructureErrorCode ModelBuilderLocalizedTextNavigationExpressionInvalid =
        new("INFRA_MODELBUILDER_LOCALIZEDTEXT_NAVIGATION_EXPRESSION_INVALID", "E007");
}

public record InfrastructureErrorCode(string Code, string MaskedCode);