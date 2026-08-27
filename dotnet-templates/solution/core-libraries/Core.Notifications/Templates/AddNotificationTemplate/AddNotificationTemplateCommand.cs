using Core.DDD.Models;
using MediatR;

namespace Core.Notifications.Templates.AddNotificationTemplate;

public sealed record AddNotificationTemplateCommand(string Code, LocalizedText Title, LocalizedText Content, string? DeepLink, string? ImageUrl) : IRequest<AddNotificationTemplateResult>;
