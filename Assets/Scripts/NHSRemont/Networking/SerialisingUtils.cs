using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Networking
{ 
    public static class SerialisingUtils
    {
        public static T ReceiveNext<T>(this PhotonStream stream)
        {
            return (T) stream.ReceiveNext();
        }
    }
}