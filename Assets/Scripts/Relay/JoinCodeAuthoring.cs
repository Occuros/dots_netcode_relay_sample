using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Samples.HelloNetcode
{
    
    public struct JoinCode : IComponentData
    {
        public FixedString64Bytes value;
    }
    
    [DisallowMultipleComponent]
    public class JoinCodeAuthoring : MonoBehaviour
    {
        internal class JoinCodeAuthoringBaker : Baker<JoinCodeAuthoring>
        {
            public override void Bake(JoinCodeAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent<JoinCode>(entity);
            }
        }
    }
}

