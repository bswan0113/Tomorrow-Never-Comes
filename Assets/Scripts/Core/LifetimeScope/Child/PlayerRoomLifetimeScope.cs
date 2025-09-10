using Core.Interface;
using Features.Player;
using Features.UI.Common;
using Features.World;
using VContainer;
using VContainer.Unity;

namespace Core.LifetimeScope.Child
{
    public class PlayerRoomLifetimeScope : VContainer.Unity.LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<DialogueUIHandler>()
                .As<IDialogueUIHandler>();
            builder.RegisterEntryPoint<DialogueInitializer>().AsSelf();
            builder.RegisterComponentInHierarchy<StatusUIController>();
            builder.RegisterComponentInHierarchy<ActionSequencer>();
            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InteractionObject>().AsSelf();

        }
    }
}