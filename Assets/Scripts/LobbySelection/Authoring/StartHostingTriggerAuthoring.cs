using LobbySelection.Components;
using Unity.Entities;
using UnityEngine;

namespace LobbySelection.Authoring
{
    public class StartHostingTriggerAuthoring : MonoBehaviour
    {
        private class StartHostingTriggerBaker : Baker<StartHostingTriggerAuthoring>
        {
            public override void Bake(StartHostingTriggerAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(entity, new StartHostingTrigger());
            }
        }
    }
}