using ExitGames.Client.Photon;

namespace NHSRemont.Environment.Terrain
{
    /// <summary>
    /// An event which deterministically modifies the terrain.
    /// Applying a list of terrain events in the correct order should always yield the same result on the same starting terrain, regardless of the state of the rest of the map.
    /// That is to say, terrain events should not behave differently based on dynamic external factors such as destructible buildings/movable vehicles blocking an explosion.
    /// </summary>
    public interface ITerrainEvent
    {
        /// <summary>
        /// Applies the event to the given terrain
        /// </summary>
        /// <returns>Whether the event did anything to this terrain</returns>
        public bool Apply(ReactiveTerrain terrain);

        /// <returns>The index of the terrain affected by this edit.</returns>
        public int AffectedTerrain();

        /// <summary>
        /// Gets the ID of this event type, for serialisation purposes.
        /// </summary>
        public int GetEventTypeId();

        /// <returns>Amount of bytes written to the stream</returns>
        public short Serialise(StreamBuffer outStream);
        
        public void Deserialise(StreamBuffer inStream);
    }
}