namespace MapClickTeleport
{
    /// <summary>Represents a saved teleport point.</summary>
    public class SavedPoint
    {
        public string Name { get; set; } = "";
        public string LocationName { get; set; } = "";
        public int TileX { get; set; }
        public int TileY { get; set; }

        public SavedPoint() { }

        public SavedPoint(string name, string locationName, int tileX, int tileY)
        {
            Name = name;
            LocationName = locationName;
            TileX = tileX;
            TileY = tileY;
        }
    }

    /// <summary>Mod data that gets saved.</summary>
    public class ModData
    {
        public List<SavedPoint> SavedPoints { get; set; } = new();
    }
}

