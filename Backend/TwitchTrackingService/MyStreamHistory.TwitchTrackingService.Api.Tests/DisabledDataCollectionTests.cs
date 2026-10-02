using System.Reflection;
using MyStreamHistory.Shared.Api.Features;
using MyStreamHistory.TwitchTrackingService.Api.Consumers;
using MyStreamHistory.ViewerService.Api.Consumers;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class DisabledDataCollectionTests
{
    // Other dependencies are deliberately absent: disabled collection must never reach
    // Twitch clients, storage, or even message processing (including queued old events).
    [Theory]
    [InlineData(typeof(UserRegisteredConsumer))]
    [InlineData(typeof(StreamOnlineConsumer))]
    [InlineData(typeof(StreamOfflineConsumer))]
    [InlineData(typeof(ChannelUpdateConsumer))]
    [InlineData(typeof(StreamCreatedConsumer))]
    [InlineData(typeof(StreamEndedConsumer))]
    [InlineData(typeof(StreamCategoryChangedConsumer))]
    [InlineData(typeof(ChatMessageConsumer))]
    [InlineData(typeof(StreamLiveConfirmationLostConsumer))]
    [InlineData(typeof(StreamLiveConfirmationRestoredConsumer))]
    public async Task Events_DoNotCollectDataWhenDisabled(Type consumerType)
    {
        var consumer = CreateDisabledConsumer(consumerType);
        await (Task)consumerType.GetMethod("Consume")!.Invoke(consumer, [null])!;
    }

    [Theory]
    [InlineData(typeof(GetEventSubSubscriptionsConsumer))]
    [InlineData(typeof(DeleteAllSubscriptionsConsumer))]
    [InlineData(typeof(SubscribeToAllUsersConsumer))]
    [InlineData(typeof(GetChatSubscriptionsConsumer))]
    [InlineData(typeof(CleanupChatSubscriptionsConsumer))]
    public async Task SubscriptionCommands_FailWithoutCallingTwitchWhenDisabled(Type consumerType)
    {
        async Task Execute()
        {
            try
            {
                await (Task)consumerType.GetMethod("Consume")!.Invoke(CreateDisabledConsumer(consumerType), [null])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            {
                throw exception.InnerException;
            }
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Execute);
        Assert.Contains(TwitchDataCollectionFeature.ConfigurationKey, exception.Message);
    }

    private static object CreateDisabledConsumer(Type type)
    {
        var constructor = Assert.Single(type.GetConstructors());
        return constructor.Invoke(constructor.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(TwitchDataCollectionFeature)
                ? (object)new TwitchDataCollectionFeature(false) : null).ToArray());
    }
}
