using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using NHSRemont.Networking;
using NHSRemont.Utility;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Debug = UnityEngine.Debug;

namespace NHSRemont.Environment.Fractures
{
    public class GraphSync : MonoBehaviourPunCallbacks, IPunObservable
    {
        private MasterGraph graph;
        [SerializeField, ReadOnlyInEditor]
        private NetworkedPhysicsState[] prevStates;
        private bool[] nodesDestroyed;
        private Dictionary<GraphNode, int> nodeToIndex = new();
        private double[] nodesLastSyncTime;
        private bool receivedInitialSync = false;
        
        private void Awake()
        {
            graph = GetComponent<MasterGraph>();
            photonView.Synchronization = ViewSynchronization.ReliableDeltaCompressed;
        }

        private void Start()
        {
            prevStates = new NetworkedPhysicsState[graph.allNodes.Length];
            nodesDestroyed = new bool[graph.allNodes.Length];
            nodesLastSyncTime = new double[graph.allNodes.Length];
            int i = 0;
            foreach (GraphNode node in graph.allNodes)
            {
                node.WritePhysicsState(ref prevStates[i]);
                nodeToIndex.Add(node, i);
                
                int idx = i;
                node.destroyedCallback += (_, vel) =>
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        if (nodesDestroyed[idx]) return;
                        
                        photonView.RPC(nameof(NodeDestroyedRPC), RpcTarget.All, idx, vel);
                    }
                };
                i++;
            }
            
            if (PhotonNetwork.IsMasterClient)
                receivedInitialSync = true;
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            SendFullStateTo(newPlayer);
        }

        public void SendFullStateTo(Player target)
        {
            using MemoryStream m = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(m))
            {
                WriteBytes(writer);
            }
            
            photonView.RPC(nameof(SynchroniseFullStateRPC), target, m.ToArray(), PhotonNetwork.Time);

            void WriteBytes(BinaryWriter writer)
            {
                bool fractured = graph.connectedNodes.Count != graph.allNodes.Length;
                writer.Write(fractured); //bool fractured

                if (!fractured) return;

                List<int> destroyedNodes = new();
                List<(int idx, GraphNode node)> disconnectedNodes = new();
                for (int i = 0; i < graph.allNodes.Length; i++)
                {
                    GraphNode node = graph.allNodes[i];

                    if (node == null)
                    {
                        destroyedNodes.Add(i);
                        continue;
                    }

                    if (node.frozen) continue;

                    disconnectedNodes.Add((i, node));
                }

                writer.Write((short) destroyedNodes.Count); //short destroyed count
                foreach (int idx in destroyedNodes)
                {
                    writer.Write((short) idx); //short destroyed index (per destroyed count)
                }

                writer.Write((short) disconnectedNodes.Count); //short disconnected count
                foreach ((int idx, GraphNode node) in disconnectedNodes)
                {
                    writer.Write((short) idx); //short disconnected index (per disconnected count)
                    prevStates[idx].Send(writer); //physics state (per disconnected count)
                }
            }
        }

        [PunRPC]
        public void SynchroniseFullStateRPC(byte[] bytes, double sentTime)
        {
            float lag = Mathf.Abs((float) (PhotonNetwork.Time - sentTime));
            StartCoroutine(SynchroniseOnceReady());

            IEnumerator SynchroniseOnceReady()
            {
                double timeReceived = Time.timeAsDouble;
                while (!graph.doneSetup)
                {
                    yield return null;
                }
                
                using MemoryStream m = new MemoryStream(bytes);
                using (BinaryReader reader = new BinaryReader(m))
                {
                    bool fractured = reader.ReadBoolean(); //bool fractured

                    if (!fractured)
                    {
                        receivedInitialSync = true;
                        yield break;
                    }
                    short destroyedCount = reader.ReadInt16(); //short destroyed count
                    for (int i = 0; i < destroyedCount; i++)
                    {
                        short destroyedIdx = reader.ReadInt16(); //short destroyed index (per destroyed count)
                        GraphNode destroyedNode = graph.allNodes[destroyedIdx];
                        if (destroyedNode != null)
                        {
                            destroyedNode.RemoveSelf();
                            nodesDestroyed[destroyedIdx] = true;
                        }
                    }

                    short disconnectedCount = reader.ReadInt16(); //short disconnected count
                    for (int i = 0; i < disconnectedCount; i++)
                    {
                        short disconnectedIdx = reader.ReadInt16(); //short disconnected index (per disconnected count);
                        prevStates[disconnectedIdx].Receive(reader); //physics state (per disconnected count)
                        GraphNode node = graph.allNodes[disconnectedIdx];
                        if(node == null) continue;
                        node.ApplyPhysicsState(prevStates[disconnectedIdx], lag);
                        nodesLastSyncTime[disconnectedIdx] = timeReceived;
                    }
                }
                
                receivedInitialSync = true;
            }
        }

        [PunRPC]
        public void NodeDestroyedRPC(int idx, Vector3 vel)
        {
            GraphNode node = graph.allNodes[idx];
            if(node != null)
                node.DestroySelf(vel);
            nodesDestroyed[idx] = true;
        }

        [PunRPC]
        public void NodeRemoveRPC(int idx)
        {
            GraphNode node = graph.allNodes[idx];
            if(node != null)
                node.RemoveSelf();
            nodesDestroyed[idx] = true;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                if(graph.connectedNodes.Count == graph.allNodes.Length)
                    return; //don't sync if graph is fully intact
                
                if(!BandwidthLimiter.instance.CanUseMoreBandwidth(BandwidthLimiter.BandwidthBudgetCategory.GRAPHS))
                    return; //using too much bandwidth - skip this sync

                Stopwatch timer = new Stopwatch();
                timer.Start();
                using MemoryStream ms = new MemoryStream();
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    //sync changed nodes
                    List<int> changedStateIndices = new();
                    for (int i = 0; i < graph.allNodes.Length; i++)
                    {
                        GraphNode node = graph.allNodes[i];
                        if (node == null)
                        {
                            if (nodesDestroyed[i])
                                continue;
                            else
                            {
                                photonView.RPC(nameof(NodeRemoveRPC), RpcTarget.All, i);
                                continue;
                            }
                        }

                        if (node.frozen) continue;

                        NetworkedPhysicsState newState = new NetworkedPhysicsState();
                        node.WritePhysicsState(ref newState);

                        if (!NetworkedPhysicsState.ChangedFromPrevious(newState, prevStates[i]))
                            continue; //state did not significantly change

                        //state did change - update it in prevStates and add its index to the list of changed state indices
                        prevStates[i] = newState;
                        changedStateIndices.Add(i);
                    }

                    if (changedStateIndices.Count > 0)
                    {
                        writer.Write((short) changedStateIndices.Count); //short indices count
                        foreach (int changedStateIndex in changedStateIndices)
                        {
                            writer.Write((short) changedStateIndex); //short index (per indices count)
                            prevStates[changedStateIndex].Send(writer); //physics state (per indices count)
                        }
                    }
                }

                byte[] data = ms.ToArray();
                timer.Stop();
                if (data.Length > 0)
                {
                    stream.SendNext(data);
                    BandwidthLimiter.instance.UseBandwidth(BandwidthLimiter.BandwidthBudgetCategory.GRAPHS, data.Length);
                    Debug.Log("[" + name + "] length of stream: " + data.Length + " (" + timer.Elapsed.TotalMilliseconds + "ms)", this);
                }
            }

            if (stream.IsReading)
            {
                try
                {
                    float lag = Mathf.Abs((float) (PhotonNetwork.Time - info.SentServerTime));
                    byte[] data = stream.ReceiveNext<byte[]>();
                    using BinaryReader reader = new BinaryReader(new MemoryStream(data));
                    
                    //sync changed
                    short indicesCount = reader.ReadInt16(); //short indices count
                    for (int i = 0; i < indicesCount; i++)
                    {
                        int idx = reader.ReadInt16(); //short index (per indices count)
                        if (receivedInitialSync)
                        {
                            prevStates[idx].Receive(reader); //physics state (per indices count)
                            GraphNode node = graph.allNodes[idx];
                            if (node == null) continue;
                            node.ApplyPhysicsState(prevStates[idx], lag);
                        }
                        else
                        {
                            NetworkedPhysicsState state = new NetworkedPhysicsState();
                            state.Receive(reader);
                            double timeReceived = Time.timeAsDouble;
                            StartCoroutine(SynchroniseOnceReady());
                            IEnumerator SynchroniseOnceReady()
                            {
                                while (!receivedInitialSync)
                                {
                                    yield return null;
                                }

                                GraphNode node = graph.allNodes[idx];
                                if(node == null || nodesLastSyncTime[idx] > timeReceived) yield break;

                                prevStates[idx] = state;
                                node.ApplyPhysicsState(prevStates[idx], lag);
                                nodesLastSyncTime[idx] = timeReceived;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }
    }
}