using Core.Interface;
using Features.UI.Common;
using VContainer;
using VContainer.Unity;

namespace Core.SceneLifetimeScope
{
    public class PlayerRoomLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<DialogueUIHandler>()
                .As<IDialogueUIHandler>();
            builder.RegisterEntryPoint<DialogueInitializer>().AsSelf();
            builder.RegisterComponentInHierarchy<StatusUIController>();
            builder.RegisterComponentInHierarchy<ActionSequencer>();
            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InteractionObject>();

        }
    }
}