using ExitGames.Client.Photon;
using NHSRemont.Gameplay;

namespace NHSRemont.Environment.Terrain
{
    public struct TerrainExplosionEvent : ITerrainEvent
    {
        public int terrainIndex;
        public ExplosionInfo explosion;

        public TerrainExplosionEvent(int terrainIndex, ExplosionInfo explosion)
        {
            this.terrainIndex = terrainIndex;
            this.explosion = explosion;
        }

        public bool Apply(ReactiveTerrain terrain)
        {
            return terrain.ModifyTerrain(explosion).modified;
        }

        public int AffectedTerrain()
        {
            return terrainIndex;
        }

        public int GetEventTypeId()
        {
            return 0;
        }

        public short Serialise(StreamBuffer outStream)
        {
            short written = 0;

            outStream.WriteByte((byte)terrainIndex);
            written++;

            written += explosion.Serialise(outStream);

            return written;
        }

        public void Deserialise(StreamBuffer inStream)
        {
            terrainIndex = inStream.ReadByte();
            
            explosion = ExplosionInfo.Deserialise(inStream);
        }
    }
}