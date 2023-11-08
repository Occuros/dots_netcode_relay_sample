using System.Collections;
using System.Collections.Generic;
using Unity.NetCode;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class GameBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 7979; // Enabled auto connect
        // AutoConnectPort = 0;
        CreateLocalWorld(defaultWorldName);
        
        
        return base.Initialize(defaultWorldName); // Use the regular bootstrap
   
        return true;
    }
}
