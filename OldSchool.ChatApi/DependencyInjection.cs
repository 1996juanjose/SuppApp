using MediatR;
using MongoDB.Driver;
using OldSchool.ChatApi.Application.CQRS.Commands;
using OldSchool.ChatApi.Application.CQRS.Queries;
using OldSchool.ChatApi.Application.Interfaces;
using OldSchool.ChatApi.Infrastructure.Mongo;

namespace OldSchool.ChatApi;

public static class DependencyInjection
{
    public static IServiceCollection AddChatApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(configuration.GetConnectionString("Mongo") ?? configuration["Mongo:ConnectionString"]));
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddMediatR(typeof(AddChatMessageHandler).Assembly);
        return services;
    }
}