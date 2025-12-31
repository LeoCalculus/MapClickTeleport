using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System.Text;

namespace MapClickTeleport
{
    /// <summary>Console command execution tab for Stardew Utilities.</summary>
    public class ConsoleTab
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly Rectangle bounds;
        
        // UI Components
        private readonly TextBox inputBox;
        private Rectangle inputBoxBounds;
        private readonly List<string> outputLines = new();
        
        // Static command history - persists entire game session
        private static readonly List<string> commandHistory = new();
        private const int MaxHistorySize = 20;
        
        private int historyIndex = -1;
        private string currentInput = "";
        
        // Autocomplete
        private List<string> autocompleteSuggestions = new();
        private int autocompleteIndex = -1;
        private bool showAutocomplete = false;

        // Scrolling
        private int scrollOffset = 0;
        private int maxVisibleLines;

        // Known commands for autocomplete
        private static readonly string[] DebugCommands = new[]
        {
            // Movement and warping
            "warp", "warphome", "warptocharacter", "wtc", "warpcharacter", "wc",
            // Time and date
            "time", "day", "season", "year", "addminutes", "settime",
            // Player
            "speed", "water", "heal", "die", "invincible", "testnut",
            "addmoney", "money", "levelup", "experience", "profession",
            // Items
            "item", "give", "fuzzyitemnamed", "fin", "listtags",
            "backpack", "doesitemexist", "water",
            // NPCs
            "friendship", "marry", "divorce", "dateable", "pregnant",
            "makeinvisible", "makevisible", "pathspousetome",
            // World
            "sleep", "minelevel", "growcrops", "water", "waterall",
            "removeterrainfeatures", "rtf", "growgrass", "grass",
            "regrowgrass", "spawnfoliage", "spawnmonster",
            // Events
            "event", "eventover", "eventtestspecific", "ebi", "eventbyid",
            // Weather
            "rain", "storm", "snow", "sun", "weather",
            // Buildings
            "build", "buildcoop", "buildbarn", "upgrade",
            // Quests
            "quest", "completequest", "removequest",
            // Farm
            "clearfarm", "setupfarm", "farmtype",
            // Misc
            "save", "loadsave", "newday", "pausetime", "freezetime",
            "canmove", "resetmove", "zoomlevel", "zl", "uiscale", "us",
            "playsound", "ps", "playmusic", "stopmusic",
            "showplurals", "panmode", "pm",
            "cat", "dog", "horse", "removenpcs",
            "junimonotefixer", "fixweapons", "removelargefeatures",
            "removedirt", "removeobjects", "clearlocations",
        };

        private static readonly string[] SmapiCommands = new[]
        {
            "help", "list_items", "player_add", "player_add name",
            "player_setmoney", "player_changecolor", "player_changestyle",
            "world_clear", "world_downminelevel", "world_freezetime",
            "world_setday", "world_setminelevel", "world_setseason",
            "world_settime", "world_setyear",
            "show_game_files", "show_data_files",
        };

        public bool IsTyping { get; private set; }

        public ConsoleTab(Rectangle bounds, IModHelper helper, IMonitor monitor)
        {
            this.bounds = bounds;
            this.helper = helper;
            this.monitor = monitor;

            // Calculate layout
            int inputHeight = 40;
            int inputY = bounds.Bottom - inputHeight - 20;
            
            maxVisibleLines = (inputY - bounds.Y - 60) / 24;

            // Create input box
            inputBox = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null,
                Game1.smallFont,
                Game1.textColor)
            {
                X = bounds.X + 20,
                Y = inputY,
                Width = bounds.Width - 40,
                Text = ""
            };
            inputBoxBounds = new Rectangle(inputBox.X, inputBox.Y, inputBox.Width, inputHeight);

            // Add welcome message
            outputLines.Add("=== Stardew Utilities Console ===");
            outputLines.Add("Type commands and press Enter to execute.");
            outputLines.Add("Use UP/DOWN arrows for command history.");
            outputLines.Add("Press TAB for autocomplete suggestions.");
            outputLines.Add("Prefix with 'debug ' for debug commands.");
            outputLines.Add("");
        }

        public void ReceiveLeftClick(int x, int y)
        {
            if (inputBoxBounds.Contains(x, y))
            {
                inputBox.SelectMe();
                IsTyping = true;
            }
            else
            {
                IsTyping = false;
            }

            // Check autocomplete click
            if (showAutocomplete && autocompleteSuggestions.Count > 0)
            {
                int acY = inputBoxBounds.Y - autocompleteSuggestions.Count * 24 - 10;
                for (int i = 0; i < autocompleteSuggestions.Count && i < 8; i++)
                {
                    Rectangle itemBounds = new Rectangle(inputBoxBounds.X, acY + i * 24, inputBoxBounds.Width, 24);
                    if (itemBounds.Contains(x, y))
                    {
                        ApplyAutocomplete(autocompleteSuggestions[i]);
                        return;
                    }
                }
            }
        }

        public void ReceiveKeyPress(Keys key)
        {
            if (!IsTyping)
                return;

            switch (key)
            {
                case Keys.Enter:
                    ExecuteCommand(inputBox.Text);
                    inputBox.Text = "";
                    showAutocomplete = false;
                    break;

                case Keys.Up:
                    if (showAutocomplete && autocompleteSuggestions.Count > 0)
                    {
                        autocompleteIndex = Math.Max(0, autocompleteIndex - 1);
                    }
                    else if (commandHistory.Count > 0)
                    {
                        if (historyIndex == -1)
                        {
                            currentInput = inputBox.Text;
                            historyIndex = commandHistory.Count - 1;
                        }
                        else if (historyIndex > 0)
                        {
                            historyIndex--;
                        }
                        inputBox.Text = commandHistory[historyIndex];
                    }
                    break;

                case Keys.Down:
                    if (showAutocomplete && autocompleteSuggestions.Count > 0)
                    {
                        autocompleteIndex = Math.Min(autocompleteSuggestions.Count - 1, autocompleteIndex + 1);
                    }
                    else if (historyIndex >= 0)
                    {
                        historyIndex++;
                        if (historyIndex >= commandHistory.Count)
                        {
                            historyIndex = -1;
                            inputBox.Text = currentInput;
                        }
                        else
                        {
                            inputBox.Text = commandHistory[historyIndex];
                        }
                    }
                    break;

                case Keys.Tab:
                    if (showAutocomplete && autocompleteSuggestions.Count > 0 && autocompleteIndex >= 0)
                    {
                        ApplyAutocomplete(autocompleteSuggestions[autocompleteIndex]);
                    }
                    else
                    {
                        UpdateAutocomplete(inputBox.Text);
                        if (autocompleteSuggestions.Count > 0)
                        {
                            showAutocomplete = true;
                            autocompleteIndex = 0;
                        }
                    }
                    break;

                case Keys.Escape:
                    if (showAutocomplete)
                    {
                        showAutocomplete = false;
                    }
                    else
                    {
                        IsTyping = false;
                        inputBox.Selected = false;
                    }
                    break;
            }
        }

        public void ReceiveScrollWheel(int direction)
        {
            int maxScroll = Math.Max(0, outputLines.Count - maxVisibleLines);
            
            if (direction > 0 && scrollOffset > 0)
                scrollOffset--;
            else if (direction < 0 && scrollOffset < maxScroll)
                scrollOffset++;
        }

        public void Update()
        {
            // Update autocomplete as user types
            if (IsTyping && inputBox.Text != currentInput)
            {
                currentInput = inputBox.Text;
                if (!string.IsNullOrEmpty(currentInput))
                {
                    UpdateAutocomplete(currentInput);
                }
                else
                {
                    showAutocomplete = false;
                }
            }
        }

        private void UpdateAutocomplete(string input)
        {
            autocompleteSuggestions.Clear();
            autocompleteIndex = -1;

            if (string.IsNullOrWhiteSpace(input))
                return;

            string searchTerm = input.ToLower().Trim();
            
            // Check if it starts with "debug "
            bool isDebug = searchTerm.StartsWith("debug ");
            string cmdPart = isDebug ? searchTerm.Substring(6) : searchTerm;

            // Search debug commands
            foreach (var cmd in DebugCommands)
            {
                if (cmd.StartsWith(cmdPart, StringComparison.OrdinalIgnoreCase))
                {
                    string suggestion = isDebug ? $"debug {cmd}" : cmd;
                    if (!autocompleteSuggestions.Contains(suggestion))
                        autocompleteSuggestions.Add(suggestion);
                }
            }

            // Search SMAPI commands
            foreach (var cmd in SmapiCommands)
            {
                if (cmd.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    if (!autocompleteSuggestions.Contains(cmd))
                        autocompleteSuggestions.Add(cmd);
                }
            }

            // Limit suggestions
            if (autocompleteSuggestions.Count > 8)
                autocompleteSuggestions = autocompleteSuggestions.Take(8).ToList();

            showAutocomplete = autocompleteSuggestions.Count > 0;
            if (showAutocomplete)
                autocompleteIndex = 0;
        }

        private void ApplyAutocomplete(string suggestion)
        {
            inputBox.Text = suggestion + " ";
            showAutocomplete = false;
            autocompleteIndex = -1;
        }

        private void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Add to history (persists entire session, max 20)
            if (commandHistory.Count == 0 || commandHistory[^1] != command)
            {
                commandHistory.Add(command);
                while (commandHistory.Count > MaxHistorySize)
                    commandHistory.RemoveAt(0);
            }
            historyIndex = -1;

            // Log command
            outputLines.Add($"> {command}");

            try
            {
                // Check if it's a debug command
                if (command.StartsWith("debug ", StringComparison.OrdinalIgnoreCase))
                {
                    string debugCmd = command.Substring(6);
                    ExecuteDebugCommand(debugCmd);
                }
                else if (IsSmapiCommand(command))
                {
                    // SMAPI commands need to be run in SMAPI console
                    outputLines.Add("[INFO] SMAPI commands must be run in the SMAPI console window.");
                    outputLines.Add($"[TIP] Open SMAPI console and type: {command}");
                }
                else
                {
                    // Try as debug command directly
                    ExecuteDebugCommand(command);
                }
            }
            catch (Exception ex)
            {
                outputLines.Add($"[ERROR] {ex.Message}");
                monitor.Log($"Console command error: {ex}", LogLevel.Error);
            }

            // Auto-scroll to bottom
            scrollOffset = Math.Max(0, outputLines.Count - maxVisibleLines);
        }

        private bool IsSmapiCommand(string command)
        {
            string[] parts = command.Split(' ');
            if (parts.Length == 0) return false;
            
            string cmd = parts[0].ToLower();
            return cmd.StartsWith("player_") || cmd.StartsWith("world_") || 
                   cmd.StartsWith("list_") || cmd.StartsWith("show_") ||
                   cmd == "help";
        }

        private void ExecuteDebugCommand(string command)
        {
            try
            {
                // Use the game's debug command parser
                bool result = Game1.game1.parseDebugInput(command);
                
                if (result)
                {
                    outputLines.Add("[OK] Command executed successfully.");
                }
                else
                {
                    outputLines.Add("[WARN] Command may have failed or is unknown.");
                    outputLines.Add("[TIP] Check SMAPI console for detailed output.");
                }
            }
            catch (Exception ex)
            {
                outputLines.Add($"[ERROR] Failed to execute: {ex.Message}");
            }
        }

        public void Draw(SpriteBatch b)
        {
            // Draw console background
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                bounds.X + 10, bounds.Y + 10, bounds.Width - 20, bounds.Height - 20,
                new Color(30, 30, 30), 4f, false);

            // Draw title
            string title = "Console Commands";
            Vector2 titleSize = Game1.smallFont.MeasureString(title);
            b.DrawString(Game1.smallFont, title,
                new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + 20),
                Color.LightGreen);

            // Draw output area
            int outputY = bounds.Y + 50;
            int outputHeight = inputBoxBounds.Y - outputY - 20;
            
            // Output background
            b.Draw(Game1.fadeToBlackRect, 
                new Rectangle(bounds.X + 20, outputY, bounds.Width - 40, outputHeight),
                Color.Black * 0.5f);

            // Draw output lines
            int lineY = outputY + 5;
            int startLine = Math.Max(0, scrollOffset);
            int endLine = Math.Min(outputLines.Count, startLine + maxVisibleLines);

            for (int i = startLine; i < endLine; i++)
            {
                string line = outputLines[i];
                Color lineColor = GetLineColor(line);
                
                // Truncate long lines
                if (Game1.smallFont.MeasureString(line).X > bounds.Width - 60)
                {
                    while (Game1.smallFont.MeasureString(line + "...").X > bounds.Width - 60 && line.Length > 0)
                        line = line.Substring(0, line.Length - 1);
                    line += "...";
                }

                b.DrawString(Game1.smallFont, line,
                    new Vector2(bounds.X + 25, lineY),
                    lineColor);
                lineY += 24;
            }

            // Draw scroll indicator
            if (outputLines.Count > maxVisibleLines)
            {
                string scrollInfo = $"[{scrollOffset + 1}-{endLine}/{outputLines.Count}]";
                Vector2 scrollSize = Game1.smallFont.MeasureString(scrollInfo);
                b.DrawString(Game1.smallFont, scrollInfo,
                    new Vector2(bounds.Right - scrollSize.X - 30, outputY + 5),
                    Color.Gray * 0.7f);
            }

            // Draw autocomplete dropdown (above input)
            if (showAutocomplete && autocompleteSuggestions.Count > 0)
            {
                int acHeight = Math.Min(8, autocompleteSuggestions.Count) * 24 + 10;
                int acY = inputBoxBounds.Y - acHeight - 5;
                
                // Background
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                    inputBoxBounds.X - 5, acY, inputBoxBounds.Width + 10, acHeight,
                    new Color(50, 50, 70), 4f, false);

                // Suggestions
                for (int i = 0; i < autocompleteSuggestions.Count && i < 8; i++)
                {
                    Color itemColor = (i == autocompleteIndex) ? Color.Yellow : Color.White;
                    b.DrawString(Game1.smallFont, autocompleteSuggestions[i],
                        new Vector2(inputBoxBounds.X + 5, acY + 5 + i * 24),
                        itemColor);
                }
            }

            // Draw input box label
            b.DrawString(Game1.smallFont, ">",
                new Vector2(inputBoxBounds.X - 15, inputBoxBounds.Y + 8),
                Color.LightGreen);

            // Draw input box
            inputBox.Draw(b);

            // Draw help text
            string helpText = "Enter=Run | Tab=Autocomplete | Up/Down=History | Scroll=Output";
            b.DrawString(Game1.smallFont, helpText,
                new Vector2(bounds.X + 20, bounds.Bottom - 18),
                Color.Gray * 0.6f);
        }

        private Color GetLineColor(string line)
        {
            if (line.StartsWith(">"))
                return Color.Cyan;
            if (line.StartsWith("[OK]"))
                return Color.LightGreen;
            if (line.StartsWith("[ERROR]"))
                return Color.Red;
            if (line.StartsWith("[WARN]"))
                return Color.Orange;
            if (line.StartsWith("[INFO]") || line.StartsWith("[TIP]"))
                return Color.Yellow;
            if (line.StartsWith("==="))
                return Color.Gold;
            return Color.White;
        }
    }
}

