using Core.DDD.Models;
using MediatR;

namespace Core.Notifications.Templates.UpdateNotificationTemplate;

public sealed record UpdateNotificationTemplateCommand(Guid Id, LocalizedText Title, LocalizedText Content, string? DeepLink, string? ImageUrl) : IRequest;
