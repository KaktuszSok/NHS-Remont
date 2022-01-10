using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;

namespace NHSRemont.Environment.Terrain
{
    /// <summary>
    /// Represents a change to the terrain.
    /// </summary>
    public struct TerrainEdit : ITerrainEvent
    {
        public struct ModifiedDetailsLayer
        {
            public int layer;
            public int[,] values;
        }
        
        public int terrainIndex;

        public (int startX, int startZ, float[,] values) heightModification;
        public (int startX, int startZ, float[,,] values) coloursModification;
        public (int startX, int startZ, ModifiedDetailsLayer[] layersAndValues) detailsModification;
        
        /// <summary>
        /// Finds the appropriate terrain and applies the edit to it
        /// </summary>
        public void ApplyToScene()
        {
            ReactiveTerrain terrain = Object.FindObjectsOfType<ReactiveTerrain>()[terrainIndex];
            Apply(terrain);
        }
        
        public bool Apply(ReactiveTerrain terrain)
        {
            if(heightModification.values != null)
                terrain.terrainData.SetHeights(heightModification.startX, heightModification.startZ, heightModification.values);
            
            if(coloursModification.values != null)
                terrain.terrainData.SetAlphamaps(coloursModification.startX, coloursModification.startZ, coloursModification.values);
            
            if(detailsModification.layersAndValues != null)
                foreach (ModifiedDetailsLayer change in detailsModification.layersAndValues)
                {
                    terrain.terrainData.SetDetailLayer(detailsModification.startX, detailsModification.startZ, change.layer, change.values);
                }

            return true;
        }

        public int AffectedTerrain()
        {
            return terrainIndex;
        }

        public int GetEventTypeId()
        {
            return 1;
        }

        public static void ApplyEditsToScene(IEnumerable<TerrainEdit> edits)
        {
            ReactiveTerrain[] terrains = Object.FindObjectsOfType<ReactiveTerrain>();
            foreach (TerrainEdit terrainEdit in edits)
            {
                terrainEdit.Apply(terrains[terrainEdit.terrainIndex]);
            }
        }
        
        public short Serialise(StreamBuffer outStream)
        {
            short detailModsSize = 1; //array size (as byte)
            foreach (ModifiedDetailsLayer entry in detailsModification.layersAndValues)
            {
                detailModsSize++; //layer index (as byte)
                detailModsSize += sizeof(short) * 2; //l0,l1 (as short)
                detailModsSize += (short)(entry.values.GetLength(0) * entry.values.GetLength(1) * sizeof(byte)); //values (as bytes)
            }
            
            //terrainIndex (as byte) +
            //(startX + startZ)*3 +
            //heightModValues.l0,l1 + heightModValues +
            //colourModValues.l0,l1,l2 + colourModValues +
            //detailModValues.l0 detailModValues
            short size = (short) (
                1 +
                sizeof(short) * 6 +
                sizeof(short) * 2 + heightModification.values.GetLength(0) * heightModification.values.GetLength(1) * sizeof(float) +
                sizeof(short) * 2 + coloursModification.values.GetLength(0) * coloursModification.values.GetLength(1) * coloursModification.values.GetLength(2) * sizeof(float) +
                detailModsSize
            );

            byte[] bytes = new byte[size];
            int offset = 0;
            //terrain index
            bytes[offset++] = (byte) terrainIndex;
            //start points
            Protocol.Serialize((short) heightModification.startX, bytes, ref offset);
            Protocol.Serialize((short) heightModification.startZ, bytes, ref offset);
            
            Protocol.Serialize((short) coloursModification.startX, bytes, ref offset);
            Protocol.Serialize((short) coloursModification.startZ, bytes, ref offset);
            
            Protocol.Serialize((short) detailsModification.startX, bytes, ref offset);
            Protocol.Serialize((short) detailsModification.startZ, bytes, ref offset);

            //height mod
            short l0 = (short) heightModification.values.GetLength(0);
            short l1 = (short) heightModification.values.GetLength(1);
            Protocol.Serialize(l0, bytes, ref offset);
            Protocol.Serialize(l1, bytes, ref offset);
            for (int i = 0; i < l0; i++)
            {
                for (int j = 0; j < l1; j++)
                {
                    Protocol.Serialize(heightModification.values[i,j], bytes, ref offset);
                }
            }
            
            
            //colours mod
            l0 = (short) coloursModification.values.GetLength(0);
            l1 = (short) coloursModification.values.GetLength(1);
            short l2 = (short) coloursModification.values.GetLength(2);

            Protocol.Serialize(l0, bytes, ref offset);
            Protocol.Serialize(l1, bytes, ref offset);
            Protocol.Serialize(l2, bytes, ref offset);
            for (int i = 0; i < l0; i++)
            {
                for (int j = 0; j < l1; j++)
                {
                    for (int k = 0; k < l2; k++)
                    {
                        Protocol.Serialize(coloursModification.values[i,j,k], bytes, ref offset);
                    }
                }
            }
            
            //details mod
            byte l0_byte = (byte) detailsModification.layersAndValues.Length;
            bytes[offset++] = l0_byte;
            for (int i = 0; i < l0_byte; i++)
            {
                ModifiedDetailsLayer entry = detailsModification.layersAndValues[i];
                bytes[offset++] = (byte)entry.layer;
                l0 = (short)entry.values.GetLength(0);
                l1 = (short)entry.values.GetLength(1);
                
                Protocol.Serialize(l0, bytes, ref offset);
                Protocol.Serialize(l1, bytes, ref offset);

                for (int j = 0; j < l0; j++)
                {
                    for (int k = 0; k < l1; k++)
                    {
                        bytes[offset++] = (byte)entry.values[j,k];
                    }
                }
            }

            outStream.Write(bytes, 0, size);
            return size;
        }
        
        public void Deserialise(StreamBuffer inStream)
        {
            //terrain index
            terrainIndex = inStream.ReadByte();

            //start points
            byte[] short6 = new byte[sizeof(short)*6];
            inStream.Read(short6, 0, sizeof(short)*6);
            int offset = 0;
            Protocol.Deserialize(out heightModification.startX, short6, ref offset);
            Protocol.Deserialize(out heightModification.startZ, short6, ref offset);
            
            Protocol.Deserialize(out coloursModification.startX, short6, ref offset);
            Protocol.Deserialize(out coloursModification.startZ, short6, ref offset);
            
            Protocol.Deserialize(out detailsModification.startX, short6, ref offset);
            Protocol.Deserialize(out detailsModification.startZ, short6, ref offset);
            
            //height mod
            inStream.Read(short6, 0, sizeof(short) * 2);
            offset = 0;
            Protocol.Deserialize(out short l0, short6, ref offset);
            Protocol.Deserialize(out short l1, short6, ref offset);

            heightModification.values = new float[l0, l1];
            byte[] floatN = new byte[sizeof(float)*l0*l1];
            inStream.Read(floatN, 0, sizeof(float)*l0*l1);
            offset = 0;
            for (int i = 0; i < l0; i++)
            {
                for (int j = 0; j < l1; j++)
                {
                    Protocol.Deserialize(out heightModification.values[i,j], floatN, ref offset);
                }
            }
            
            //colours mod
            inStream.Read(short6, 0, sizeof(short) * 3);
            offset = 0;
            Protocol.Deserialize(out l0, short6, ref offset);
            Protocol.Deserialize(out l1, short6, ref offset);
            Protocol.Deserialize(out short l2, short6, ref offset);

            coloursModification.values = new float[l0, l1, l2];
            floatN = new byte[sizeof(float)*l0*l1*l2];
            inStream.Read(floatN, 0, sizeof(float)*l0*l1*l2);
            offset = 0;
            for (int i = 0; i < l0; i++)
            {
                for (int j = 0; j < l1; j++)
                {
                    for (int k = 0; k < l2; k++)
                    {
                        Protocol.Deserialize(out coloursModification.values[i,j,k], floatN, ref offset);
                    }
                }
            }

            //details mod
            byte l0_byte = inStream.ReadByte();
            detailsModification.layersAndValues = new ModifiedDetailsLayer[l0_byte];
            for (int i = 0; i < l0_byte; i++)
            {
                detailsModification.layersAndValues[i].layer = inStream.ReadByte();
                
                inStream.Read(short6, 0, sizeof(short) * 2);
                offset = 0;
                Protocol.Deserialize(out l0, short6, ref offset);
                Protocol.Deserialize(out l1, short6, ref offset);
                detailsModification.layersAndValues[i].values = new int[l0, l1];
                for (int j = 0; j < l0; j++)
                {
                    for (int k = 0; k < l1; k++)
                    {
                        detailsModification.layersAndValues[i].values[j,k] = inStream.ReadByte();
                    }
                }
            }
        }
    }
}