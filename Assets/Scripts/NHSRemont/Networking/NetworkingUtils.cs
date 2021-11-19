using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace NHSRemont.Networking
{
    public static class NetworkingUtils
    {
        private static readonly Dictionary<ulong, ClientRpcParams> sendToSingleClientCache = new();

        public static ClientRpcParams SendToSingleClient(ulong id)
        {
            ClientRpcParams result;
            if (sendToSingleClientCache.TryGetValue(id, out result))
            {
                return result;
            }

            result = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] {id}
                }
            };
            
            sendToSingleClientCache.Add(id, result);
            return result;
        }

        public static ClientRpcParams SendToAllButServer()
        {
            var allIds = NetworkManager.Singleton.ConnectedClientsIds;
            ulong[] targetIds = new ulong[allIds.Count-1];
            int i = 0;
            foreach (ulong id in allIds)
            {
                if (id == NetworkManager.Singleton.ServerClientId)
                    continue;

                targetIds[i] = id;
                i++;
            }
            
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = targetIds
                }
            };
        }
    }
}