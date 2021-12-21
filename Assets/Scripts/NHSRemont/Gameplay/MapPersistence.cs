using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using NHSRemont.Environment.Terrain;

namespace NHSRemont.Gameplay
{
    [Serializable]
    public class MapPersistence
    {
        public const byte typeId = 255;

        //network serialised:
        public readonly List<ITerrainEvent> terrainEventsHistory = new();

        public static short Serialise(StreamBuffer outStream, object obj)
        {
            short written = 0;
            
            MapPersistence persistence = (MapPersistence)obj;
            byte[] int1 = new byte[sizeof(int)];
            int offset = 0;

            int terrainEventsCount = persistence.terrainEventsHistory.Count;
            Protocol.Serialize(terrainEventsCount, int1, ref offset);
            outStream.Write(int1, 0, sizeof(int));
            written += sizeof(int);

            for (int i = 0; i < terrainEventsCount; i++)
            {
                ITerrainEvent terrainEvent = persistence.terrainEventsHistory[i];
                
                offset = 0;
                Protocol.Serialize(terrainEvent.GetEventTypeId(), int1, ref offset);
                outStream.Write(int1, 0, sizeof(int));
                written += sizeof(int);

                written += terrainEvent.Serialise(outStream);
            }

            return written;
        }

        public static object Deserialise(StreamBuffer inStream, short length)
        {
            MapPersistence persistence = new MapPersistence();
            byte[] int1 = new byte[sizeof(int)];

            inStream.Read(int1, 0, sizeof(int));
            int offset = 0;
            Protocol.Deserialize(out int terrainEventsCount, int1, ref offset);

            persistence.terrainEventsHistory.Capacity = terrainEventsCount;
            for (int i = 0; i < terrainEventsCount; i++)
            {
                inStream.Read(int1, 0, sizeof(int));
                offset = 0;
                Protocol.Deserialize(out int id, int1, ref offset);

                switch (id)
                {
                    case 0: //Explosion
                        TerrainExplosionEvent explosionEvent = new TerrainExplosionEvent();
                        explosionEvent.Deserialise(inStream);
                        persistence.terrainEventsHistory.Add(explosionEvent);
                        break;
                    case 1: //Edit
                        TerrainEdit editEvent = new TerrainEdit();
                        editEvent.Deserialise(inStream);
                        persistence.terrainEventsHistory.Add(editEvent);
                        break;
                }
            }

            return persistence;
        }
    }
}