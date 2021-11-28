using System.Collections.Generic;

namespace NHSRemont.Networking
{
    public static class NetworkingUtils
    {
        private static readonly Dictionary<ulong, ulong[]> sendToSingleClientCache = new();

        //TODO
        // public static ClientRpcParams SendToSingleClient(ulong id)
        // {
        //     if (!sendToSingleClientCache.TryGetValue(id, out ulong[] targetIds))
        //     {
        //         targetIds = new[] {id};
        //         sendToSingleClientCache.Add(id, targetIds);
        //     }
        //
        //     return new ClientRpcParams
        //     {
        //         Send = new ClientRpcSendParams
        //         {
        //             TargetClientIds = targetIds
        //         }
        //     };
        // }
        //
        // public static ClientRpcParams SendToAllButServer()
        // {
        //     var allIds = NetworkManager.Singleton.ConnectedClientsIds;
        //     ulong[] targetIds = new ulong[allIds.Count-1];
        //     int i = 0;
        //     foreach (ulong id in allIds)
        //     {
        //         if (id == NetworkManager.Singleton.ServerClientId)
        //             continue;
        //
        //         targetIds[i] = id;
        //         i++;
        //     }
        //     
        //     return new ClientRpcParams
        //     {
        //         Send = new ClientRpcSendParams
        //         {
        //             TargetClientIds = targetIds
        //         }
        //     };
        // }
    }
}