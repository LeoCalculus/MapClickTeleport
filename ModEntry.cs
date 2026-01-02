using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;

namespace MapClickTeleport
{
    /// <summary>
    /// Stardew Utilities Mod with ImGui UI
    /// 
    /// Controls:
    /// - Right Shift: Open Stardew Utilities menu (ImGui)
    /// - Shift + Right Click (when enabled): Click teleport to any tile
    /// </summary>
    public class ModEntry : Mod
    {
        private ModData modData = new();

        // Click teleport feature
        public static bool ClickTeleportEnabled { get; set; } = false;

        // ImGui Renderer (shared)
        public static ImGuiRenderer? Renderer { get; private set; }
        private bool _imGuiInitialized = false;

        // Keyboard state for floating window text input
        private KeyboardState _prevKeyboard;

        public override void Entry(IModHelper helper)
        {
            // Initialize Harmony for all patches
            var harmony = new Harmony(this.ModManifest.UniqueID);
            HUDHider.ApplyPatches(harmony);
            OPFeatures.ApplyPatches(harmony, Monitor, Helper);

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
            helper.Events.Player.Warped += OnPlayerWarped;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Display.Rendered += OnRendered;

            this.Monitor.Log("Stardew Utilities loaded! (ImGui Edition)", LogLevel.Info);
            this.Monitor.Log("  - Press Right Shift to open menu", LogLevel.Info);
            this.Monitor.Log("  - Harmony patches applied for HUD hiding and OP features", LogLevel.Debug);
        }

        private void InitializeImGui()
        {
            if (_imGuiInitialized) return;

            try
            {
                Renderer = new ImGuiRenderer(Game1.graphics.GraphicsDevice);
                _imGuiInitialized = true;
                Monitor.Log("ImGui initialized successfully!", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to initialize ImGui: {ex.Message}", LogLevel.Error);
                _imGuiInitialized = false;
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            // Initialize ImGui lazily (needs graphics context)
            if (!_imGuiInitialized && Game1.graphics?.GraphicsDevice != null)
            {
                InitializeImGui();
            }
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            // Stamina/Health bar hiding is now handled by Harmony transpiler in HUDHider
            // No need to draw cover rectangles anymore
        }

        private void OnRendered(object? sender, RenderedEventArgs e)
        {
            // Draw floating windows when no menu is open (or menu is not our ImGuiMenu)
            if (!Context.IsWorldReady || Renderer == null) return;
            if (Game1.activeClickableMenu is ImGuiMenu) return; // ImGuiMenu handles its own rendering

            // Only draw if any floating window is visible
            if (!FloatingWindows.ShowPlayerStats &&
                !FloatingWindows.ShowTimeWidget &&
                !FloatingWindows.ShowInventoryWidget &&
                !FloatingWindows.ShowFPSCounter)
                return;

            // Handle text input for floating windows
            HandleFloatingTextInput();

            // Render floating windows
            Renderer.BeginLayout(new GameTime());
            FloatingWindows.Draw();
            Renderer.EndLayout();
        }

        private void HandleFloatingTextInput()
        {
            var keyboard = Keyboard.GetState();

            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (keyboard.IsKeyDown(key) && !_prevKeyboard.IsKeyDown(key))
                {
                    char? c = KeyToChar(key, keyboard);
                    if (c.HasValue && Renderer != null)
                    {
                        Renderer.AddInputCharacter(c.Value);
                    }
                }
            }

            _prevKeyboard = keyboard;
        }

        private char? KeyToChar(Keys key, KeyboardState keyboard)
        {
            bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(c) : c;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            return key switch
            {
                Keys.Space => ' ',
                _ => null
            };
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            // Only the host can read/write save data
            // Farmhands (clients) will use default empty data
            if (Context.IsMainPlayer)
            {
                modData = Helper.Data.ReadSaveData<ModData>("StardewUtilities.SavedPoints") ?? new ModData();
                this.Monitor.Log($"Loaded {modData.SavedPoints.Count} saved teleport points", LogLevel.Debug);
            }
            else
            {
                modData = new ModData();
                this.Monitor.Log("Farmhand detected - using default mod data (save data only available to host)", LogLevel.Debug);
            }
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            // Only the host can save data
            if (Context.IsMainPlayer)
            {
                Helper.Data.WriteSaveData("StardewUtilities.SavedPoints", modData);
            }
        }

        private void SaveModData(ModData data)
        {
            modData = data;
            // Only the host can save data
            if (Context.IsWorldReady && Context.IsMainPlayer)
            {
                Helper.Data.WriteSaveData("StardewUtilities.SavedPoints", modData);
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // CRITICAL: Suppress most keyboard input when our ImGui menu is open
            // This prevents T, /, and other keys from triggering game actions like chat
            // EXCEPT: ESC key must pass through so the menu can close itself
            if (Game1.activeClickableMenu is ImGuiMenu)
            {
                // Don't suppress ESC - let it pass through to close the menu
                if (e.Button == SButton.Escape)
                {
                    // Let ESC through to ImGuiMenu.receiveKeyPress
                    return;
                }

                // Suppress all other keyboard keys (not mouse buttons) to prevent game input
                if (e.Button.TryGetKeyboard(out _))
                {
                    Helper.Input.Suppress(e.Button);
                    return;
                }
            }

            // Right Shift = Open Stardew Utilities Menu (ImGui)
            if (e.Button == SButton.RightShift && Game1.activeClickableMenu == null)
            {
                Helper.Input.Suppress(e.Button);

                if (Renderer != null)
                {
                    Game1.activeClickableMenu = new ImGuiMenu(Renderer, modData, SaveModData, Helper, Monitor);
                    Game1.playSound("bigSelect");
                }
                else
                {
                    Monitor.Log("ImGui not initialized yet!", LogLevel.Warn);
                }
                return;
            }

            // Door Bypass: When enabled, bypass door restrictions (only at actual doors)
            if (RelationshipTab.DoorBypassEnabled && e.Button.IsActionButton())
            {
                Vector2 playerTile = Game1.player.Tile;
                Vector2 facingTile = playerTile + GetFacingDirection();

                string? targetLocation = GetDoorTarget(Game1.currentLocation, (int)facingTile.X, (int)facingTile.Y);
                if (targetLocation != null)
                {
                    Helper.Input.Suppress(e.Button);
                    var (entryX, entryY) = GetLocationEntry(targetLocation);
                    Game1.warpFarmer(targetLocation, entryX, entryY, false);
                    Game1.playSound("doorClose");
                    Monitor.Log($"Door bypass: Entered {targetLocation}", LogLevel.Debug);
                    return;
                }
            }

            // Click Teleport: Shift + Right Click (when enabled and no menu open)
            if (ClickTeleportEnabled && e.Button == SButton.MouseRight &&
                Game1.activeClickableMenu == null &&
                (Helper.Input.IsDown(SButton.LeftShift) || Helper.Input.IsDown(SButton.RightShift)))
            {
                Helper.Input.Suppress(e.Button);

                Vector2 cursorTile = Game1.currentCursorTile;
                int tileX = (int)cursorTile.X;
                int tileY = (int)cursorTile.Y;

                GameLocation location = Game1.currentLocation;
                if (location != null && IsTileWalkable(location, tileX, tileY))
                {
                    Game1.player.setTileLocation(cursorTile);
                    Game1.playSound("powerup");
                    // Monitor.Log($"Click teleported to ({tileX}, {tileY})", LogLevel.Debug);
                }
                else if (location != null)
                {
                    Vector2? nearestTile = FindNearestWalkableTile(location, tileX, tileY);
                    if (nearestTile.HasValue)
                    {
                        Game1.player.setTileLocation(nearestTile.Value);
                        Game1.playSound("powerup");
                    }
                    else
                    {
                        Game1.playSound("cancel");
                        Game1.addHUDMessage(new HUDMessage("Cannot teleport there!", HUDMessage.error_type));
                    }
                }
            }
        }

        private bool IsTileWalkable(GameLocation location, int x, int y)
        {
            if (x < 0 || y < 0 || location.Map == null) return false;
            var layer = location.Map.Layers[0];
            if (x >= layer.LayerWidth || y >= layer.LayerHeight) return false;

            try
            {
                Vector2 tile = new(x, y);
                if (!location.isTilePassable(new xTile.Dimensions.Location(x * 64, y * 64), Game1.viewport)) return false;
                if (location.isWaterTile(x, y)) return false;
                if (location.Objects.ContainsKey(tile)) return false;
                return true;
            }
            catch { return false; }
        }

        private Vector2? FindNearestWalkableTile(GameLocation location, int targetX, int targetY)
        {
            for (int radius = 1; radius <= 5; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                        int x = targetX + dx, y = targetY + dy;
                        if (IsTileWalkable(location, x, y))
                            return new Vector2(x, y);
                    }
                }
            }
            return null;
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Apply HUD hiding every tick
            HUDHider.UpdateHUD();

            // Update OP features (instant kill on contact, auto pickup, etc.)
            OPFeatures.UpdateTick();

            // GOD MODE: Keep player health at max (true invincibility)
            if (BuffTab.IsInvincible || OPFeatures.InfiniteHealth)
            {
                if (Game1.player.health < Game1.player.maxHealth)
                {
                    Game1.player.health = Game1.player.maxHealth;
                }
            }

            // Instant Mine: Auto-mine rocks near player
            if (CaveTab.InstantMineEnabled && Game1.currentLocation is MineShaft mineShaft)
            {
                AutoMineNearbyRocks(mineShaft);
            }

            // Check if player just broke a rock and NextRockIsLadder is active
            if (CaveTab.NextRockIsLadder && Game1.currentLocation is MineShaft shaft)
            {
                if (Game1.player.UsingTool && Game1.player.CurrentTool is Pickaxe)
                {
                    // The ladder spawn is handled in ObjectListChanged
                }
            }
        }

        private void AutoMineNearbyRocks(MineShaft shaft)
        {
            var playerTile = Game1.player.Tile;
            int radius = CaveTab.InstantMineRadius;
            var toRemove = new List<Vector2>();

            foreach (var obj in shaft.Objects.Pairs)
            {
                float dist = Vector2.Distance(obj.Key, playerTile);
                if (dist <= radius)
                {
                    if (obj.Value.Name.Contains("Stone") || obj.Value.Name.Contains("Ore") ||
                        obj.Value.Name.Contains("Node") || IsRockNode(obj.Value.ItemId))
                    {
                        // Drop appropriate loot
                        var drops = GetRockDrops(obj.Value.ItemId, obj.Value.Name, shaft.mineLevel);
                        foreach (var drop in drops)
                        {
                            Game1.createItemDebris(drop, obj.Key * 64f, Game1.random.Next(4), shaft);
                        }
                        toRemove.Add(obj.Key);
                    }
                }
            }

            foreach (var pos in toRemove)
            {
                shaft.Objects.Remove(pos);
            }
        }

        private List<Item> GetRockDrops(string itemId, string name, int mineLevel)
        {
            var drops = new List<Item>();

            // CORRECT ore node to drop mapping
            var dropMap = new Dictionary<string, (string itemId, int min, int max)>
            {
                // Copper nodes
                {"751", ("378", 1, 3)}, // Copper Ore
                {"849", ("378", 2, 5)}, // Copper (Skull Cavern)
                // Iron nodes
                {"290", ("380", 1, 3)}, // Iron Ore (correct!)
                {"850", ("380", 2, 5)}, // Iron (Skull Cavern)
                // Gold nodes
                {"764", ("384", 1, 3)}, // Gold Ore
                {"851", ("384", 2, 5)}, // Gold (Skull Cavern)
                // Iridium nodes (765 = Iridium, NOT Iron!)
                {"765", ("386", 1, 3)}, // Iridium Ore
                {"843", ("386", 1, 3)}, {"844", ("386", 1, 3)}, // Iridium variants
                {"845", ("386", 1, 4)}, {"846", ("386", 1, 4)}, {"847", ("386", 2, 5)}, // Iridium variants
                // Radioactive
                {"95", ("909", 1, 2)}, // Radioactive Ore
                // Gems
                {"2", ("72", 1, 1)}, {"4", ("64", 1, 1)}, {"6", ("70", 1, 1)}, // Diamond, Ruby, Jade
                {"8", ("66", 1, 1)}, {"10", ("68", 1, 1)}, {"12", ("60", 1, 1)}, {"14", ("62", 1, 1)}, // More gems
                // Geodes
                {"75", ("535", 1, 1)}, {"76", ("536", 1, 1)}, {"77", ("537", 1, 1)},
                // Quartz
                {"668", ("80", 1, 1)}, {"670", ("82", 1, 1)},
                // Mystic Stone
                {"760", ("386", 1, 4)}, {"762", ("386", 2, 5)},
                // Cinder Shard (Volcano)
                {"816", ("848", 1, 3)}, {"817", ("848", 2, 4)},
            };

            if (dropMap.TryGetValue(itemId, out var info))
            {
                drops.Add(ItemRegistry.Create(info.itemId, Game1.random.Next(info.min, info.max + 1)));
            }
            else if (name.Contains("Copper")) drops.Add(ItemRegistry.Create("378", Game1.random.Next(1, 4)));
            else if (name.Contains("Iron")) drops.Add(ItemRegistry.Create("380", Game1.random.Next(1, 4)));
            else if (name.Contains("Gold")) drops.Add(ItemRegistry.Create("384", Game1.random.Next(1, 4)));
            else if (name.Contains("Iridium")) drops.Add(ItemRegistry.Create("386", Game1.random.Next(1, 4)));
            else drops.Add(ItemRegistry.Create("390", Game1.random.Next(1, 3))); // Stone

            if (Game1.random.NextDouble() < 0.05) drops.Add(ItemRegistry.Create("382", 1)); // Coal
            if (Game1.random.NextDouble() < 0.03)
            {
                string geodeId = mineLevel < 40 ? "535" : mineLevel < 80 ? "536" : mineLevel < 120 ? "537" : "749";
                drops.Add(ItemRegistry.Create(geodeId, 1));
            }

            return drops;
        }

        private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Location is MineShaft shaft && e.Removed.Any())
            {
                if (CaveTab.NextRockIsLadder)
                {
                    foreach (var removed in e.Removed)
                    {
                        if (removed.Value.Name.Contains("Stone") || IsRockNode(removed.Value.ItemId))
                        {
                            Vector2 pos = removed.Key;
                            shaft.createLadderAt(pos);
                            CaveTab.NextRockIsLadder = false;
                            Game1.addHUDMessage(new HUDMessage("Ladder spawned!", HUDMessage.newQuest_type));
                            Monitor.Log($"Spawned ladder at {pos} in {shaft.Name}", LogLevel.Debug);
                            break;
                        }
                    }
                }
            }
        }

        private void OnPlayerWarped(object? sender, WarpedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (CaveTab.SkullCavernSkipLevels > 0)
            {
                if (e.NewLocation is MineShaft newShaft)
                {
                    if (newShaft.mineLevel >= 121)
                    {
                        int currentLevel = newShaft.mineLevel;
                        int targetLevel = currentLevel + CaveTab.SkullCavernSkipLevels;

                        Game1.warpFarmer($"UndergroundMine{targetLevel}",
                            Game1.player.TilePoint.X,
                            Game1.player.TilePoint.Y,
                            false);

                        CaveTab.SkullCavernSkipLevels = 0;
                        Game1.addHUDMessage(new HUDMessage($"Skipped to level {targetLevel - 120}!", HUDMessage.newQuest_type));
                    }
                }
            }
        }

        private bool IsRockNode(string itemId)
        {
            string[] rockIds = {
                "751", "290", "764", "765", "95", "843", "844", "845", "846", "847",
                "668", "670", "760", "762", "32", "34", "36", "38", "40", "42",
                "44", "46", "48", "50", "52", "54", "56", "58",
                "2", "4", "6", "8", "10", "12", "14",
                "75", "76", "77"
            };
            return rockIds.Contains(itemId);
        }

        private Vector2 GetFacingDirection()
        {
            return Game1.player.FacingDirection switch
            {
                0 => new Vector2(0, -1),
                1 => new Vector2(1, 0),
                2 => new Vector2(0, 1),
                3 => new Vector2(-1, 0),
                _ => Vector2.Zero
            };
        }

        private string? GetDoorTarget(GameLocation location, int x, int y)
        {
            if (location?.Map == null) return null;

            try
            {
                var tile = location.Map.GetLayer("Buildings")?.Tiles[x, y];
                if (tile == null) return null;

                if (tile.Properties.TryGetValue("Action", out var action))
                {
                    string actionStr = action.ToString();
                    if (actionStr.StartsWith("Warp ") || actionStr.StartsWith("Door ") ||
                        actionStr.StartsWith("LockedDoorWarp "))
                    {
                        string[] parts = actionStr.Split(' ');
                        if (parts.Length >= 4)
                            return parts[3];
                        else if (parts.Length >= 2)
                            return parts[1];
                    }
                }
            }
            catch { }

            return null;
        }

        private (int x, int y) GetLocationEntry(string locationName)
        {
            return locationName switch
            {
                "ScienceHouse" => (6, 24),
                "AnimalShop" => (12, 16),
                "SeedShop" => (6, 30),
                "Saloon" => (14, 24),
                "Hospital" => (10, 19),
                "Blacksmith" => (5, 19),
                "JoshHouse" => (9, 24),
                "HaleyHouse" => (9, 24),
                "SamHouse" => (9, 24),
                "ManorHouse" => (9, 24),
                "Trailer" => (12, 9),
                "Trailer_Big" => (13, 24),
                "LeahHouse" => (7, 9),
                "WizardHouse" => (8, 24),
                "HarveyRoom" => (5, 8),
                "ElliottHouse" => (9, 9),
                "ArchaeologyHouse" => (3, 15),
                "Tent" => (5, 5),
                "FishShop" => (5, 9),
                "WitchHut" => (7, 15),
                _ => (5, 5)
            };
        }
    }
}
