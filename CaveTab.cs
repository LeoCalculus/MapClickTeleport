using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace MapClickTeleport
{
    /// <summary>Cave/Mining utilities tab.</summary>
    public class CaveTab
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly Rectangle bounds;

        // Static state that persists
        public static bool NextRockIsLadder { get; set; } = false;
        public static int SkullCavernSkipLevels { get; set; } = 0;
        public static string BoostedOreType { get; set; } = "";
        public static float BoostedOreChance { get; set; } = 0f;

        // Instant mine feature
        public static bool InstantMineEnabled { get; set; } = false;
        public static int InstantMineRadius { get; set; } = 3;

        // UI Components
        private Rectangle nextLadderButton;
        private Rectangle skipLevelsButton;
        private TextBox skipLevelsInput;
        private Rectangle skipLevelsInputBounds;

        private Rectangle oreBoostButton;
        private TextBox oreTypeInput;
        private Rectangle oreTypeInputBounds;
        private TextBox oreChanceInput;
        private Rectangle oreChanceInputBounds;

        private Rectangle nukeOresButton;

        private bool isTypingSkipLevels = false;
        private bool isTypingOreType = false;
        private bool isTypingOreChance = false;

        // Ore types for reference
        private static readonly Dictionary<string, string> OreTypes = new()
        {
            {"Copper", "751"},
            {"Iron", "290"},
            {"Gold", "764"},
            {"Iridium", "765"},
            {"Radioactive", "95"},
            {"Mystic", "46"},
            {"Diamond", "2"},
            {"Ruby", "4"},
            {"Jade", "6"},
            {"Amethyst", "8"},
            {"Topaz", "10"},
            {"Emerald", "12"},
            {"Aquamarine", "14"}
        };

        public bool IsTyping => isTypingSkipLevels || isTypingOreType || isTypingOreChance;

        public CaveTab(Rectangle bounds, IModHelper helper, IMonitor monitor)
        {
            this.bounds = bounds;
            this.helper = helper;
            this.monitor = monitor;

            int buttonWidth = 200;
            int buttonHeight = 40;
            int inputWidth = 100;
            int startY = bounds.Y + 60;
            int spacing = 60;

            // Next rock = ladder button
            nextLadderButton = new Rectangle(bounds.X + 30, startY, buttonWidth + 100, buttonHeight);

            // Skip levels section
            startY += spacing + 20;
            skipLevelsButton = new Rectangle(bounds.X + 30, startY, buttonWidth, buttonHeight);
            skipLevelsInput = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null, Game1.smallFont, Game1.textColor)
            {
                X = bounds.X + 250,
                Y = startY + 5,
                Width = inputWidth,
                Text = SkullCavernSkipLevels > 0 ? SkullCavernSkipLevels.ToString() : ""
            };
            skipLevelsInputBounds = new Rectangle(skipLevelsInput.X, skipLevelsInput.Y, inputWidth, 36);

            // Ore boost section
            startY += spacing + 30;
            oreBoostButton = new Rectangle(bounds.X + 30, startY, buttonWidth, buttonHeight);

            oreTypeInput = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null, Game1.smallFont, Game1.textColor)
            {
                X = bounds.X + 250,
                Y = startY + 5,
                Width = inputWidth + 20,
                Text = BoostedOreType
            };
            oreTypeInputBounds = new Rectangle(oreTypeInput.X, oreTypeInput.Y, inputWidth + 20, 36);

            oreChanceInput = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null, Game1.smallFont, Game1.textColor)
            {
                X = bounds.X + 400,
                Y = startY + 5,
                Width = 80,
                Text = BoostedOreChance > 0 ? (BoostedOreChance * 100).ToString("F0") : ""
            };
            oreChanceInputBounds = new Rectangle(oreChanceInput.X, oreChanceInput.Y, 80, 36);

            // Nuke ores button
            startY += spacing + 30;
            nukeOresButton = new Rectangle(bounds.X + 30, startY, buttonWidth + 150, buttonHeight);
        }

        public void ReceiveLeftClick(int x, int y)
        {
            // Next ladder toggle
            if (nextLadderButton.Contains(x, y))
            {
                NextRockIsLadder = !NextRockIsLadder;
                Game1.playSound(NextRockIsLadder ? "coin" : "cancel");
                if (NextRockIsLadder)
                {
                    Game1.addHUDMessage(new HUDMessage("Next rock will spawn ladder!", HUDMessage.newQuest_type));
                }
                return;
            }

            // Skip levels input
            if (skipLevelsInputBounds.Contains(x, y))
            {
                skipLevelsInput.SelectMe();
                isTypingSkipLevels = true;
                isTypingOreType = false;
                isTypingOreChance = false;
                return;
            }

            // Skip levels apply
            if (skipLevelsButton.Contains(x, y))
            {
                if (int.TryParse(skipLevelsInput.Text, out int levels) && levels > 0)
                {
                    SkullCavernSkipLevels = levels;
                    Game1.playSound("coin");
                    Game1.addHUDMessage(new HUDMessage($"Next hole: skip {levels} levels!", HUDMessage.newQuest_type));
                }
                else
                {
                    SkullCavernSkipLevels = 0;
                    skipLevelsInput.Text = "";
                }
                return;
            }

            // Ore type input
            if (oreTypeInputBounds.Contains(x, y))
            {
                oreTypeInput.SelectMe();
                isTypingOreType = true;
                isTypingSkipLevels = false;
                isTypingOreChance = false;
                return;
            }

            // Ore chance input
            if (oreChanceInputBounds.Contains(x, y))
            {
                oreChanceInput.SelectMe();
                isTypingOreChance = true;
                isTypingSkipLevels = false;
                isTypingOreType = false;
                return;
            }

            // Ore boost apply
            if (oreBoostButton.Contains(x, y))
            {
                BoostedOreType = oreTypeInput.Text.Trim();
                if (float.TryParse(oreChanceInput.Text, out float chance))
                {
                    BoostedOreChance = Math.Clamp(chance / 100f, 0f, 1f);
                    Game1.playSound("coin");
                    Game1.addHUDMessage(new HUDMessage($"Ore boost: {BoostedOreType} +{chance}%", HUDMessage.newQuest_type));
                }
                else
                {
                    BoostedOreChance = 0f;
                }
                return;
            }

            // Nuke ores
            if (nukeOresButton.Contains(x, y))
            {
                NukeAllOres();
                return;
            }

            // Click elsewhere - deselect
            isTypingSkipLevels = false;
            isTypingOreType = false;
            isTypingOreChance = false;
        }

        public void ReceiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                isTypingSkipLevels = false;
                isTypingOreType = false;
                isTypingOreChance = false;
                skipLevelsInput.Selected = false;
                oreTypeInput.Selected = false;
                oreChanceInput.Selected = false;
            }
            else if (key == Keys.Tab)
            {
                // Cycle through inputs
                if (isTypingSkipLevels)
                {
                    isTypingSkipLevels = false;
                    skipLevelsInput.Selected = false;
                    isTypingOreType = true;
                    oreTypeInput.SelectMe();
                }
                else if (isTypingOreType)
                {
                    isTypingOreType = false;
                    oreTypeInput.Selected = false;
                    isTypingOreChance = true;
                    oreChanceInput.SelectMe();
                }
                else if (isTypingOreChance)
                {
                    isTypingOreChance = false;
                    oreChanceInput.Selected = false;
                }
            }
        }

        private void NukeAllOres()
        {
            GameLocation location = Game1.currentLocation;

            if (location is not MineShaft shaft)
            {
                Game1.addHUDMessage(new HUDMessage("Must be in a mine!", HUDMessage.error_type));
                return;
            }

            int destroyed = 0;
            int itemsDropped = 0;
            List<Vector2> toRemove = new();
            Vector2? lastRockPosition = null;

            // Find all ore/stone objects
            foreach (var kvp in location.Objects.Pairs)
            {
                var obj = kvp.Value;
                // Stone/ore object IDs: stones are typically 668, 670, 845, 846, 847, and ore nodes
                if (obj.Name.Contains("Stone") || obj.Name.Contains("Ore") ||
                    IsOreNode(obj.ItemId))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var pos in toRemove)
            {
                var obj = location.Objects[pos];
                lastRockPosition = pos;

                // Drop the corresponding items before removing
                var drops = GetOreDrops(obj.ItemId);
                foreach (var drop in drops)
                {
                    Game1.createItemDebris(
                        ItemRegistry.Create(drop.itemId, drop.amount),
                        pos * 64f,
                        Game1.random.Next(4),
                        location);
                    itemsDropped += drop.amount;
                }

                location.Objects.Remove(pos);
                destroyed++;
            }

            // Also check resource clumps (large boulders)
            for (int i = location.resourceClumps.Count - 1; i >= 0; i--)
            {
                var clump = location.resourceClumps[i];
                lastRockPosition = clump.Tile;
                var drops = GetResourceClumpDrops(clump.parentSheetIndex.Value);
                foreach (var drop in drops)
                {
                    Game1.createItemDebris(
                        ItemRegistry.Create(drop.itemId, drop.amount),
                        clump.Tile * 64f,
                        Game1.random.Next(4),
                        location);
                    itemsDropped += drop.amount;
                }
                location.resourceClumps.RemoveAt(i);
                destroyed++;
            }

            // Spawn ladder after clearing all rocks
            bool ladderSpawned = false;
            // destroy is optional, but we must check if the current location is a mine, mine => spawn ladder
            if (destroyed >= 0 && Game1.currentLocation.Name.StartsWith("UndergroundMine"))
            {
                try
                {
                    // Find a clear position for the ladder near the player
                    Vector2 playerTile = Game1.player.Tile;
                    // Vector2 ladderPos = FindClearTileForLadder(shaft, playerTile);
                    // spawn a ladder just next to player
                    Vector2 ladderPos = new Vector2(playerTile.X + 1, playerTile.Y);

                    // Method 1: Use createLadderDown which is more reliable
                    // This method places the ladder tile directly on the map
                    xTile.Dimensions.Location tileLocation = new xTile.Dimensions.Location((int)ladderPos.X, (int)ladderPos.Y);

                    // Get the tilesheet for the mine
                    var buildingsLayer = shaft.Map.GetLayer("Buildings");
                    if (buildingsLayer != null)
                    {
                        // Ladder tile index is 173 in the mines tilesheet
                        var tileSheet = shaft.Map.GetTileSheet("mine");
                        if (tileSheet != null)
                        {
                            buildingsLayer.Tiles[tileLocation] = new xTile.Tiles.StaticTile(
                                buildingsLayer,
                                tileSheet,
                                xTile.Tiles.BlendMode.Alpha,
                                173  // Ladder tile index
                            );
                            shaft.ladderHasSpawned = true;
                            ladderSpawned = true;
                            monitor.Log($"Created ladder tile at {ladderPos}", LogLevel.Debug);
                        }
                    }

                    // Fallback: try the game's method
                    if (!ladderSpawned)
                    {
                        shaft.createLadderAt(ladderPos, "hut");
                        shaft.ladderHasSpawned = true;
                        ladderSpawned = true;
                        monitor.Log($"Used createLadderAt at {ladderPos}", LogLevel.Debug);
                    }
                }
                catch (Exception ex)
                {
                    monitor.Log($"Failed to spawn ladder: {ex.Message}", LogLevel.Warn);
                }
            }

            Game1.playSound("explosion");
            string ladderMsg = ladderSpawned ? " Ladder appeared!" : "";
            Game1.addHUDMessage(new HUDMessage($"Destroyed {destroyed} rocks, dropped {itemsDropped} items!{ladderMsg}", HUDMessage.newQuest_type));
            monitor.Log($"Nuked {destroyed} ores, dropped {itemsDropped} items in {location.Name}", LogLevel.Debug);
        }

        private Vector2 FindClearTileForLadder(MineShaft shaft, Vector2 preferredPos)
        {
            // Try the preferred position first
            if (IsTileClearForLadder(shaft, preferredPos))
                return preferredPos;

            // Search in a spiral pattern around preferred position
            for (int radius = 1; radius <= 10; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                        Vector2 testPos = new Vector2(preferredPos.X + dx, preferredPos.Y + dy);
                        if (IsTileClearForLadder(shaft, testPos))
                            return testPos;
                    }
                }
            }

            return preferredPos; // Fallback
        }

        private bool IsTileClearForLadder(MineShaft shaft, Vector2 pos)
        {
            // Check if position is valid and clear
            if (pos.X < 1 || pos.Y < 1) return false;
            if (shaft.Objects.ContainsKey(pos)) return false;
            if (shaft.terrainFeatures.ContainsKey(pos)) return false;

            try
            {
                // Check if tile is passable
                return shaft.isTilePassable(new xTile.Dimensions.Location((int)pos.X, (int)pos.Y), Game1.viewport);
            }
            catch
            {
                return true;
            }
        }

        private List<(string itemId, int amount)> GetOreDrops(string nodeId)
        {
            var drops = new List<(string itemId, int amount)>();
            int baseAmount = Game1.random.Next(1, 4);

            // Map ore node IDs to their drop items
            switch (nodeId)
            {
                // Copper ore nodes
                case "751":
                    drops.Add(("(O)378", baseAmount)); // Copper Ore
                    break;
                // Iron ore nodes
                case "290":
                    drops.Add(("(O)380", baseAmount)); // Iron Ore
                    break;
                // Gold ore nodes
                case "764":
                    drops.Add(("(O)384", baseAmount)); // Gold Ore
                    break;
                // Iridium ore nodes
                case "765":
                    drops.Add(("(O)386", baseAmount)); // Iridium Ore
                    break;
                // Radioactive ore
                case "95":
                    drops.Add(("(O)909", baseAmount)); // Radioactive Ore
                    break;
                // Mystic stone
                case "46":
                    drops.Add(("(O)386", Game1.random.Next(1, 3))); // Iridium
                    drops.Add(("(O)74", 1)); // Prismatic Shard (rare)
                    break;
                // Gem nodes
                case "2": drops.Add(("(O)72", 1)); break; // Diamond
                case "4": drops.Add(("(O)64", 1)); break; // Ruby
                case "6": drops.Add(("(O)70", 1)); break; // Jade
                case "8": drops.Add(("(O)66", 1)); break; // Amethyst
                case "10": drops.Add(("(O)68", 1)); break; // Topaz
                case "12": drops.Add(("(O)60", 1)); break; // Emerald
                case "14": drops.Add(("(O)62", 1)); break; // Aquamarine
                // Geode nodes
                case "75": drops.Add(("(O)535", 1)); break; // Geode
                case "76": drops.Add(("(O)536", 1)); break; // Frozen Geode
                case "77": drops.Add(("(O)537", 1)); break; // Magma Geode
                // Regular stones - drop stone
                default:
                    drops.Add(("(O)390", Game1.random.Next(1, 3))); // Stone
                    // Chance for coal
                    if (Game1.random.NextDouble() < 0.05)
                        drops.Add(("(O)382", 1)); // Coal
                    break;
            }

            return drops;
        }

        private List<(string itemId, int amount)> GetResourceClumpDrops(int clumpId)
        {
            var drops = new List<(string itemId, int amount)>();

            switch (clumpId)
            {
                case 600: // Large Stump
                case 602:
                    drops.Add(("(O)388", Game1.random.Next(6, 12))); // Wood
                    drops.Add(("(O)709", Game1.random.Next(1, 3))); // Hardwood
                    break;
                case 672: // Boulder
                    drops.Add(("(O)390", Game1.random.Next(10, 20))); // Stone
                    break;
                case 752: // Copper boulder
                    drops.Add(("(O)378", Game1.random.Next(6, 12))); // Copper
                    break;
                case 754: // Iron boulder
                    drops.Add(("(O)380", Game1.random.Next(6, 12))); // Iron
                    break;
                case 756: // Gold boulder
                case 758:
                    drops.Add(("(O)384", Game1.random.Next(6, 12))); // Gold
                    break;
                default:
                    drops.Add(("(O)390", Game1.random.Next(5, 15))); // Stone
                    break;
            }

            return drops;
        }

        private bool IsOreNode(string itemId)
        {
            // Common ore node IDs
            string[] oreIds = { "751", "290", "764", "765", "95", "843", "844", "845", "846", "847",
                               "668", "670", "760", "762", "32", "34", "36", "38", "40", "42",
                               "44", "46", "48", "50", "52", "54", "56", "58" };
            return oreIds.Contains(itemId);
        }

        public void Draw(SpriteBatch b)
        {
            // Title
            string title = "Cave / Mining Utilities";
            Vector2 titleSize = Game1.smallFont.MeasureString(title);
            b.DrawString(Game1.smallFont, title,
                new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + 20),
                Color.Gold);

            // Current location info
            string locInfo = $"Current: {Game1.currentLocation?.Name ?? "Unknown"}";
            if (Game1.currentLocation is MineShaft shaft)
            {
                locInfo += $" (Level {shaft.mineLevel})";
            }
            b.DrawString(Game1.smallFont, locInfo,
                new Vector2(bounds.X + 30, bounds.Y + 45),
                Color.LightGray);

            // === SECTION 1: Next Rock = Ladder ===
            DrawButton(b, nextLadderButton,
                NextRockIsLadder ? "✓ Next Rock → Ladder (ON)" : "Next Rock → Ladder (OFF)",
                NextRockIsLadder ? Color.LightGreen : Color.White);

            // === SECTION 2: Skip Levels ===
            int sectionY = skipLevelsButton.Y - 25;
            b.DrawString(Game1.smallFont, "Skull Cavern - Skip Levels on Next Hole:",
                new Vector2(bounds.X + 30, sectionY),
                Color.Orange);

            DrawButton(b, skipLevelsButton,
                SkullCavernSkipLevels > 0 ? $"Skip {SkullCavernSkipLevels} Levels" : "Set Skip",
                SkullCavernSkipLevels > 0 ? Color.LightGreen : Color.White);

            b.DrawString(Game1.smallFont, "Levels:",
                new Vector2(skipLevelsInput.X - 55, skipLevelsInput.Y + 8),
                Game1.textColor);
            skipLevelsInput.Draw(b);

            // === SECTION 3: Ore Boost ===
            sectionY = oreBoostButton.Y - 25;
            b.DrawString(Game1.smallFont, "Boost Ore Generation (next mine entry):",
                new Vector2(bounds.X + 30, sectionY),
                Color.Orange);

            DrawButton(b, oreBoostButton,
                BoostedOreChance > 0 ? $"Boosting: {BoostedOreType}" : "Apply Boost",
                BoostedOreChance > 0 ? Color.LightGreen : Color.White);

            b.DrawString(Game1.smallFont, "Ore:",
                new Vector2(oreTypeInput.X - 35, oreTypeInput.Y + 8),
                Game1.textColor);
            oreTypeInput.Draw(b);

            b.DrawString(Game1.smallFont, "%:",
                new Vector2(oreChanceInput.X - 25, oreChanceInput.Y + 8),
                Game1.textColor);
            oreChanceInput.Draw(b);

            // === SECTION 4: Nuke Ores ===
            sectionY = nukeOresButton.Y - 25;
            b.DrawString(Game1.smallFont, "Clear Current Level:",
                new Vector2(bounds.X + 30, sectionY),
                Color.Orange);

            DrawButton(b, nukeOresButton, "💥 Destroy All Rocks/Ores", new Color(255, 100, 100));

            // === Help Text ===
            int helpY = bounds.Bottom - 120;
            b.DrawString(Game1.smallFont, "Ore Types: Copper, Iron, Gold, Iridium, Radioactive, Diamond, etc.",
                new Vector2(bounds.X + 30, helpY),
                Color.Gray * 0.8f);

            b.DrawString(Game1.smallFont, "Note: Ladder/Skip features activate on next rock break.",
                new Vector2(bounds.X + 30, helpY + 22),
                Color.Gray * 0.8f);

            // Status indicators
            int statusY = bounds.Bottom - 60;
            string status = "Active Effects: ";
            List<string> effects = new();
            if (NextRockIsLadder) effects.Add("Ladder Ready");
            if (SkullCavernSkipLevels > 0) effects.Add($"Skip {SkullCavernSkipLevels}");
            if (BoostedOreChance > 0) effects.Add($"{BoostedOreType} +{BoostedOreChance * 100:F0}%");

            if (effects.Count == 0) effects.Add("None");
            status += string.Join(" | ", effects);

            b.DrawString(Game1.smallFont, status,
                new Vector2(bounds.X + 30, statusY),
                Color.Yellow);
        }

        private void DrawButton(SpriteBatch b, Rectangle rect, string text, Color color)
        {
            bool hovered = rect.Contains(Game1.getMouseX(), Game1.getMouseY());

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                rect.X, rect.Y, rect.Width, rect.Height,
                hovered ? Color.Wheat : color, 4f, false);

            Vector2 textSize = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2),
                Color.Black);
        }
    }
}

