using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;
using SVector2 = System.Numerics.Vector2;
using SVector4 = System.Numerics.Vector4;

namespace MapClickTeleport
{
    /// <summary>
    /// ImGui-based Stardew Utilities Menu
    /// </summary>
    public class ImGuiMenu : IClickableMenu
    {
        private readonly ImGuiRenderer _imGui;
        private readonly ModData _modData;
        private readonly Action<ModData> _saveData;
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        // UI State
        private int _currentTab = 0;
        private bool _showNPCs = false;
        private string _searchText = "";
        private string _consoleInput = "";
        private readonly List<string> _consoleOutput = new();
        private readonly List<string> _commandHistory = new();
        private bool _debugModeEnabled = true; // Toggle for auto-prepending "debug"

        // Tab names - added UI tab and OP tab
        private readonly string[] _tabNames = { "TP", "Console", "Cave", "Buff", "Sell", "Relations", "UI", "OP" };

        // Cave settings
        private bool _nextRockIsLadder = false;
        private int _skullCavernSkipLevels = 0;

        // OP settings (synced with OPFeatures static class)
        private bool _instantMineRock = false;
        private bool _instantCutTree = false;
        private bool _instantKillMonster = false;
        private bool _oneHitKill = false;
        private bool _infiniteStamina = false;
        private bool _infiniteHealth = false;
        private bool _noClip = false;
        private bool _instantCatch = false;
        private bool _autoPickup = false;
        private float _autoPickupRadius = 5f;
        private bool _pvpEnabled = false;
        private int _pvpDamage = 20;
        private bool _pvpUseMonsterMethod = true;
        private bool _mineCart = false;

        // Buff settings
        private int _buffSpeed = 0;
        private int _buffDefense = 0;
        private int _buffAttack = 0;
        private int _buffLuck = 0;
        private float _buffCritChance = 0;
        private float _buffCritPower = 0;
        private int _buffDuration = 0; // 0 = endless
        private bool _isInvincible = false;
        
        // Skill buff settings (farming, fishing, mining, foraging)
        private int _buffFarming = 0;
        private int _buffFishing = 0;
        private int _buffMining = 0;
        private int _buffForaging = 0;
        private int _buffMaxStamina = 0;
        private int _buffMagneticRadius = 0;

        // Monster drop multipliers (controlled via Harmony patch on GameLocation.monsterDrop)
        public static float MonsterDropChanceMultiplier { get; set; } = 1.0f;
        public static int MonsterDropAmountBonus { get; set; } = 0;
        public static float MonsterSpawnRateMultiplier { get; set; } = 1.0f;
        private int _monsterDropChance = 1;
        private int _monsterDropAmount = 0;
        private int _monsterSpawnRate = 1;

        // Fishing controls
        public static int FishCatchChance { get; set; } = 0; // 0 = default game behavior, 1-100 = force fish chance
        private int _fishCatchChance = 0;

        // Relations
        private string _relationsSearch = "";
        private bool _doorBypassEnabled = false;
        private string? _selectedNPC = null; // For NPC detail popup
        private int _editHearts = 0;

        // Click TP
        private bool _clickTPEnabled = false;

        // Console autocomplete
        private List<string> _autocompleteSuggestions = new();
        private int _autocompleteIndex = -1;
        private string _lastConsoleInput = "";
        private List<string> _smapiCommands = new(); // SMAPI commands fetched at runtime
        private bool _smapiCommandsFetched = false;

        // Known debug commands for autocomplete (used when debug mode is ON)
        private static readonly string[] DebugCommands = new[]
        {
            "warp", "warphome", "warptocharacter", "wtc", "warpcharacter", "wc",
            "time", "day", "season", "year", "addminutes", "settime",
            "speed", "water", "heal", "die", "invincible",
            "addmoney", "money", "levelup", "experience", "profession",
            "item", "give", "fuzzyitemnamed", "fin", "listtags",
            "backpack", "doesitemexist",
            "friendship", "marry", "divorce", "dateable", "pregnant",
            "makeinvisible", "makevisible", "pathspousetome",
            "sleep", "minelevel", "growcrops", "waterall",
            "removeterrainfeatures", "rtf", "growgrass", "grass",
            "regrowgrass", "spawnfoliage", "spawnmonster",
            "event", "eventover", "eventtestspecific", "ebi", "eventbyid",
            "rain", "storm", "snow", "sun", "weather",
            "build", "buildcoop", "buildbarn", "upgrade",
            "quest", "completequest", "removequest",
            "clearfarm", "setupfarm", "farmtype",
            "save", "loadsave", "newday", "pausetime", "freezetime",
            "canmove", "resetmove", "zoomlevel", "zl", "uiscale", "us",
            "playsound", "ps", "playmusic", "stopmusic",
            "cat", "dog", "horse", "removenpcs"
        };

        // Known SMAPI console commands (common ones from SMAPI and popular mods)
        private static readonly string[] BaseSMAPICommands = new[]
        {
            "help", "reload_i18n", "show_game_files", "show_data_files",
            "world_ready", "player_add", "player_setmoney", "player_sethealth",
            "player_setstamina", "player_setimmunity", "player_setlevel",
            "list_items", "debug", "patch", "patch summary", "patch export"
        };

        // Colors (dark theme)
        private static readonly SVector4 BgColor = new(0.1f, 0.1f, 0.12f, 0.95f);
        private static readonly SVector4 TabActiveColor = new(0.3f, 0.5f, 0.8f, 1f);
        private static readonly SVector4 TabHoverColor = new(0.25f, 0.4f, 0.6f, 1f);
        private static readonly SVector4 ButtonColor = new(0.2f, 0.4f, 0.7f, 1f);
        private static readonly SVector4 ButtonHoverColor = new(0.3f, 0.5f, 0.8f, 1f);
        private static readonly SVector4 ToggleOnColor = new(0.2f, 0.7f, 0.3f, 1f);
        private static readonly SVector4 ToggleOffColor = new(0.5f, 0.5f, 0.5f, 1f);
        private static readonly SVector4 HeaderColor = new(0.9f, 0.7f, 0.2f, 1f);

        // Keyboard state for text input
        private KeyboardState _prevKeyboard;

        // Key repeat timing for proper text input
        private Dictionary<Keys, float> _keyRepeatTimers = new();
        private const float KEY_INITIAL_DELAY = 0.5f; // Initial delay before repeat starts (500ms)
        private const float KEY_REPEAT_RATE = 0.08f; // Time between repeated keys (80ms = ~12.5 chars/sec)
        private float _deltaTime = 0.016f;

        // Separate tracking for navigation keys (arrows) with even slower repeat
        private Dictionary<ImGuiKey, float> _navKeyTimers = new();
        private const float NAV_INITIAL_DELAY = 0.3f; // Initial delay for nav keys
        private const float NAV_REPEAT_RATE = 0.15f; // Slower repeat for navigation (150ms)

        public ImGuiMenu(ImGuiRenderer imGui, ModData modData, Action<ModData> saveData, IModHelper helper, IMonitor monitor)
            : base(0, 0, Game1.viewport.Width, Game1.viewport.Height, true)
        {
            _imGui = imGui;
            _modData = modData;
            _saveData = saveData;
            _helper = helper;
            _monitor = monitor;

            // Sync with static states
            _clickTPEnabled = ModEntry.ClickTeleportEnabled;
            _doorBypassEnabled = RelationshipTab.DoorBypassEnabled;
            _isInvincible = BuffTab.IsInvincible;
            _nextRockIsLadder = CaveTab.NextRockIsLadder;
            _skullCavernSkipLevels = CaveTab.SkullCavernSkipLevels;
            
            // Sync OP features
            _instantMineRock = OPFeatures.InstantMineRock;
            _instantCutTree = OPFeatures.InstantCutTree;
            _instantKillMonster = OPFeatures.InstantKillMonster;
            _oneHitKill = OPFeatures.OneHitKill;
            _infiniteStamina = OPFeatures.InfiniteStamina;
            _infiniteHealth = OPFeatures.InfiniteHealth;

            // Sync monster drop controls (convert multiplier back to slider value)
            _monsterDropChance = (int)(MonsterDropChanceMultiplier * 10f);
            _monsterDropAmount = MonsterDropAmountBonus;
            _monsterSpawnRate = (int)(MonsterSpawnRateMultiplier * 10f);

            // Sync fishing controls
            _fishCatchChance = FishCatchChance;
            _noClip = OPFeatures.NoClip;
            _instantCatch = OPFeatures.InstantCatch;
            _autoPickup = OPFeatures.AutoPickup;
            _autoPickupRadius = OPFeatures.AutoPickupRadius;
            _pvpEnabled = OPFeatures.PVPEnabled;
            _pvpDamage = OPFeatures.PVPDamage;
            _pvpUseMonsterMethod = OPFeatures.UseMonsterMethod;
        }

        public override void update(GameTime time)
        {
            base.update(time);
            HandleTextInput();
        }

        private void HandleTextInput()
        {
            var keyboard = Keyboard.GetState();
            _deltaTime = 1f / 60f; // Approximate delta time

            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                // Skip backspace - handled in ImGuiRenderer with rate limiting
                if (key == Keys.Back) continue;

                bool isDown = keyboard.IsKeyDown(key);
                bool wasDown = _prevKeyboard.IsKeyDown(key);

                if (isDown && !wasDown)
                {
                    // Key just pressed - send character immediately and start repeat timer
                    char? c = KeyToChar(key, keyboard);
                    if (c.HasValue)
                    {
                        _imGui.AddInputCharacter(c.Value);
                    }
                    _keyRepeatTimers[key] = KEY_INITIAL_DELAY;
                }
                else if (isDown && wasDown)
                {
                    // Key held - check if we should repeat
                    if (_keyRepeatTimers.TryGetValue(key, out float timer))
                    {
                        timer -= _deltaTime;
                        if (timer <= 0)
                        {
                            char? c = KeyToChar(key, keyboard);
                            if (c.HasValue)
                            {
                                _imGui.AddInputCharacter(c.Value);
                            }
                            _keyRepeatTimers[key] = KEY_REPEAT_RATE;
                        }
                        else
                        {
                            _keyRepeatTimers[key] = timer;
                        }
                    }
                }
                else if (!isDown && wasDown)
                {
                    // Key released - remove from timers
                    _keyRepeatTimers.Remove(key);
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
                if (shift)
                {
                    char[] shiftNums = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    return shiftNums[key - Keys.D0];
                }
                return (char)('0' + (key - Keys.D0));
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return key switch
            {
                Keys.Space => ' ',
                Keys.OemPeriod => shift ? '>' : '.',
                Keys.OemComma => shift ? '<' : ',',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemTilde => shift ? '~' : '`',
                _ => null
            };
        }

        /// <summary>
        /// Handle navigation keys with proper debouncing to prevent multiple triggers
        /// Returns true only on initial press or after repeat delay
        /// </summary>
        private bool HandleNavKey(ImGuiKey key)
        {
            bool isDown = ImGui.IsKeyDown(key);

            if (!isDown)
            {
                // Key released - remove timer
                _navKeyTimers.Remove(key);
                return false;
            }

            if (!_navKeyTimers.TryGetValue(key, out float timer))
            {
                // Key just pressed - trigger immediately and start timer
                _navKeyTimers[key] = NAV_INITIAL_DELAY;
                return true;
            }

            // Key held - decrement timer
            timer -= _deltaTime;
            if (timer <= 0)
            {
                // Timer expired - trigger and reset to repeat rate
                _navKeyTimers[key] = NAV_REPEAT_RATE;
                return true;
            }

            // Timer still running - update but don't trigger
            _navKeyTimers[key] = timer;
            return false;
        }

        public override void receiveKeyPress(Keys key)
        {
            // When ImGui wants keyboard, ONLY allow escape to close menu
            // This prevents game chat (T key), inventory, etc. from being triggered
            if (_imGui.WantCaptureKeyboard)
            {
                if (key == Keys.Escape)
                    exitThisMenu();
                // Don't call base - completely suppress all game keyboard input
                return;
            }

            if (key == Keys.Escape || key == Keys.RightShift)
            {
                exitThisMenu();
            }
            // Don't call base.receiveKeyPress to prevent game input while menu is open
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_imGui.WantCaptureMouse)
                return;
            base.receiveLeftClick(x, y, playSound);
        }

        public override void draw(SpriteBatch b)
        {
            // No dark overlay - keep game visible

            _imGui.BeginLayout(new GameTime());
            SetupStyle();

            // Main window
            ImGui.SetNextWindowPos(new SVector2(Game1.viewport.Width * 0.1f, Game1.viewport.Height * 0.1f), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new SVector2(Game1.viewport.Width * 0.8f, Game1.viewport.Height * 0.8f), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Stardew Utilities", ImGuiWindowFlags.NoCollapse))
            {
                if (ImGui.BeginTabBar("MainTabs"))
                {
                    for (int i = 0; i < _tabNames.Length; i++)
                    {
                        if (ImGui.BeginTabItem(_tabNames[i]))
                        {
                            _currentTab = i;
                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();

                            switch (i)
                            {
                                case 0: DrawTPTab(); break;
                                case 1: DrawConsoleTab(); break;
                                case 2: DrawCaveTab(); break;
                                case 3: DrawBuffTab(); break;
                                case 4: DrawSellTab(); break;
                                case 5: DrawRelationsTab(); break;
                                case 6: DrawUITab(); break;
                                case 7: DrawOPTab(); break;
                            }

                            ImGui.EndTabItem();
                        }
                    }
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();

            // Floating windows are now rendered by ModEntry when menu is closed
            // Draw them here too so they're visible while menu is open
            FloatingWindows.Draw();

            _imGui.EndLayout();
            drawMouse(b);
        }

        private void SetupStyle()
        {
            var style = ImGui.GetStyle();

            style.WindowRounding = 8f;
            style.FrameRounding = 4f;
            style.GrabRounding = 4f;
            style.TabRounding = 4f;
            style.ScrollbarRounding = 4f;
            style.WindowPadding = new SVector2(15, 15);
            style.FramePadding = new SVector2(8, 4);
            style.ItemSpacing = new SVector2(10, 8);

            var colors = style.Colors;
            colors[(int)ImGuiCol.WindowBg] = BgColor;
            colors[(int)ImGuiCol.Tab] = new SVector4(0.15f, 0.15f, 0.18f, 1f);
            colors[(int)ImGuiCol.TabHovered] = TabHoverColor;
            colors[(int)ImGuiCol.TabActive] = TabActiveColor;
            colors[(int)ImGuiCol.Button] = ButtonColor;
            colors[(int)ImGuiCol.ButtonHovered] = ButtonHoverColor;
            colors[(int)ImGuiCol.FrameBg] = new SVector4(0.15f, 0.15f, 0.18f, 1f);
            colors[(int)ImGuiCol.FrameBgHovered] = new SVector4(0.2f, 0.2f, 0.25f, 1f);
            colors[(int)ImGuiCol.Header] = new SVector4(0.2f, 0.3f, 0.5f, 1f);
            colors[(int)ImGuiCol.HeaderHovered] = new SVector4(0.25f, 0.4f, 0.6f, 1f);
            colors[(int)ImGuiCol.TitleBg] = new SVector4(0.08f, 0.08f, 0.1f, 1f);
            colors[(int)ImGuiCol.TitleBgActive] = new SVector4(0.12f, 0.12f, 0.15f, 1f);
        }

        #region TP Tab
        private void DrawTPTab()
        {
            ImGui.TextColored(HeaderColor, "Click Teleport");
            ImGui.SameLine();

            bool clickTP = _clickTPEnabled;
            if (ImGui.Checkbox("##ClickTP", ref clickTP))
            {
                _clickTPEnabled = clickTP;
                ModEntry.ClickTeleportEnabled = clickTP;
                Game1.playSound(clickTP ? "coin" : "cancel");
            }
            ImGui.SameLine();
            ImGui.TextColored(clickTP ? ToggleOnColor : ToggleOffColor, clickTP ? "ON (Shift+RClick)" : "OFF");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(HeaderColor, "Teleport Mode");
            ImGui.SameLine();
            if (ImGui.Button(_showNPCs ? "NPCs" : "Locations"))
            {
                _showNPCs = !_showNPCs;
                Game1.playSound("smallSelect");
            }

            ImGui.Spacing();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 100);
            ImGui.InputTextWithHint("##Search", "Search...", ref _searchText, 100);

            ImGui.Spacing();

            if (ImGui.BeginChild("TPList", new SVector2(0, 300), true))
            {
                if (_showNPCs)
                    DrawNPCList();
                else
                    DrawLocationList();
            }
            ImGui.EndChild();

            ImGui.Spacing();
            ImGui.TextColored(HeaderColor, "Saved Points");

            if (ImGui.Button("+ Save Current Location"))
            {
                var name = $"Point {_modData.SavedPoints.Count + 1}";
                _modData.SavedPoints.Add(new SavedPoint
                {
                    Name = name,
                    LocationName = Game1.currentLocation.Name,
                    TileX = (int)Game1.player.Tile.X,
                    TileY = (int)Game1.player.Tile.Y
                });
                _saveData(_modData);
                Game1.playSound("coin");
            }

            if (ImGui.BeginChild("SavedPoints", new SVector2(0, 150), true))
            {
                for (int i = 0; i < _modData.SavedPoints.Count; i++)
                {
                    var point = _modData.SavedPoints[i];

                    ImGui.PushID(i);
                    if (ImGui.Button("Go"))
                    {
                        Game1.warpFarmer(point.LocationName, point.TileX, point.TileY, false);
                        Game1.playSound("wand");
                        exitThisMenu();
                    }
                    ImGui.SameLine();
                    ImGui.Text($"{point.Name} ({point.LocationName})");
                    ImGui.SameLine();
                    if (ImGui.Button("X"))
                    {
                        _modData.SavedPoints.RemoveAt(i);
                        _saveData(_modData);
                        Game1.playSound("trashcan");
                    }
                    ImGui.PopID();
                }
            }
            ImGui.EndChild();
        }

        private void DrawLocationList()
        {
            var locations = Game1.locations
                .Where(l => string.IsNullOrEmpty(_searchText) ||
                           l.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(l => l.Name)
                .ToList();

            foreach (var loc in locations)
            {
                if (ImGui.Button($"Go##{loc.Name}", new SVector2(40, 0)))
                {
                    int x = 5, y = 5;
                    try
                    {
                        var warp = loc.warps.FirstOrDefault();
                        if (warp != null)
                        {
                            x = warp.X;
                            y = warp.Y;
                        }
                    }
                    catch { }

                    Game1.warpFarmer(loc.Name, x, y, false);
                    Game1.playSound("wand");
                    exitThisMenu();
                }
                ImGui.SameLine();
                ImGui.Text(loc.Name);
            }
        }

        private void DrawNPCList()
        {
            var npcs = Utility.getAllCharacters()
                .Where(n => n.IsVillager &&
                           (string.IsNullOrEmpty(_searchText) ||
                            n.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(n => n.Name)
                .ToList();

            foreach (var npc in npcs)
            {
                if (ImGui.Button($"Go##{npc.Name}", new SVector2(40, 0)))
                {
                    if (npc.currentLocation != null)
                    {
                        Game1.warpFarmer(npc.currentLocation.Name, (int)npc.Tile.X, (int)npc.Tile.Y + 1, false);
                        Game1.playSound("wand");
                        exitThisMenu();
                    }
                }
                ImGui.SameLine();
                ImGui.Text($"{npc.Name} @ {npc.currentLocation?.Name ?? "Unknown"}");
            }
        }
        #endregion

        #region Console Tab
        private void DrawConsoleTab()
        {
            // Mode display based on toggle
            string modeLabel = _debugModeEnabled ? "ConsoleCommands Mode" : "SMAPI Mode";
            ImGui.TextColored(HeaderColor, modeLabel);
            ImGui.SameLine();

            // Debug mode toggle - text changes based on state
            string toggleLabel = _debugModeEnabled ? "Switch to SMAPI" : "Switch to ConsoleCommands";
            if (ImGui.Button(toggleLabel))
            {
                _debugModeEnabled = !_debugModeEnabled;
                _autocompleteSuggestions.Clear();
                _autocompleteIndex = -1;
                Game1.playSound("smallSelect");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_debugModeEnabled
                    ? "ConsoleCommands Mode: Auto-prepends 'debug' to commands\nClick to switch to SMAPI mode"
                    : "SMAPI Mode: Execute SMAPI console commands directly\nClick to switch to ConsoleCommands mode");
            }

            ImGui.Spacing();

            float inputHeight = 40;
            float autocompleteHeight = _autocompleteSuggestions.Count > 0 ? Math.Min(_autocompleteSuggestions.Count, 8) * 22 + 10 : 0;
            float outputHeight = ImGui.GetContentRegionAvail().Y - inputHeight - autocompleteHeight - 30;

            if (ImGui.BeginChild("ConsoleOutput", new SVector2(0, outputHeight), true))
            {
                foreach (var line in _consoleOutput)
                {
                    // Color code output lines
                    if (line.StartsWith(">"))
                        ImGui.TextColored(new SVector4(0.4f, 0.8f, 1f, 1f), line);
                    else if (line.StartsWith("[DEBUG]"))
                        ImGui.TextColored(new SVector4(0.5f, 1f, 0.5f, 1f), line);
                    else if (line.StartsWith("[ERROR]"))
                        ImGui.TextColored(new SVector4(1f, 0.4f, 0.4f, 1f), line);
                    else if (line.StartsWith("[RAW]"))
                        ImGui.TextColored(new SVector4(1f, 0.8f, 0.4f, 1f), line);
                    else
                        ImGui.TextWrapped(line);
                }

                if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                    ImGui.SetScrollHereY(1.0f);
            }
            ImGui.EndChild();

            // Autocomplete dropdown
            if (_autocompleteSuggestions.Count > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new SVector4(0.12f, 0.12f, 0.16f, 0.98f));
                if (ImGui.BeginChild("Autocomplete", new SVector2(0, autocompleteHeight), true))
                {
                    for (int i = 0; i < Math.Min(_autocompleteSuggestions.Count, 8); i++)
                    {
                        bool isSelected = i == _autocompleteIndex;
                        if (isSelected)
                            ImGui.PushStyleColor(ImGuiCol.Text, new SVector4(1f, 0.9f, 0.3f, 1f));

                        if (ImGui.Selectable(_autocompleteSuggestions[i], isSelected))
                        {
                            _consoleInput = _autocompleteSuggestions[i] + " ";
                            _autocompleteSuggestions.Clear();
                            _autocompleteIndex = -1;
                        }

                        if (isSelected)
                            ImGui.PopStyleColor();
                    }
                }
                ImGui.EndChild();
                ImGui.PopStyleColor();
            }

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 80);
            string hint = _debugModeEnabled
                ? "Enter debug command (e.g., warp Farm)..."
                : "Enter SMAPI command (e.g., help, player_add)...";

            // Note: Don't use CallbackCompletion flag without providing callback - causes AccessViolationException
            ImGuiInputTextFlags flags = ImGuiInputTextFlags.EnterReturnsTrue;
            if (ImGui.InputTextWithHint("##ConsoleInput", hint, ref _consoleInput, 256, flags))
            {
                ExecuteCommand(_consoleInput);
                _consoleInput = "";
                _autocompleteSuggestions.Clear();
            }

            // Update autocomplete on input change
            if (_consoleInput != _lastConsoleInput)
            {
                _lastConsoleInput = _consoleInput;
                UpdateAutocomplete(_consoleInput);
            }

            // Handle Tab key for autocomplete selection
            if (ImGui.IsItemFocused())
            {
                // Use custom key handling with debouncing for navigation keys
                bool tabPressed = HandleNavKey(ImGuiKey.Tab);
                bool upPressed = HandleNavKey(ImGuiKey.UpArrow);
                bool downPressed = HandleNavKey(ImGuiKey.DownArrow);
                bool escPressed = HandleNavKey(ImGuiKey.Escape);

                if (tabPressed && _autocompleteSuggestions.Count > 0)
                {
                    if (_autocompleteIndex >= 0 && _autocompleteIndex < _autocompleteSuggestions.Count)
                    {
                        _consoleInput = _autocompleteSuggestions[_autocompleteIndex] + " ";
                        _autocompleteSuggestions.Clear();
                        _autocompleteIndex = -1;
                    }
                    else
                    {
                        _autocompleteIndex = 0;
                    }
                }
                if (upPressed && _autocompleteSuggestions.Count > 0)
                {
                    _autocompleteIndex = Math.Max(0, _autocompleteIndex - 1);
                }
                if (downPressed && _autocompleteSuggestions.Count > 0)
                {
                    _autocompleteIndex = Math.Min(_autocompleteSuggestions.Count - 1, _autocompleteIndex + 1);
                }
                if (escPressed)
                {
                    _autocompleteSuggestions.Clear();
                    _autocompleteIndex = -1;
                }
            }
            else
            {
                // Clear nav timers when not focused
                _navKeyTimers.Clear();
            }

            ImGui.SameLine();
            if (ImGui.Button("Run", new SVector2(70, 0)))
            {
                ExecuteCommand(_consoleInput);
                _consoleInput = "";
                _autocompleteSuggestions.Clear();
            }
        }

        private void UpdateAutocomplete(string input)
        {
            _autocompleteSuggestions.Clear();
            _autocompleteIndex = -1;

            if (string.IsNullOrWhiteSpace(input) || input.Length < 1)
                return;

            string searchTerm = input.ToLower().Trim();

            // Use different command list based on mode
            var commands = _debugModeEnabled ? DebugCommands : GetSMAPICommands();

            foreach (var cmd in commands)
            {
                if (cmd.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    _autocompleteSuggestions.Add(cmd);
                    if (_autocompleteSuggestions.Count >= 8) break;
                }
            }

            // If exact match found, also show partial matches
            if (_autocompleteSuggestions.Count < 8)
            {
                foreach (var cmd in commands)
                {
                    if (cmd.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                        !_autocompleteSuggestions.Contains(cmd))
                    {
                        _autocompleteSuggestions.Add(cmd);
                        if (_autocompleteSuggestions.Count >= 8) break;
                    }
                }
            }

            if (_autocompleteSuggestions.Count > 0)
                _autocompleteIndex = 0;
        }

        private string[] GetSMAPICommands()
        {
            // Try to fetch SMAPI commands if not already done
            if (!_smapiCommandsFetched)
            {
                _smapiCommandsFetched = true;
                _smapiCommands.AddRange(BaseSMAPICommands);

                // Try to get commands from ConsoleCommands mod (common SMAPI utility)
                // These are the most common SMAPI commands that users would want
                _smapiCommands.AddRange(new[] {
                    "world_settime", "world_setday", "world_setseason", "world_setyear",
                    "player_setname", "player_setfarmname", "player_setfavoritething",
                    "player_setcolor", "player_setstyle", "player_setgender",
                    "player_additem", "player_addweapon", "player_addring",
                    "relationship_setfriendship", "relationship_setdating", "relationship_setmarried"
                });
            }

            return _smapiCommands.ToArray();
        }

        private void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            _consoleOutput.Add($"> {command}");
            _commandHistory.Insert(0, command);
            if (_commandHistory.Count > 20) _commandHistory.RemoveAt(20);

            try
            {
                if (_debugModeEnabled)
                {
                    // ConsoleCommands Mode: Auto-prepend debug and execute
                    _consoleOutput.Add($"[DEBUG] {command}");
                    Game1.game1.parseDebugInput(command);
                }
                else
                {
                    // SMAPI Mode: Execute as SMAPI console command
                    var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) return;

                    var cmd = parts[0].ToLower();
                    var args = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

                    _consoleOutput.Add($"[SMAPI] {command}");

                    // Special handling for common commands
                    if (cmd == "help")
                    {
                        _consoleOutput.Add("Available SMAPI commands:");
                        _consoleOutput.Add("  help - Show this help");
                        _consoleOutput.Add("  debug <cmd> - Run a debug command");
                        _consoleOutput.Add("  player_add <item> [count] - Add item to inventory");
                        _consoleOutput.Add("  world_settime <time> - Set time (e.g., 0600)");
                        _consoleOutput.Add("  world_setday <day> - Set day of month");
                        _consoleOutput.Add("  world_setseason <season> - Set season");
                        _consoleOutput.Add("Use ConsoleCommands mode for game debug commands");
                    }
                    else if (cmd == "debug" && args.Length > 0)
                    {
                        // Execute as debug command
                        string debugCmd = string.Join(" ", args);
                        Game1.game1.parseDebugInput(debugCmd);
                    }
                    else
                    {
                        // Try to execute through SMAPI's command system
                        // Note: Direct SMAPI command execution requires reflection or mod API
                        // For now, we'll handle common commands manually
                        bool handled = TryExecuteSMAPICommand(cmd, args);
                        if (!handled)
                        {
                            _consoleOutput.Add($"[INFO] Command '{cmd}' not recognized.");
                            _consoleOutput.Add("Try: debug <command> to run debug commands");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _consoleOutput.Add($"[ERROR] {ex.Message}");
            }

            Game1.playSound("smallSelect");
        }

        private bool TryExecuteSMAPICommand(string cmd, string[] args)
        {
            switch (cmd)
            {
                case "player_add":
                case "player_additem":
                    if (args.Length >= 1)
                    {
                        int count = args.Length >= 2 && int.TryParse(args[1], out int c) ? c : 1;
                        var item = ItemRegistry.Create(args[0], count);
                        if (item != null)
                        {
                            Game1.player.addItemToInventory(item);
                            _consoleOutput.Add($"[OK] Added {count}x {item.Name}");
                            return true;
                        }
                        _consoleOutput.Add($"[ERROR] Item '{args[0]}' not found");
                    }
                    return true;

                case "player_setmoney":
                    if (args.Length >= 1 && int.TryParse(args[0], out int money))
                    {
                        Game1.player.Money = money;
                        _consoleOutput.Add($"[OK] Money set to {money}");
                        return true;
                    }
                    return true;

                case "player_sethealth":
                    if (args.Length >= 1 && int.TryParse(args[0], out int health))
                    {
                        Game1.player.health = Math.Min(health, Game1.player.maxHealth);
                        _consoleOutput.Add($"[OK] Health set to {Game1.player.health}");
                        return true;
                    }
                    return true;

                case "player_setstamina":
                    if (args.Length >= 1 && float.TryParse(args[0], out float stamina))
                    {
                        Game1.player.Stamina = Math.Min(stamina, Game1.player.MaxStamina);
                        _consoleOutput.Add($"[OK] Stamina set to {Game1.player.Stamina}");
                        return true;
                    }
                    return true;

                case "world_settime":
                    if (args.Length >= 1 && int.TryParse(args[0], out int time))
                    {
                        Game1.timeOfDay = time;
                        _consoleOutput.Add($"[OK] Time set to {time}");
                        return true;
                    }
                    return true;

                case "world_setday":
                    if (args.Length >= 1 && int.TryParse(args[0], out int day))
                    {
                        Game1.dayOfMonth = Math.Clamp(day, 1, 28);
                        _consoleOutput.Add($"[OK] Day set to {Game1.dayOfMonth}");
                        return true;
                    }
                    return true;

                case "world_setseason":
                    if (args.Length >= 1)
                    {
                        string seasonStr = args[0].ToLower();
                        Season? newSeason = seasonStr switch
                        {
                            "spring" => Season.Spring,
                            "summer" => Season.Summer,
                            "fall" => Season.Fall,
                            "winter" => Season.Winter,
                            _ => null
                        };
                        if (newSeason.HasValue)
                        {
                            Game1.season = newSeason.Value;
                            _consoleOutput.Add($"[OK] Season set to {seasonStr}");
                            return true;
                        }
                        _consoleOutput.Add("[ERROR] Invalid season. Use: spring, summer, fall, winter");
                    }
                    return true;

                case "world_setyear":
                    if (args.Length >= 1 && int.TryParse(args[0], out int year))
                    {
                        Game1.year = Math.Max(1, year);
                        _consoleOutput.Add($"[OK] Year set to {Game1.year}");
                        return true;
                    }
                    return true;

                default:
                    return false;
            }
        }
        #endregion

        #region Cave Tab
        // Cave tab state
        private bool _instantMineEnabled = false;
        private int _instantMineRadius = 3;

        private void DrawCaveTab()
        {
            ImGui.TextColored(HeaderColor, "Mining Utilities");

            // Multiplayer host check warning
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, new SVector4(1f, 0.6f, 0.2f, 1f));
                ImGui.TextWrapped("⚠ MULTIPLAYER WARNING: Some features only work for the HOST player!");
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();

            // Instant Mine toggle
            ImGui.TextColored(HeaderColor, "Instant Mine");
            bool instantMine = _instantMineEnabled;
            if (ImGui.Checkbox("Auto-mine rocks near player", ref instantMine))
            {
                _instantMineEnabled = instantMine;
                CaveTab.InstantMineEnabled = instantMine;
                Game1.playSound(instantMine ? "coin" : "cancel");
                if (instantMine)
                    Game1.addHUDMessage(new HUDMessage("Instant Mine ON!", HUDMessage.newQuest_type));
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Automatically mines rocks within radius as you walk");

            ImGui.SameLine();
            ImGui.Text("Radius:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            int radius = _instantMineRadius;
            if (ImGui.SliderInt("##Radius", ref radius, 1, 10))
            {
                _instantMineRadius = radius;
                CaveTab.InstantMineRadius = radius;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            bool ladder = _nextRockIsLadder;
            if (ImGui.Checkbox("Next rock break spawns ladder/hole", ref ladder))
            {
                _nextRockIsLadder = ladder;
                CaveTab.NextRockIsLadder = ladder;
                Game1.playSound(ladder ? "coin" : "cancel");
            }
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                ImGui.SameLine();
                ImGui.TextColored(new SVector4(1, 0.5f, 0, 1), "(Host only)");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(HeaderColor, "Skull Cavern");
            ImGui.Text("Skip levels on next floor:");
            ImGui.SetNextItemWidth(150);
            int skip = _skullCavernSkipLevels;
            if (ImGui.InputInt("##SkipLevels", ref skip))
            {
                skip = Math.Max(0, Math.Min(skip, 1000)); // Allow up to 1000 levels
                _skullCavernSkipLevels = skip;
                CaveTab.SkullCavernSkipLevels = skip;
            }
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                ImGui.SameLine();
                ImGui.TextColored(new SVector4(1, 0.5f, 0, 1), "(Host only)");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(HeaderColor, "Destroy All Rocks");
            if (ImGui.Button("Mine All + Spawn Ladder", new SVector2(250, 32)))
            {
                NukeAllOres(true);
                Game1.playSound("explosion");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Mines all rocks, drops loot, and spawns ladder to next level");

            ImGui.SameLine();
            if (ImGui.Button("Mine Only", new SVector2(120, 32)))
            {
                NukeAllOres(false);
                Game1.playSound("explosion");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Mines all rocks and drops loot (no ladder)");

            if (!(Game1.currentLocation is MineShaft))
            {
                ImGui.Spacing();
                ImGui.TextColored(new SVector4(1, 0.5f, 0, 1), "Warning: Not in a mine!");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Spawn ladder/hole buttons
            ImGui.TextColored(HeaderColor, "Spawn Ladder / Shaft Hole");

            if (ImGui.Button("Spawn Ladder", new SVector2(185, 32)))
            {
                SpawnLadderDirectly(false);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Spawns a ladder to go down 1 level");

            ImGui.SameLine();

            if (ImGui.Button("Spawn Shaft Hole", new SVector2(185, 32)))
            {
                SpawnLadderDirectly(true);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Spawns a shaft hole to skip multiple levels (like in Skull Cavern)");
        }

        private void SpawnLadderDirectly(bool spawnHole = false)
        {
            if (Game1.currentLocation is not MineShaft shaft)
            {
                Game1.addHUDMessage(new HUDMessage("Must be in a mine!", HUDMessage.error_type));
                return;
            }

            bool spawned = false;
            Vector2 playerTile = Game1.player.Tile;

            // Spawn ladder/hole 1 tile to the right of player (not ON player)
            Vector2 spawnPos = new Vector2(playerTile.X + 1, playerTile.Y);

            try
            {
                if (spawnHole)
                {
                    // Create a shaft hole (for Skull Cavern to skip levels)
                    shaft.createLadderAt(spawnPos, "clubSmash"); // Different sound for hole

                    // Mark as shaft/hole by setting a property or using game's method
                    // The shaft appearance is actually based on tile index
                    try
                    {
                        var buildingsLayer = shaft.Map.GetLayer("Buildings");
                        var tileSheet = shaft.Map.TileSheets.FirstOrDefault();
                        if (buildingsLayer != null && tileSheet != null)
                        {
                            xTile.Dimensions.Location tileLoc = new xTile.Dimensions.Location((int)spawnPos.X, (int)spawnPos.Y);
                            // Shaft/hole tile index is 174 (ladder is 173)
                            buildingsLayer.Tiles[tileLoc] = new xTile.Tiles.StaticTile(
                                buildingsLayer,
                                tileSheet,
                                xTile.Tiles.BlendMode.Alpha,
                                174  // Shaft hole tile index
                            );
                        }
                    }
                    catch { }

                    shaft.ladderHasSpawned = true;
                    spawned = true;
                }
                else
                {
                    // Method 1: Try the game's built-in method first (most reliable)
                    shaft.createLadderAt(spawnPos, "hoeHit");
                    shaft.ladderHasSpawned = true;
                    spawned = true;
                }
            }
            catch
            {
                // Method 2: Direct tile placement as fallback
                try
                {
                    var buildingsLayer = shaft.Map.GetLayer("Buildings");
                    var tileSheet = shaft.Map.TileSheets.FirstOrDefault();

                    if (buildingsLayer != null && tileSheet != null)
                    {
                        xTile.Dimensions.Location tileLocation = new xTile.Dimensions.Location((int)spawnPos.X, (int)spawnPos.Y);
                        int tileIndex = spawnHole ? 174 : 173; // 174 = hole, 173 = ladder
                        buildingsLayer.Tiles[tileLocation] = new xTile.Tiles.StaticTile(
                            buildingsLayer,
                            tileSheet,
                            xTile.Tiles.BlendMode.Alpha,
                            tileIndex
                        );
                        shaft.ladderHasSpawned = true;
                        spawned = true;
                    }
                }
                catch (Exception ex)
                {
                    Game1.addHUDMessage(new HUDMessage($"Failed: {ex.Message}", HUDMessage.error_type));
                    return;
                }
            }

            if (spawned)
            {
                string msg = spawnHole ? "Shaft hole spawned!" : "Ladder spawned!";
                Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.newQuest_type));
            }
        }

        private void NukeAllOres(bool spawnLadder)
        {
            if (Game1.currentLocation is not MineShaft shaft) return;

            var toRemove = new List<Vector2>();
            int itemsDropped = 0;
            Vector2? ladderPos = null;

            foreach (var obj in shaft.Objects.Pairs)
            {
                if (obj.Value.Name.Contains("Stone") || obj.Value.Name.Contains("Ore") ||
                    obj.Value.Name.Contains("Node") || IsOreNode(obj.Value.ItemId))
                {
                    // Get proper drops from breaking the stone/ore
                    var drops = GetOreDrops(obj.Value.ItemId, obj.Value.Name);
                    foreach (var drop in drops)
                    {
                        Game1.createItemDebris(drop, obj.Key * 64f, Game1.random.Next(4), shaft);
                        itemsDropped++;
                    }
                    toRemove.Add(obj.Key);

                    // Remember last position for ladder
                    if (ladderPos == null || Game1.random.NextDouble() < 0.1)
                    {
                        ladderPos = obj.Key;
                    }
                }
            }

            foreach (var pos in toRemove)
            {
                shaft.Objects.Remove(pos);
            }

            // Spawn ladder if requested
            if (spawnLadder && ladderPos.HasValue)
            {
                shaft.createLadderAt(ladderPos.Value);
                Game1.addHUDMessage(new HUDMessage($"Mined {toRemove.Count} rocks → {itemsDropped} items + Ladder!", HUDMessage.newQuest_type));
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage($"Mined {toRemove.Count} rocks → {itemsDropped} items!", HUDMessage.newQuest_type));
            }
        }

        private bool IsOreNode(string itemId)
        {
            string[] oreIds = {
                "751", "290", "764", "765", "95", "843", "844", "845", "846", "847",
                "668", "670", "760", "762"
            };
            return oreIds.Contains(itemId);
        }

        private List<Item> GetOreDrops(string itemId, string name)
        {
            var drops = new List<Item>();

            // CORRECT ore node to drop mapping based on actual game data:
            // Ore Node IDs -> Drop Item IDs
            // 751 = Copper Node -> 378 (Copper Ore)
            // 290 = Iron Node (common) -> 380 (Iron Ore)
            // 764 = Gold Node -> 384 (Gold Ore)
            // 765 = Iridium Node -> 386 (Iridium Ore)
            // 95 = Radioactive Node -> 909 (Radioactive Ore)
            // 843, 844 = Copper/Iron nodes in Skull Cavern
            // 845, 846, 847 = Iridium nodes variants
            var dropMap = new Dictionary<string, (string itemId, int minCount, int maxCount)>
            {
                // Copper nodes
                {"751", ("378", 1, 3)}, // Copper Ore
                {"849", ("378", 2, 5)}, // Copper Node (Skull Cavern variant)
                // Iron nodes
                {"290", ("380", 1, 3)}, // Iron Ore (this is the correct mapping!)
                {"850", ("380", 2, 5)}, // Iron Node (Skull Cavern variant)
                // Gold nodes
                {"764", ("384", 1, 3)}, // Gold Ore
                {"851", ("384", 2, 5)}, // Gold Node (Skull Cavern variant)
                // Iridium nodes (765 is Iridium, NOT Iron!)
                {"765", ("386", 1, 3)}, // Iridium Ore
                {"843", ("386", 1, 3)}, // Iridium Node variant
                {"844", ("386", 1, 3)}, // Iridium Node variant
                {"845", ("386", 1, 4)}, // Iridium Node variant
                {"846", ("386", 1, 4)}, // Iridium Node variant
                {"847", ("386", 2, 5)}, // Iridium Node variant (rich)
                // Radioactive node
                {"95", ("909", 1, 2)},  // Radioactive Ore
                // Gem nodes - these drop specific gems
                {"2", ("72", 1, 1)},    // Diamond Node -> Diamond
                {"4", ("64", 1, 1)},    // Ruby Node -> Ruby
                {"6", ("70", 1, 1)},    // Jade Node -> Jade
                {"8", ("66", 1, 1)},    // Amethyst Node -> Amethyst
                {"10", ("68", 1, 1)},   // Topaz Node -> Topaz
                {"12", ("60", 1, 1)},   // Emerald Node -> Emerald
                {"14", ("62", 1, 1)},   // Aquamarine Node -> Aquamarine
                // Geode nodes
                {"75", ("535", 1, 1)},  // Geode
                {"76", ("536", 1, 1)},  // Frozen Geode
                {"77", ("537", 1, 1)},  // Magma Geode
                // Quartz nodes
                {"668", ("80", 1, 1)},  // Quartz
                {"670", ("82", 1, 1)},  // Fire Quartz
                // Mystic Stone
                {"760", ("386", 1, 4)}, // Iridium + chance for Prismatic
                {"762", ("386", 2, 5)}, // Iridium + chance for Prismatic
                // Cinder Shard nodes (Volcano)
                {"816", ("848", 1, 3)}, // Cinder Shard Node
                {"817", ("848", 2, 4)}, // Cinder Shard Node (rich)
            };

            if (dropMap.TryGetValue(itemId, out var dropInfo))
            {
                int count = Game1.random.Next(dropInfo.minCount, dropInfo.maxCount + 1);
                var item = ItemRegistry.Create(dropInfo.itemId, count);
                if (item != null) drops.Add(item);
            }
            else if (name.Contains("Copper"))
            {
                drops.Add(ItemRegistry.Create("378", Game1.random.Next(1, 4))); // Copper Ore
            }
            else if (name.Contains("Iron"))
            {
                drops.Add(ItemRegistry.Create("380", Game1.random.Next(1, 4))); // Iron Ore
            }
            else if (name.Contains("Gold"))
            {
                drops.Add(ItemRegistry.Create("384", Game1.random.Next(1, 4))); // Gold Ore
            }
            else if (name.Contains("Iridium"))
            {
                drops.Add(ItemRegistry.Create("386", Game1.random.Next(1, 4))); // Iridium Ore
            }
            else
            {
                // Regular stone - drop stone
                drops.Add(ItemRegistry.Create("390", Game1.random.Next(1, 3))); // Stone
            }

            // Chance for coal
            if (Game1.random.NextDouble() < 0.05)
            {
                drops.Add(ItemRegistry.Create("382", 1)); // Coal
            }

            // Chance for geode based on mine level
            if (Game1.random.NextDouble() < 0.03)
            {
                int mineLevel = Game1.currentLocation is MineShaft ms ? ms.mineLevel : 0;
                string geodeId = mineLevel switch
                {
                    < 40 => "535", // Geode
                    < 80 => "536", // Frozen Geode
                    < 120 => "537", // Magma Geode
                    _ => "749" // Omni Geode
                };
                drops.Add(ItemRegistry.Create(geodeId, 1));
            }

            return drops;
        }
        #endregion

        #region Buff Tab
        private void DrawBuffTab()
        {
            ImGui.TextColored(HeaderColor, "Buff Manager");
            ImGui.Spacing();

            // Quick toggles row
            bool invincible = _isInvincible;
            if (ImGui.Checkbox("God Mode (Invincible)", ref invincible))
            {
                _isInvincible = invincible;
                BuffTab.IsInvincible = invincible;
                Game1.playSound(invincible ? "powerup" : "cancel");

                if (invincible)
                    Game1.addHUDMessage(new HUDMessage("God Mode ON!", HUDMessage.newQuest_type));
            }

            ImGui.SameLine(300);

            if (ImGui.Button("Max Speed (+5)", new SVector2(150, 0)))
            {
                ApplyBuff("speed", 5);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(HeaderColor, "Custom Buff");
            ImGui.Spacing();

            // Use a table layout for better organization
            float inputWidth = 100f;
            float columnWidth = 130f;

            // Row 1: Speed, Defense
            ImGui.Text("Speed");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Speed", ref _buffSpeed);
            ImGui.SameLine(columnWidth + 80);
            ImGui.Text("Defense");
            ImGui.SameLine(columnWidth + 160);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Defense", ref _buffDefense);

            // Row 2: Attack, Luck
            ImGui.Text("Attack");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Attack", ref _buffAttack);
            ImGui.SameLine(columnWidth + 80);
            ImGui.Text("Luck");
            ImGui.SameLine(columnWidth + 160);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Luck", ref _buffLuck);

            // Row 3: Farming, Fishing
            ImGui.Text("Farming");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Farming", ref _buffFarming);
            ImGui.SameLine(columnWidth + 80);
            ImGui.Text("Fishing");
            ImGui.SameLine(columnWidth + 160);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Fishing", ref _buffFishing);

            // Row 4: Mining, Foraging
            ImGui.Text("Mining");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Mining", ref _buffMining);
            ImGui.SameLine(columnWidth + 80);
            ImGui.Text("Foraging");
            ImGui.SameLine(columnWidth + 160);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Foraging", ref _buffForaging);

            // Row 5: MaxStamina, MagneticRadius
            ImGui.Text("MaxStamina");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##MaxStamina", ref _buffMaxStamina);
            ImGui.SameLine(columnWidth + 80);
            ImGui.Text("Magnet");
            ImGui.SameLine(columnWidth + 160);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Magnet", ref _buffMagneticRadius);

            ImGui.Spacing();

            // Row 6: Crit Chance, Crit Power
            ImGui.Text("Crit Chance");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.SliderFloat("##CritChance", ref _buffCritChance, 0f, 100f, "%.0f%%");
            ImGui.SameLine(columnWidth + 80);
            ImGui.Text("Crit Power");
            ImGui.SameLine(columnWidth + 160);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.SliderFloat("##CritPower", ref _buffCritPower, 0f, 999f, "%.0f%%");

            // Row 7: Duration
            ImGui.Text("Duration");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt("##Duration", ref _buffDuration);
            _buffDuration = Math.Max(0, _buffDuration);
            ImGui.SameLine();
            ImGui.TextColored(new SVector4(0.6f, 0.6f, 0.6f, 1f), _buffDuration == 0 ? "(endless)" : "sec");
            ImGui.Spacing();

            // Duration presets
            ImGui.Text("Duration:");
            ImGui.SameLine();
            if (ImGui.SmallButton("Endless")) _buffDuration = 0;
            ImGui.SameLine();
            if (ImGui.SmallButton("1m")) _buffDuration = 60;
            ImGui.SameLine();
            if (ImGui.SmallButton("5m")) _buffDuration = 300;
            ImGui.SameLine();
            if (ImGui.SmallButton("10m")) _buffDuration = 600;
            ImGui.SameLine();
            if (ImGui.SmallButton("1h")) _buffDuration = 3600;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Apply buttons
            if (ImGui.Button("Apply Custom Buff", new SVector2(200, 32)))
            {
                ApplyCustomBuff();
            }

            ImGui.SameLine();

            if (ImGui.Button("Clear All Buffs", new SVector2(150, 32)))
            {
                Game1.player.buffs.Clear();
                _isInvincible = false;
                BuffTab.IsInvincible = false;
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage("All buffs cleared!", HUDMessage.newQuest_type));
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Quick buff presets
            ImGui.TextColored(HeaderColor, "Quick Presets");
            ImGui.Spacing();

            if (ImGui.Button("Combat King", new SVector2(140, 32)))
            {
                _buffAttack = 10;
                _buffDefense = 5;
                _buffCritChance = 50f;
                _buffCritPower = 100f;
                _buffDuration = 0;
                ApplyCustomBuff();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("+10 ATK, +5 DEF, +50% Crit, +100% Crit Power");

            ImGui.SameLine();

            if (ImGui.Button("Speed Demon", new SVector2(140, 32)))
            {
                _buffSpeed = 5;
                _buffLuck = 3;
                _buffDuration = 0;
                ApplyCustomBuff();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("+5 Speed, +3 Luck");

            ImGui.SameLine();

            if (ImGui.Button("Tank Mode", new SVector2(140, 32)))
            {
                _buffDefense = 20;
                _buffDuration = 0;
                ApplyCustomBuff();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("+20 Defense");

            ImGui.SameLine();

            if (ImGui.Button("Lucky Miner", new SVector2(140, 32)))
            {
                _buffLuck = 5;
                _buffSpeed = 2;
                _buffDuration = 0;
                ApplyCustomBuff();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("+5 Luck, +2 Speed");

            // Show current active buffs
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(HeaderColor, "Active Buffs");

            var appliedBuffs = Game1.player.buffs.AppliedBuffs;
            if (appliedBuffs.Count == 0)
            {
                ImGui.TextColored(new SVector4(0.5f, 0.5f, 0.5f, 1f), "No active buffs");
            }
            else
            {
                ImGui.Text($"Total: {appliedBuffs.Count} buff(s)");
                ImGui.Spacing();

                // Create a scrollable child window for the buff list
                if (ImGui.BeginChild("BuffList", new SVector2(0, 150), true))
                {
                    foreach (var kvp in appliedBuffs)
                    {
                        var buff = kvp.Value;
                        // Get clean name without special characters
                        string name = buff.displayName ?? buff.id ?? "Unknown Buff";
                        // Remove any potential icon characters that ImGui can't render
                        name = System.Text.RegularExpressions.Regex.Replace(name, @"[^\u0000-\u007F]+", "").Trim();
                        if (string.IsNullOrEmpty(name)) name = buff.id ?? "Buff";

                        // Format duration - use text instead of infinity symbol
                        string duration;
                        if (buff.millisecondsDuration == Buff.ENDLESS || buff.millisecondsDuration < 0)
                            duration = "Endless";
                        else if (buff.millisecondsDuration > 60000)
                            duration = $"{buff.millisecondsDuration / 60000}m {(buff.millisecondsDuration % 60000) / 1000}s";
                        else
                            duration = $"{buff.millisecondsDuration / 1000}s";

                        // Color based on buff source
                        SVector4 buffColor = name.Contains("Utilities")
                            ? new SVector4(0.4f, 0.8f, 1f, 1f)
                            : new SVector4(0.9f, 0.9f, 0.9f, 1f);

                        ImGui.TextColored(buffColor, $"[{duration}]");
                        ImGui.SameLine();
                        ImGui.Text(name);

                        // Show buff effects on hover
                        if (ImGui.IsItemHovered() && buff.effects != null)
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text($"ID: {buff.id}");
                            if (buff.effects.Speed.Value != 0) ImGui.Text($"Speed: {buff.effects.Speed.Value:+#;-#;0}");
                            if (buff.effects.Attack.Value != 0) ImGui.Text($"Attack: {buff.effects.Attack.Value:+#;-#;0}");
                            if (buff.effects.Defense.Value != 0) ImGui.Text($"Defense: {buff.effects.Defense.Value:+#;-#;0}");
                            if (buff.effects.LuckLevel.Value != 0) ImGui.Text($"Luck: {buff.effects.LuckLevel.Value:+#;-#;0}");
                            if (buff.effects.FarmingLevel.Value != 0) ImGui.Text($"Farming: {buff.effects.FarmingLevel.Value:+#;-#;0}");
                            if (buff.effects.FishingLevel.Value != 0) ImGui.Text($"Fishing: {buff.effects.FishingLevel.Value:+#;-#;0}");
                            if (buff.effects.MiningLevel.Value != 0) ImGui.Text($"Mining: {buff.effects.MiningLevel.Value:+#;-#;0}");
                            if (buff.effects.ForagingLevel.Value != 0) ImGui.Text($"Foraging: {buff.effects.ForagingLevel.Value:+#;-#;0}");
                            if (buff.effects.MaxStamina.Value != 0) ImGui.Text($"MaxStamina: {buff.effects.MaxStamina.Value:+#;-#;0}");
                            if (buff.effects.MagneticRadius.Value != 0) ImGui.Text($"MagnetRadius: {buff.effects.MagneticRadius.Value:+#;-#;0}");
                            ImGui.EndTooltip();
                        }
                    }
                }
                ImGui.EndChild();
            }

            // Monster Drop Controls Section
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(HeaderColor, "Monster Drop Controls");
            ImGui.TextColored(new SVector4(0.6f, 0.6f, 0.6f, 1f), "Patches GameLocation.monsterDrop");
            ImGui.Spacing();
            ImGui.Spacing();

            float labelWidth = 110f;
            float sliderWidth = 180f;

            // Drop Chance (0-100)
            ImGui.Text("Drop Chance");
            ImGui.SameLine(labelWidth);
            ImGui.SetNextItemWidth(sliderWidth);
            if (ImGui.SliderInt("##DropChance", ref _monsterDropChance, 0, 100, "%d%%"))
            {
                MonsterDropChanceMultiplier = _monsterDropChance / 100f * 10f; // 0-100% maps to 0-10x
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("1##DC")) { _monsterDropChance = 1; MonsterDropChanceMultiplier = 0.1f; }
            ImGui.SameLine();
            if (ImGui.SmallButton("50##DC")) { _monsterDropChance = 50; MonsterDropChanceMultiplier = 5f; }
            ImGui.SameLine();
            if (ImGui.SmallButton("100##DC")) { _monsterDropChance = 100; MonsterDropChanceMultiplier = 10f; }

            // Drop Amount (0-100)
            ImGui.Text("Drop Amount");
            ImGui.SameLine(labelWidth);
            ImGui.SetNextItemWidth(sliderWidth);
            if (ImGui.SliderInt("##DropAmount", ref _monsterDropAmount, 0, 100, "+%d"))
            {
                MonsterDropAmountBonus = _monsterDropAmount;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("0##DA")) { _monsterDropAmount = 0; MonsterDropAmountBonus = 0; }
            ImGui.SameLine();
            if (ImGui.SmallButton("10##DA")) { _monsterDropAmount = 10; MonsterDropAmountBonus = 10; }
            ImGui.SameLine();
            if (ImGui.SmallButton("50##DA")) { _monsterDropAmount = 50; MonsterDropAmountBonus = 50; }

            // Spawn Rate (0-100)
            ImGui.Text("Spawn Rate");
            ImGui.SameLine(labelWidth);
            ImGui.SetNextItemWidth(sliderWidth);
            if (ImGui.SliderInt("##SpawnRate", ref _monsterSpawnRate, 0, 100, "%d%%"))
            {
                MonsterSpawnRateMultiplier = _monsterSpawnRate / 100f * 10f; // 0-100% maps to 0-10x
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("1##SR")) { _monsterSpawnRate = 1; MonsterSpawnRateMultiplier = 0.1f; }
            ImGui.SameLine();
            if (ImGui.SmallButton("50##SR")) { _monsterSpawnRate = 50; MonsterSpawnRateMultiplier = 5f; }
            ImGui.SameLine();
            if (ImGui.SmallButton("100##SR")) { _monsterSpawnRate = 100; MonsterSpawnRateMultiplier = 10f; }

            // Reset all monster controls
            ImGui.Spacing();
            if (ImGui.Button("Reset Monster Controls", new SVector2(200, 0)))
            {
                _monsterDropChance = 1;
                _monsterDropAmount = 0;
                _monsterSpawnRate = 1;
                MonsterDropChanceMultiplier = 0.1f;
                MonsterDropAmountBonus = 0;
                MonsterSpawnRateMultiplier = 0.1f;
                Game1.playSound("cancel");
            }

            // Fishing Controls Section
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(HeaderColor, "Fishing Controls");
            ImGui.TextColored(new SVector4(0.6f, 0.6f, 0.6f, 1f), "Patches GameLocation.getFish");
            ImGui.Spacing();
            ImGui.Spacing();

            // Fish Catch Chance (0 = default, 1-100 = force fish)
            ImGui.Text("Fish Chance");
            ImGui.SameLine(labelWidth);
            ImGui.SetNextItemWidth(sliderWidth);
            if (ImGui.SliderInt("##FishChance", ref _fishCatchChance, 0, 100, _fishCatchChance == 0 ? "Default" : "%d%%"))
            {
                FishCatchChance = _fishCatchChance;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("0##FC")) { _fishCatchChance = 0; FishCatchChance = 0; }
            ImGui.SameLine();
            if (ImGui.SmallButton("50##FC")) { _fishCatchChance = 50; FishCatchChance = 50; }
            ImGui.SameLine();
            if (ImGui.SmallButton("100##FC")) { _fishCatchChance = 100; FishCatchChance = 100; }

            if (_fishCatchChance > 0)
            {
                ImGui.TextColored(new SVector4(0.5f, 1f, 0.5f, 1f), $"  {_fishCatchChance}% chance to catch fish instead of trash");
            }
            else
            {
                ImGui.TextColored(new SVector4(0.6f, 0.6f, 0.6f, 1f), "  Using default game fishing logic");
            }
        }

        private void ApplyBuff(string type, int amount)
        {
            var effects = new StardewValley.Buffs.BuffEffects();

            switch (type)
            {
                case "speed":
                    effects.Speed.Value = amount;
                    break;
            }

            var buff = new Buff("stardewutilities_" + type, duration: Buff.ENDLESS, effects: effects);
            Game1.player.applyBuff(buff);
            Game1.playSound("powerup");
        }

        private void ApplyCustomBuff()
        {
            var effects = new StardewValley.Buffs.BuffEffects();

            // Combat buffs
            effects.Speed.Value = _buffSpeed;
            effects.Defense.Value = _buffDefense;
            effects.Attack.Value = _buffAttack;
            effects.LuckLevel.Value = _buffLuck;

            // Skill buffs (farming, fishing, mining, foraging)
            effects.FarmingLevel.Value = _buffFarming;
            effects.FishingLevel.Value = _buffFishing;
            effects.MiningLevel.Value = _buffMining;
            effects.ForagingLevel.Value = _buffForaging;

            // Other buffs
            effects.MaxStamina.Value = _buffMaxStamina;
            effects.MagneticRadius.Value = _buffMagneticRadius;

            // Crit buffs - these are multipliers
            if (_buffCritChance > 0)
            {
                effects.CriticalChanceMultiplier.Value = _buffCritChance;
            }
            if (_buffCritPower > 0)
            {
                effects.CriticalPowerMultiplier.Value = _buffCritPower;
            }

            int durationMs = _buffDuration == 0 ? Buff.ENDLESS : _buffDuration * 1000;

            var buff = new Buff(
                id: "stardewutilities_custom_" + Game1.ticks,
                displayName: "Utilities Buff",
                duration: durationMs,
                effects: effects
            );

            Game1.player.applyBuff(buff);
            Game1.playSound("powerup");

            string durationText = _buffDuration == 0 ? "endless" : $"{_buffDuration}s";
            Game1.addHUDMessage(new HUDMessage($"Buff applied ({durationText})!", HUDMessage.newQuest_type));
        }
        #endregion

        #region Sell Tab
        private void DrawSellTab()
        {
            ImGui.TextColored(HeaderColor, "Quick Sell");
            ImGui.TextWrapped("Click items in your inventory to sell them instantly.");
            ImGui.Spacing();

            if (ImGui.BeginChild("InventorySell", new SVector2(0, 400), true))
            {
                var inventory = Game1.player.Items;
                int columns = 12;

                for (int i = 0; i < inventory.Count; i++)
                {
                    var item = inventory[i];

                    if (i % columns != 0)
                        ImGui.SameLine();

                    ImGui.PushID(i);

                    if (item != null)
                    {
                        int sellPrice = item.sellToStorePrice();
                        string label = $"{item.Name.Substring(0, Math.Min(8, item.Name.Length))}\n${sellPrice}";

                        if (ImGui.Button(label, new SVector2(80, 50)))
                        {
                            int gold = sellPrice * item.Stack;
                            Game1.player.Money += gold;
                            Game1.player.removeItemFromInventory(item);
                            Game1.playSound("purchaseClick");
                            Game1.addHUDMessage(new HUDMessage($"Sold for ${gold}!", HUDMessage.newQuest_type));
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"{item.Name} x{item.Stack}\nSell: ${sellPrice * item.Stack}");
                        }
                    }
                    else
                    {
                        ImGui.Button("Empty", new SVector2(80, 50));
                    }

                    ImGui.PopID();
                }
            }
            ImGui.EndChild();

            ImGui.Spacing();
            ImGui.Text($"Current Gold: ${Game1.player.Money:N0}");
        }
        #endregion

        #region Relations Tab
        private void DrawRelationsTab()
        {
            ImGui.TextColored(HeaderColor, "Relationships");
            ImGui.Spacing();

            // Top row - door bypass and bulk actions
            bool doorBypass = _doorBypassEnabled;
            if (ImGui.Checkbox("Door Bypass (Enter any NPC home)", ref doorBypass))
            {
                _doorBypassEnabled = doorBypass;
                RelationshipTab.SetDoorBypass(doorBypass);
                Game1.playSound(doorBypass ? "coin" : "cancel");
            }

            ImGui.SameLine();
            if (ImGui.Button("Max All Hearts"))
            {
                foreach (var npc in Utility.getAllCharacters().Where(n => n.IsVillager))
                {
                    SetHearts(npc.Name, 10);
                }
                Game1.playSound("achievement");
                Game1.addHUDMessage(new HUDMessage("All friendships maxed!", HUDMessage.newQuest_type));
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.SetNextItemWidth(250);
            ImGui.InputTextWithHint("##RelSearch", "Search NPC...", ref _relationsSearch, 100);

            ImGui.Spacing();

            // Calculate available height for the list
            float availHeight = ImGui.GetContentRegionAvail().Y - 10;
            float listWidth = 450;
            float detailWidth = ImGui.GetContentRegionAvail().X - listWidth - 20;

            // NPC List (left side)
            if (ImGui.BeginChild("NPCList", new SVector2(listWidth, availHeight), true))
            {
                var npcs = Utility.getAllCharacters()
                    .Where(n => n.IsVillager &&
                               (string.IsNullOrEmpty(_relationsSearch) ||
                                n.Name.Contains(_relationsSearch, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(n => n.Name)
                    .ToList();

                foreach (var npc in npcs)
                {
                    ImGui.PushID(npc.Name);

                    Game1.player.friendshipData.TryGetValue(npc.Name, out var friendship);
                    int hearts = friendship?.Points / 250 ?? 0;
                    bool isSelected = _selectedNPC == npc.Name;

                    // Status icon
                    string status = "";
                    if (friendship?.IsMarried() == true) status = "[M]";
                    else if (friendship?.IsDating() == true) status = "[D]";
                    else if (friendship?.IsDivorced() == true) status = "[X]";

                    // Row with name, hearts bar representation (ASCII only - ImGui font doesn't support Unicode hearts)
                    int maxHearts = npc.datable.Value ? 14 : 10;
                    string heartBar = new string('#', Math.Min(hearts, maxHearts)) + new string('-', Math.Max(0, maxHearts - hearts));

                    // Clickable button for selection - use npc.Name (ASCII) not displayName (may have Unicode)
                    bool clicked = ImGui.Button($"{status,-4}{npc.Name,-18}", new SVector2(180, 22));
                    if (clicked)
                    {
                        _selectedNPC = npc.Name;
                        _editHearts = hearts;
                        Game1.playSound("smallSelect");
                    }

                    // Hearts display
                    ImGui.SameLine();
                    var heartColor = hearts >= 8 ? new SVector4(1f, 0.3f, 0.5f, 1f) :
                                    hearts >= 5 ? new SVector4(1f, 0.6f, 0.4f, 1f) :
                                    new SVector4(0.7f, 0.7f, 0.7f, 1f);
                    ImGui.TextColored(heartColor, heartBar);

                    // +/- buttons
                    ImGui.SameLine();
                    if (ImGui.SmallButton("-##dec") && hearts > 0)
                    {
                        SetHearts(npc.Name, hearts - 1);
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("+##inc") && hearts < 14)
                    {
                        SetHearts(npc.Name, hearts + 1);
                    }

                    ImGui.PopID();
                }
            }
            ImGui.EndChild();

            // Detail panel (right side)
            ImGui.SameLine();

            if (ImGui.BeginChild("NPCDetails", new SVector2(detailWidth, availHeight), true))
            {
                if (_selectedNPC != null)
                {
                    var npc = Utility.getAllCharacters().FirstOrDefault(n => n.Name == _selectedNPC);
                    if (npc != null)
                    {
                        DrawNPCDetailPanel(npc);
                    }
                    else
                    {
                        _selectedNPC = null;
                    }
                }
                else
                {
                    ImGui.TextColored(new SVector4(0.6f, 0.6f, 0.6f, 1f), "Click an NPC to edit");
                }
            }
            ImGui.EndChild();
        }

        private void DrawNPCDetailPanel(NPC npc)
        {
            Game1.player.friendshipData.TryGetValue(npc.Name, out var friendship);
            int hearts = friendship?.Points / 250 ?? 0;

            ImGui.TextColored(HeaderColor, npc.Name);
            ImGui.Spacing();

            // Location info
            ImGui.Text($"Location: {npc.currentLocation?.Name ?? "Unknown"}");
            ImGui.Text($"Birthday: {npc.Birthday_Season} {npc.Birthday_Day}");
            ImGui.Text($"Datable: {(npc.datable.Value ? "Yes" : "No")}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Heart editor
            ImGui.TextColored(HeaderColor, "Friendship");
            ImGui.Text($"Current: {hearts} hearts ({friendship?.Points ?? 0} points)");

            ImGui.Spacing();
            ImGui.Text("Set Hearts:");
            ImGui.SetNextItemWidth(150);
            if (ImGui.SliderInt("##EditHearts", ref _editHearts, 0, npc.datable.Value ? 14 : 10))
            {
                SetHearts(npc.Name, _editHearts);
            }

            ImGui.Spacing();

            // Quick heart buttons
            if (ImGui.Button("0", new SVector2(30, 0))) { SetHearts(npc.Name, 0); _editHearts = 0; }
            ImGui.SameLine();
            if (ImGui.Button("5", new SVector2(30, 0))) { SetHearts(npc.Name, 5); _editHearts = 5; }
            ImGui.SameLine();
            if (ImGui.Button("8", new SVector2(30, 0))) { SetHearts(npc.Name, 8); _editHearts = 8; }
            ImGui.SameLine();
            if (ImGui.Button("10", new SVector2(35, 0))) { SetHearts(npc.Name, 10); _editHearts = 10; }
            if (npc.datable.Value)
            {
                ImGui.SameLine();
                if (ImGui.Button("14", new SVector2(35, 0))) { SetHearts(npc.Name, 14); _editHearts = 14; }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Relationship status (only for datable NPCs)
            if (npc.datable.Value)
            {
                ImGui.TextColored(HeaderColor, "Relationship Status");

                bool isDating = friendship?.IsDating() == true;
                bool isMarried = friendship?.IsMarried() == true;
                bool isDivorced = friendship?.IsDivorced() == true;

                if (ImGui.Checkbox("Dating", ref isDating))
                {
                    ToggleRelationshipStatus(npc.Name, isDating ? "dating" : "friendly");
                }

                if (ImGui.Checkbox("Married", ref isMarried))
                {
                    ToggleRelationshipStatus(npc.Name, isMarried ? "married" : "friendly");
                }

                if (ImGui.Checkbox("Divorced", ref isDivorced))
                {
                    ToggleRelationshipStatus(npc.Name, isDivorced ? "divorced" : "friendly");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Actions
            ImGui.TextColored(HeaderColor, "Actions");

            if (ImGui.Button("Teleport to NPC", new SVector2(150, 0)))
            {
                if (npc.currentLocation != null)
                {
                    Game1.warpFarmer(npc.currentLocation.Name, (int)npc.Tile.X, (int)npc.Tile.Y + 1, false);
                    Game1.playSound("wand");
                    exitThisMenu();
                }
            }
        }

        private void ToggleRelationshipStatus(string npcName, string status)
        {
            if (!Game1.player.friendshipData.ContainsKey(npcName))
                Game1.player.friendshipData.Add(npcName, new Friendship());

            var friendship = Game1.player.friendshipData[npcName];

            switch (status)
            {
                case "dating":
                    friendship.Status = FriendshipStatus.Dating;
                    if (friendship.Points < 2000) friendship.Points = 2000;
                    break;
                case "married":
                    // Clear other marriages first
                    foreach (var kvp in Game1.player.friendshipData.Pairs)
                    {
                        if (kvp.Value.IsMarried()) kvp.Value.Status = FriendshipStatus.Friendly;
                    }
                    friendship.Status = FriendshipStatus.Married;
                    if (friendship.Points < 3500) friendship.Points = 3500;
                    Game1.player.spouse = npcName;
                    break;
                case "divorced":
                    friendship.Status = FriendshipStatus.Divorced;
                    if (Game1.player.spouse == npcName) Game1.player.spouse = null;
                    break;
                default:
                    friendship.Status = FriendshipStatus.Friendly;
                    if (Game1.player.spouse == npcName) Game1.player.spouse = null;
                    break;
            }

            Game1.playSound("drumkit6");
        }

        private void SetHearts(string npcName, int hearts)
        {
            if (!Game1.player.friendshipData.ContainsKey(npcName))
            {
                Game1.player.friendshipData.Add(npcName, new Friendship());
            }

            Game1.player.friendshipData[npcName].Points = hearts * 250;
            Game1.playSound("coin");
        }
        #endregion

        #region UI Tab - Custom Windows
        private void DrawUITab()
        {
            ImGui.TextColored(HeaderColor, "Floating HUD Windows");
            ImGui.TextWrapped("These windows stay visible even when this menu is closed!");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Window toggles - using FloatingWindows static class
            bool showStats = FloatingWindows.ShowPlayerStats;
            if (ImGui.Checkbox("Player Stats HUD", ref showStats))
            {
                FloatingWindows.ShowPlayerStats = showStats;
                Game1.playSound(showStats ? "coin" : "cancel");
            }
            ImGui.SameLine();
            ImGui.TextColored(ToggleOffColor, "(HP, Stamina, Gold)");

            bool showTime = FloatingWindows.ShowTimeWidget;
            if (ImGui.Checkbox("Time Widget", ref showTime))
            {
                FloatingWindows.ShowTimeWidget = showTime;
                Game1.playSound(showTime ? "coin" : "cancel");
            }
            ImGui.SameLine();
            ImGui.TextColored(ToggleOffColor, "(Time, Date, Location)");

            bool showInv = FloatingWindows.ShowInventoryWidget;
            if (ImGui.Checkbox("Hotbar Widget", ref showInv))
            {
                FloatingWindows.ShowInventoryWidget = showInv;
                Game1.playSound(showInv ? "coin" : "cancel");
            }
            ImGui.SameLine();
            ImGui.TextColored(ToggleOffColor, "(First 12 inventory slots)");

            bool showFPS = FloatingWindows.ShowFPSCounter;
            if (ImGui.Checkbox("FPS Counter", ref showFPS))
            {
                FloatingWindows.ShowFPSCounter = showFPS;
                Game1.playSound(showFPS ? "coin" : "cancel");
            }
            ImGui.SameLine();
            ImGui.TextColored(ToggleOffColor, "(Frames per second)");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // FPS Controls section
            ImGui.TextColored(HeaderColor, "FPS Controls");
            ImGui.TextWrapped("Control the game's frame rate limit");
            ImGui.Spacing();

            // Current FPS display
            float currentFps = ImGui.GetIO().Framerate;
            string fpsText = float.IsInfinity(currentFps) || float.IsNaN(currentFps) || currentFps > 10000
                ? "Unlimited"
                : $"{currentFps:F1}";
            ImGui.Text($"Current FPS: {fpsText}");
            ImGui.Spacing();

            // Quick preset buttons
            if (ImGui.Button("60 FPS", new SVector2(100, 30)))
            {
                FloatingWindows.TargetFPS = 60;
                FloatingWindows.ApplyFPSLimit();
                Game1.playSound("coin");
            }
            ImGui.SameLine();
            if (ImGui.Button("120 FPS", new SVector2(100, 30)))
            {
                FloatingWindows.TargetFPS = 120;
                FloatingWindows.ApplyFPSLimit();
                Game1.playSound("coin");
            }
            ImGui.SameLine();
            if (ImGui.Button("144 FPS", new SVector2(100, 30)))
            {
                FloatingWindows.TargetFPS = 144;
                FloatingWindows.ApplyFPSLimit();
                Game1.playSound("coin");
            }
            ImGui.SameLine();
            if (ImGui.Button("Unlimited", new SVector2(100, 30)))
            {
                FloatingWindows.TargetFPS = 0;
                FloatingWindows.ApplyFPSLimit();
                Game1.playSound("powerup");
            }

            ImGui.Spacing();

            // Custom FPS input
            ImGui.Text("Custom FPS:");
            ImGui.SameLine();
            int customFPS = FloatingWindows.CustomFPS;
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##CustomFPS", ref customFPS, 1, 10))
            {
                FloatingWindows.CustomFPS = Math.Clamp(customFPS, 30, 500);
            }
            ImGui.SameLine();
            if (ImGui.Button("Apply Custom", new SVector2(120, 0)))
            {
                FloatingWindows.TargetFPS = FloatingWindows.CustomFPS;
                FloatingWindows.ApplyFPSLimit();
                Game1.playSound("coin");
            }

            ImGui.Spacing();
            ImGui.TextColored(new SVector4(0.7f, 0.7f, 0.7f, 1f),
                $"Current Target: {(FloatingWindows.TargetFPS == 0 ? "Unlimited" : FloatingWindows.TargetFPS + " FPS")}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Hide in-game HUD section (Harmony patched)
            ImGui.TextColored(HeaderColor, "Hide In-Game HUD Elements");
            ImGui.TextColored(new SVector4(0.7f, 0.7f, 0.7f, 1f), "Use these when you have custom widgets enabled");
            ImGui.Spacing();

            // Combined toggle
            bool hideBoth = HUDHider.HideHealthStamina;
            if (ImGui.Checkbox("Hide Health & Stamina Bars", ref hideBoth))
            {
                HUDHider.HideHealthStamina = hideBoth;
                // Sync individual toggles
                HUDHider.HideHealthBar = hideBoth;
                HUDHider.HideStaminaBar = hideBoth;
                Game1.playSound(hideBoth ? "coin" : "cancel");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Hides both health and stamina bars together");
            }

            // Individual toggles (indented)
            ImGui.Indent(20);
            
            bool hideHealthOnly = HUDHider.HideHealthBar;
            if (ImGui.Checkbox("Health Bar Only", ref hideHealthOnly))
            {
                HUDHider.HideHealthBar = hideHealthOnly;
                // Update combined toggle if both are same
                HUDHider.HideHealthStamina = hideHealthOnly && HUDHider.HideStaminaBar;
                Game1.playSound(hideHealthOnly ? "coin" : "cancel");
            }

            bool hideStaminaOnly = HUDHider.HideStaminaBar;
            if (ImGui.Checkbox("Stamina Bar Only", ref hideStaminaOnly))
            {
                HUDHider.HideStaminaBar = hideStaminaOnly;
                // Update combined toggle if both are same
                HUDHider.HideHealthStamina = HUDHider.HideHealthBar && hideStaminaOnly;
                Game1.playSound(hideStaminaOnly ? "coin" : "cancel");
            }
            
            ImGui.Unindent(20);
            ImGui.Spacing();

            bool hideAll = HUDHider.HideEntireHUD;
            if (ImGui.Checkbox("Hide Entire Game HUD", ref hideAll))
            {
                HUDHider.HideEntireHUD = hideAll;
                Game1.playSound(hideAll ? "coin" : "cancel");
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Hides ALL game HUD elements (clock, money, toolbar, etc.)\nUse when you want a clean screen");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Style editor section
            ImGui.TextColored(HeaderColor, "Floating Window Style");

            ImGui.Text("Window Rounding:");
            ImGui.SameLine();
            float rounding = FloatingWindows.WindowRounding;
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("##WinRound", ref rounding, 0, 12))
            {
                FloatingWindows.WindowRounding = rounding;
            }

            ImGui.Text("Window Opacity:");
            ImGui.SameLine();
            float opacity = FloatingWindows.WindowOpacity;
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("##WinOpacity", ref opacity, 0.3f, 1.0f))
            {
                FloatingWindows.WindowOpacity = opacity;
            }

            ImGui.Text("Blur Strength:");
            ImGui.SameLine();
            float blur = FloatingWindows.PanelBlur;
            ImGui.SetNextItemWidth(200);
            if (ImGui.SliderFloat("##PanelBlur", ref blur, 0f, 10f))
            {
                FloatingWindows.PanelBlur = blur;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Frosted glass blur effect around floating windows\n0 = Off, 10 = Maximum");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Quick setup presets
            ImGui.TextColored(HeaderColor, "Quick Presets");

            if (ImGui.Button("Enable All Custom HUDs", new SVector2(200, 30)))
            {
                FloatingWindows.ShowPlayerStats = true;
                FloatingWindows.ShowTimeWidget = true;
                FloatingWindows.ShowInventoryWidget = true;
                HUDHider.HideHealthStamina = true;
                Game1.playSound("achievement");
            }

            ImGui.SameLine();

            if (ImGui.Button("Disable All / Reset", new SVector2(150, 30)))
            {
                FloatingWindows.ShowPlayerStats = false;
                FloatingWindows.ShowTimeWidget = false;
                FloatingWindows.ShowInventoryWidget = false;
                HUDHider.HideHealthStamina = false;
                HUDHider.HideEntireHUD = false;
                Game1.playSound("cancel");
            }
        }
        #endregion

        #region OP Tab - Overpowered Features
        private void DrawOPTab()
        {
            ImGui.TextColored(new SVector4(1f, 0.3f, 0.3f, 1f), "OVERPOWERED FEATURES");
            ImGui.TextColored(new SVector4(0.7f, 0.7f, 0.7f, 1f), "Use with caution - these can break game balance!");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Combat Section
            ImGui.TextColored(HeaderColor, "Combat");
            ImGui.Spacing();

            if (ImGui.Checkbox("One-Hit Kill Monsters", ref _oneHitKill))
            {
                OPFeatures.OneHitKill = _oneHitKill;
                Game1.playSound(_oneHitKill ? "powerup" : "cancel");
                if (_oneHitKill)
                    Game1.addHUDMessage(new HUDMessage("One-Hit Kill ON!", HUDMessage.newQuest_type));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Any attack instantly kills monsters");

            if (ImGui.Checkbox("Instant Kill on Contact", ref _instantKillMonster))
            {
                OPFeatures.InstantKillMonster = _instantKillMonster;
                Game1.playSound(_instantKillMonster ? "powerup" : "cancel");
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Monsters die when you touch them");

            ImGui.Spacing();

            // PVP Section
            ImGui.TextColored(new SVector4(1f, 0.5f, 0.2f, 1f), "PVP (Multiplayer)");
            ImGui.TextColored(new SVector4(0.7f, 0.7f, 0.7f, 1f), "CLIENT-SIDE ONLY: Other players won't see damage unless they have this mod too");
            ImGui.Spacing();

            if (ImGui.Checkbox("Enable PVP Damage", ref _pvpEnabled))
            {
                OPFeatures.PVPEnabled = _pvpEnabled;
                Game1.playSound(_pvpEnabled ? "swordswipe" : "cancel");
                if (_pvpEnabled)
                    Game1.addHUDMessage(new HUDMessage("PVP Mode ON - attack other farmers!", HUDMessage.error_type));
                else
                    Game1.addHUDMessage(new HUDMessage("PVP Mode OFF", HUDMessage.achievement_type));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Swing your weapon near other farmers to damage them (client-side only!)");

            if (_pvpEnabled)
            {
                ImGui.SameLine();
                ImGui.Text("Bonus Damage:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(120);
                if (ImGui.SliderInt("##PVPDamage", ref _pvpDamage, 0, 999, "%d"))
                {
                    OPFeatures.PVPDamage = _pvpDamage;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Bonus damage added to weapon damage (total = weapon damage + this value)");

                ImGui.Spacing();

                if (ImGui.Checkbox("Use Invisible Monster Method (REAL damage!)", ref _pvpUseMonsterMethod))
                {
                    OPFeatures.UseMonsterMethod = _pvpUseMonsterMethod;
                    Game1.playSound(_pvpUseMonsterMethod ? "shadowpeep" : "drumkit6");
                    if (_pvpUseMonsterMethod)
                        Game1.addHUDMessage(new HUDMessage("Monster method ON - damage will be REAL!", HUDMessage.newQuest_type));
                    else
                        Game1.addHUDMessage(new HUDMessage("Direct method (client-side only)", HUDMessage.achievement_type));
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Spawns invisible monster to deal REAL synced damage that your friend will actually see!\nUnchecked = direct damage (client-side only, friend won't see it)");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Mining Section
            ImGui.TextColored(HeaderColor, "Mining & Gathering");
            ImGui.Spacing();

            if (ImGui.Checkbox("Instant Mine Rocks", ref _instantMineRock))
            {
                OPFeatures.InstantMineRock = _instantMineRock;
                Game1.playSound(_instantMineRock ? "powerup" : "cancel");
                if (_instantMineRock)
                    Game1.addHUDMessage(new HUDMessage("Instant Mine ON!", HUDMessage.newQuest_type));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("One hit breaks any rock/ore node");

            if (ImGui.Checkbox("Instant Cut Trees", ref _instantCutTree))
            {
                OPFeatures.InstantCutTree = _instantCutTree;
                Game1.playSound(_instantCutTree ? "powerup" : "cancel");
                if (_instantCutTree)
                    Game1.addHUDMessage(new HUDMessage("Instant Chop ON!", HUDMessage.newQuest_type));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("One hit cuts down any tree/stump");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Player Stats Section
            ImGui.TextColored(HeaderColor, "Player Stats");
            ImGui.Spacing();

            if (ImGui.Checkbox("Infinite Stamina", ref _infiniteStamina))
            {
                OPFeatures.InfiniteStamina = _infiniteStamina;
                Game1.playSound(_infiniteStamina ? "powerup" : "cancel");
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Stamina never decreases");

            if (ImGui.Checkbox("Infinite Health", ref _infiniteHealth))
            {
                OPFeatures.InfiniteHealth = _infiniteHealth;
                Game1.playSound(_infiniteHealth ? "powerup" : "cancel");
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Health stays at maximum (same as God Mode in Buff tab)");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Movement Section
            ImGui.TextColored(HeaderColor, "Movement");
            ImGui.Spacing();

            if (ImGui.Checkbox("No Clip (Walk Through Walls)", ref _noClip))
            {
                OPFeatures.NoClip = _noClip;
                OPFeatures.OnNoClipToggled(_noClip); // Immediately restore collision when disabled
                Game1.playSound(_noClip ? "powerup" : "cancel");
                if (_noClip)
                    Game1.addHUDMessage(new HUDMessage("No Clip ON - walk through anything!", HUDMessage.newQuest_type));
                else
                    Game1.addHUDMessage(new HUDMessage("No Clip OFF - collision restored", HUDMessage.achievement_type));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Walk through walls, fences, and obstacles");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Fishing Section
            ImGui.TextColored(HeaderColor, "Fishing");
            ImGui.Spacing();

            if (ImGui.Checkbox("Instant Catch Fish", ref _instantCatch))
            {
                OPFeatures.InstantCatch = _instantCatch;
                Game1.playSound(_instantCatch ? "powerup" : "cancel");
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Fish are caught instantly without minigame");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Auto Pickup Section
            ImGui.TextColored(HeaderColor, "Auto Pickup");
            ImGui.Spacing();

            if (ImGui.Checkbox("Auto Pickup Items", ref _autoPickup))
            {
                OPFeatures.AutoPickup = _autoPickup;
                Game1.playSound(_autoPickup ? "powerup" : "cancel");
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Automatically picks up items in radius");

            if (_autoPickup)
            {
                ImGui.SameLine();
                ImGui.Text("Radius:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                if (ImGui.SliderFloat("##PickupRadius", ref _autoPickupRadius, 1f, 20f, "%.0f tiles"))
                {
                    OPFeatures.AutoPickupRadius = _autoPickupRadius;
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            

            // MiniGame Section
            ImGui.TextColored(HeaderColor, "MiniGame");
            ImGui.Spacing();
            // speed run the bar's minigame: logic: override the vector, make the fall vector to 0 and forward vector very large
            if (ImGui.Checkbox("MineCart MiniGame", ref _mineCart))
            {
                OPFeatures.MineCartHack = _mineCart;
                OPFeatures.OnMineCartToggled(_mineCart); // Immediately restore collision when disabled
                Game1.playSound(_mineCart ? "powerup" : "cancel");
                if (_mineCart)
                    Game1.addHUDMessage(new HUDMessage("speed run enabled!", HUDMessage.newQuest_type));
                else
                    Game1.addHUDMessage(new HUDMessage("speed run disabled!", HUDMessage.achievement_type));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Speed run minecart mini game");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Quick buttons
            ImGui.TextColored(HeaderColor, "Quick Toggles");
            ImGui.Spacing();

            if (ImGui.Button("Enable All OP Features", new SVector2(200, 32)))
            {
                _oneHitKill = true;
                _instantMineRock = true;
                _instantCutTree = true;
                _infiniteStamina = true;
                _infiniteHealth = true;
                _instantCatch = true;
                
                OPFeatures.OneHitKill = true;
                OPFeatures.InstantMineRock = true;
                OPFeatures.InstantCutTree = true;
                OPFeatures.InfiniteStamina = true;
                OPFeatures.InfiniteHealth = true;
                OPFeatures.InstantCatch = true;
                
                Game1.playSound("achievement");
                Game1.addHUDMessage(new HUDMessage("All OP features enabled!", HUDMessage.newQuest_type));
            }

            ImGui.SameLine();

            if (ImGui.Button("Disable All OP Features", new SVector2(200, 32)))
            {
                _oneHitKill = false;
                _instantMineRock = false;
                _instantCutTree = false;
                _instantKillMonster = false;
                _infiniteStamina = false;
                _infiniteHealth = false;
                _noClip = false;
                _instantCatch = false;
                _autoPickup = false;
                
                OPFeatures.OneHitKill = false;
                OPFeatures.InstantMineRock = false;
                OPFeatures.InstantCutTree = false;
                OPFeatures.InstantKillMonster = false;
                OPFeatures.InfiniteStamina = false;
                OPFeatures.InfiniteHealth = false;
                OPFeatures.NoClip = false;
                OPFeatures.InstantCatch = false;
                OPFeatures.AutoPickup = false;
                
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage("All OP features disabled.", HUDMessage.newQuest_type));
            }
        }
        #endregion
    }
}
