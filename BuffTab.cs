using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Menus;

namespace MapClickTeleport
{
    /// <summary>
    /// Buff modification and application tab for the Stardew Utilities menu.
    /// Provides UI for creating custom buffs with configurable stats and duration,
    /// as well as quick preset buttons for common buff configurations.
    /// </summary>
    /// <remarks>
    /// Features:
    /// - Custom buff creation with 10 configurable stats
    /// - Configurable duration (seconds or all-day)
    /// - Quick preset buttons: Speed Boost, God Mode, Max Luck
    /// - Active buff display
    /// - Tab navigation between input fields
    /// </remarks>
    public class BuffTab
    {
        #region Private Fields

        /// <summary>SMAPI helper for mod utilities.</summary>
        private readonly IModHelper helper;

        /// <summary>SMAPI monitor for logging.</summary>
        private readonly IMonitor monitor;

        /// <summary>The bounding rectangle for this tab's content area.</summary>
        private readonly Rectangle bounds;

        /// <summary>
        /// Dictionary mapping stat names to their TextBox input controls.
        /// Each stat (Speed, Farming, etc.) has its own input box.
        /// </summary>
        private readonly Dictionary<string, TextBox> statInputs = new();

        /// <summary>
        /// Dictionary mapping stat names to their input box bounding rectangles.
        /// Used for click detection on input fields.
        /// </summary>
        private readonly Dictionary<string, Rectangle> statInputBounds = new();

        /// <summary>
        /// List of all buff stat names that can be configured.
        /// These correspond to BuffEffects properties in Stardew Valley.
        /// </summary>
        /// <remarks>
        /// Stats are displayed in two columns:
        /// Column 1: Speed, Farming, Fishing, Mining, Foraging
        /// Column 2: Luck, Attack, Defense, MaxStamina, MagneticRadius
        /// </remarks>
        private readonly List<string> statNames = new()
        {
            "Speed", "Farming", "Fishing", "Mining", "Foraging", "Luck",
            "Attack", "Defense", "MaxStamina", "MagneticRadius", "CriticalChance", "CriticalPower"
        };

        /// <summary>TextBox for entering buff duration in seconds.</summary>
        private TextBox durationInput = null!;

        /// <summary>Bounding rectangle for the duration input box.</summary>
        private Rectangle durationInputBounds;

        /// <summary>Button to apply the custom buff with current settings.</summary>
        private Rectangle applyBuffButton;

        /// <summary>Button to clear all active buffs from the player.</summary>
        private Rectangle clearBuffsButton;

        /// <summary>Preset button for quick +5 Speed buff (all day).</summary>
        private Rectangle speedBoostButton;

        /// <summary>Preset button to toggle God Mode (invincibility).</summary>
        private Rectangle invincibleButton;

        /// <summary>Preset button for quick +10 Luck buff (all day).</summary>
        private Rectangle maxLuckButton;

        /// <summary>
        /// Tracks which input field is currently active/focused.
        /// Null if no input is selected. Can be a stat name or "Duration".
        /// </summary>
        private string? activeInputField = null;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets whether God Mode (invincibility) is currently enabled.
        /// When true, player health is constantly restored to maximum in ModEntry.OnUpdateTicked.
        /// </summary>
        /// <remarks>
        /// This is static so it can be accessed from ModEntry for the health restoration logic,
        /// and from ImGuiMenu for the ImGui version of this tab.
        /// </remarks>
        public static bool IsInvincible { get; set; } = false;

        /// <summary>
        /// Gets whether any input field in this tab is currently being typed in.
        /// Used to prevent game input while user is entering values.
        /// </summary>
        public bool IsTyping => activeInputField != null;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the BuffTab class.
        /// </summary>
        /// <param name="bounds">The bounding rectangle for this tab's content area.</param>
        /// <param name="helper">SMAPI helper instance for mod utilities.</param>
        /// <param name="monitor">SMAPI monitor instance for logging.</param>
        public BuffTab(Rectangle bounds, IModHelper helper, IMonitor monitor)
        {
            this.bounds = bounds;
            this.helper = helper;
            this.monitor = monitor;

            InitializeUI();
        }

        #endregion

        #region UI Initialization

        /// <summary>
        /// Initializes all UI components including stat input boxes, duration input,
        /// and action/preset buttons.
        /// </summary>
        /// <remarks>
        /// Layout:
        /// - Two columns of stat inputs (5 stats each)
        /// - Duration input below stats
        /// - Apply/Clear buttons below duration
        /// - Preset buttons (Speed, God Mode, Luck) at the bottom
        /// </remarks>
        private void InitializeUI()
        {
            int startX = bounds.X + 30;
            int startY = bounds.Y + 60;
            int inputWidth = 60;
            int inputHeight = 32;
            int labelWidth = 100;
            int spacing = 40;
            int col2X = bounds.X + bounds.Width / 2 + 20;

            // Create stat inputs in two columns (6 rows each for 12 stats)
            int statsPerColumn = (statNames.Count + 1) / 2;  // Ceiling division
            for (int i = 0; i < statNames.Count; i++)
            {
                string stat = statNames[i];
                int col = i < statsPerColumn ? 0 : 1;  // First half in column 0, rest in column 1
                int row = i % statsPerColumn;
                int x = col == 0 ? startX + labelWidth : col2X + labelWidth;
                int y = startY + row * spacing;

                var textBox = new TextBox(
                    Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                    null, Game1.smallFont, Game1.textColor)
                {
                    X = x,
                    Y = y,
                    Width = inputWidth,
                    Text = "0"  // Default value
                };

                statInputs[stat] = textBox;
                statInputBounds[stat] = new Rectangle(x, y, inputWidth, inputHeight);
            }

            // Duration input - positioned below the stat inputs
            int durationY = startY + statsPerColumn * spacing + 20;
            durationInput = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null, Game1.smallFont, Game1.textColor)
            {
                X = startX + labelWidth,
                Y = durationY,
                Width = 80,
                Text = "60"  // Default 60 seconds
            };
            durationInputBounds = new Rectangle(durationInput.X, durationInput.Y, 80, inputHeight);

            // Action buttons - Apply and Clear
            int buttonY = durationY + spacing + 10;
            int buttonWidth = 180;
            int buttonHeight = 40;

            applyBuffButton = new Rectangle(startX, buttonY, buttonWidth, buttonHeight);
            clearBuffsButton = new Rectangle(startX + buttonWidth + 20, buttonY, buttonWidth, buttonHeight);

            // Preset buttons - Speed, God Mode, Luck
            int presetY = buttonY + buttonHeight + 30;
            speedBoostButton = new Rectangle(startX, presetY, 150, buttonHeight);
            invincibleButton = new Rectangle(startX + 160, presetY, 150, buttonHeight);
            maxLuckButton = new Rectangle(startX + 320, presetY, 150, buttonHeight);
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// Handles left mouse click events within the tab.
        /// Checks for clicks on input fields and buttons.
        /// </summary>
        /// <param name="x">Mouse X coordinate.</param>
        /// <param name="y">Mouse Y coordinate.</param>
        public void ReceiveLeftClick(int x, int y)
        {
            // Reset active input - will be set if clicking on an input
            activeInputField = null;

            // Check if clicking on any stat input box
            foreach (var kvp in statInputBounds)
            {
                if (kvp.Value.Contains(x, y))
                {
                    statInputs[kvp.Key].SelectMe();
                    activeInputField = kvp.Key;
                    return;
                }
            }

            // Check if clicking on duration input
            if (durationInputBounds.Contains(x, y))
            {
                durationInput.SelectMe();
                activeInputField = "Duration";
                return;
            }

            // Check action buttons
            if (applyBuffButton.Contains(x, y))
            {
                ApplyCustomBuff();
                return;
            }

            if (clearBuffsButton.Contains(x, y))
            {
                ClearAllBuffs();
                return;
            }

            // Check preset buttons
            if (speedBoostButton.Contains(x, y))
            {
                ApplySpeedBoost();
                return;
            }

            if (invincibleButton.Contains(x, y))
            {
                ToggleInvincible();
                return;
            }

            if (maxLuckButton.Contains(x, y))
            {
                ApplyMaxLuck();
                return;
            }
        }

        /// <summary>
        /// Handles keyboard input events.
        /// Supports Escape to deselect and Tab to cycle through inputs.
        /// </summary>
        /// <param name="key">The key that was pressed.</param>
        public void ReceiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                // Deselect all inputs
                activeInputField = null;
                foreach (var tb in statInputs.Values)
                    tb.Selected = false;
                durationInput.Selected = false;
            }
            else if (key == Keys.Tab && activeInputField != null)
            {
                // Cycle to next input field
                int currentIndex = statNames.IndexOf(activeInputField);

                if (activeInputField == "Duration")
                {
                    // From duration, go back to first stat
                    statInputs[statNames[0]].SelectMe();
                    activeInputField = statNames[0];
                }
                else if (currentIndex >= 0 && currentIndex < statNames.Count - 1)
                {
                    // Move to next stat
                    statInputs[activeInputField].Selected = false;
                    string nextStat = statNames[currentIndex + 1];
                    statInputs[nextStat].SelectMe();
                    activeInputField = nextStat;
                }
                else
                {
                    // From last stat, go to duration
                    statInputs[activeInputField].Selected = false;
                    durationInput.SelectMe();
                    activeInputField = "Duration";
                }
            }
        }

        #endregion

        #region Buff Actions

        /// <summary>
        /// Creates and applies a custom buff using the current input values.
        /// Parses all stat inputs and duration, then creates a Buff object.
        /// </summary>
        /// <remarks>
        /// - Duration of -1 creates an all-day buff (Buff.ENDLESS)
        /// - Stats with value 0 are not included in the buff
        /// - Plays "yoba" sound on success, shows HUD message
        /// </remarks>
        private void ApplyCustomBuff()
        {
            try
            {
                // Parse duration (default 60 seconds if invalid)
                if (!int.TryParse(durationInput.Text, out int durationSeconds))
                    durationSeconds = 60;

                // Convert to milliseconds, or use ENDLESS for all-day
                int durationMs = durationSeconds * 1000;
                if (durationSeconds == -1)
                    durationMs = Buff.ENDLESS;

                // Create buff effects from input values
                var effects = new BuffEffects();

                // Parse each stat input and apply if non-zero
                if (int.TryParse(statInputs["Speed"].Text, out int speed) && speed != 0)
                    effects.Speed.Value = speed;
                if (int.TryParse(statInputs["Farming"].Text, out int farming) && farming != 0)
                    effects.FarmingLevel.Value = farming;
                if (int.TryParse(statInputs["Fishing"].Text, out int fishing) && fishing != 0)
                    effects.FishingLevel.Value = fishing;
                if (int.TryParse(statInputs["Mining"].Text, out int mining) && mining != 0)
                    effects.MiningLevel.Value = mining;
                if (int.TryParse(statInputs["Foraging"].Text, out int foraging) && foraging != 0)
                    effects.ForagingLevel.Value = foraging;
                if (int.TryParse(statInputs["Luck"].Text, out int luck) && luck != 0)
                    effects.LuckLevel.Value = luck;
                if (int.TryParse(statInputs["Attack"].Text, out int attack) && attack != 0)
                    effects.Attack.Value = attack;
                if (int.TryParse(statInputs["Defense"].Text, out int defense) && defense != 0)
                    effects.Defense.Value = defense;
                if (int.TryParse(statInputs["MaxStamina"].Text, out int stamina) && stamina != 0)
                    effects.MaxStamina.Value = stamina;
                if (int.TryParse(statInputs["MagneticRadius"].Text, out int magnet) && magnet != 0)
                    effects.MagneticRadius.Value = magnet;
                if (int.TryParse(statInputs["CriticalChance"].Text, out int criticalchance) && criticalchance != 0)
                    effects.CriticalChanceMultiplier.Value = criticalchance;
                if (int.TryParse(statInputs["CriticalPower"].Text, out int criticalpower) && criticalpower != 0)
                    effects.CriticalPowerMultiplier.Value = criticalpower;

                // Create the buff with all configured settings
                Buff buff = new Buff(
                    id: "StardewUtilities.CustomBuff",
                    displayName: "Custom Buff",
                    description: "Applied via Stardew Utilities",
                    iconTexture: Game1.buffsIcons,
                    iconSheetIndex: 7,  // Generic buff icon
                    duration: durationMs,
                    effects: effects
                );

                // Apply to player
                Game1.player.applyBuff(buff);
                Game1.playSound("yoba");

                // Show confirmation message
                string durationText = durationSeconds == -1 ? "all day" : $"{durationSeconds}s";
                Game1.addHUDMessage(new HUDMessage($"Buff applied for {durationText}!", HUDMessage.newQuest_type));
                monitor.Log($"Applied custom buff: {durationText}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Game1.addHUDMessage(new HUDMessage("Failed to apply buff!", HUDMessage.error_type));
                monitor.Log($"Buff error: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Removes all active buffs from the player.
        /// Also disables God Mode if it was active.
        /// </summary>
        private void ClearAllBuffs()
        {
            Game1.player.buffs.Clear();
            IsInvincible = false;
            Game1.playSound("debuffHit");
            Game1.addHUDMessage(new HUDMessage("All buffs cleared!", HUDMessage.achievement_type));
        }

        /// <summary>
        /// Applies a preset Speed +5 buff that lasts all day.
        /// </summary>
        private void ApplySpeedBoost()
        {
            Buff buff = new Buff(
                id: "StardewUtilities.SpeedBoost",
                displayName: "Speed Boost",
                description: "Gotta go fast!",
                iconTexture: Game1.buffsIcons,
                iconSheetIndex: 9,  // Speed icon
                duration: Buff.ENDLESS,
                effects: new BuffEffects { Speed = { 5 } }
            );
            Game1.player.applyBuff(buff);
            Game1.playSound("powerup");
            Game1.addHUDMessage(new HUDMessage("Speed +5 (all day)!", HUDMessage.newQuest_type));
        }

        /// <summary>
        /// Toggles God Mode (invincibility) on/off.
        /// When enabled, player health is constantly restored to maximum
        /// in the ModEntry.OnUpdateTicked handler.
        /// </summary>
        private void ToggleInvincible()
        {
            IsInvincible = !IsInvincible;

            if (IsInvincible)
            {
                // Immediately restore health and notify player
                Game1.player.health = Game1.player.maxHealth;
                Game1.playSound("yoba");
                Game1.addHUDMessage(new HUDMessage("God Mode On - You take NO damage!", HUDMessage.newQuest_type));
            }
            else
            {
                Game1.playSound("debuffHit");
                Game1.addHUDMessage(new HUDMessage("God Mode OFF", HUDMessage.achievement_type));
            }
        }

        /// <summary>
        /// Applies a preset Luck +10 buff that lasts all day.
        /// </summary>
        private void ApplyMaxLuck()
        {
            Buff buff = new Buff(
                id: "StardewUtilities.MaxLuck",
                displayName: "Max Luck",
                description: "Fortune smiles upon you!",
                iconTexture: Game1.buffsIcons,
                iconSheetIndex: 4,  // Luck icon
                duration: Buff.ENDLESS,
                effects: new BuffEffects { LuckLevel = { 10 } }
            );
            Game1.player.applyBuff(buff);
            Game1.playSound("coin");
            Game1.addHUDMessage(new HUDMessage("Luck +10 (all day)!", HUDMessage.newQuest_type));
        }

        #endregion

        #region Drawing

        /// <summary>
        /// Draws the entire buff tab UI including all inputs, buttons, and active buff display.
        /// </summary>
        /// <param name="b">The SpriteBatch to draw with.</param>
        public void Draw(SpriteBatch b)
        {
            // Draw title centered at top
            string title = "Buff Manager";
            Vector2 titleSize = Game1.smallFont.MeasureString(title);
            b.DrawString(Game1.smallFont, title,
                new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + 20),
                Color.Gold);

            // Draw subtitle/description
            b.DrawString(Game1.smallFont, "Create and apply custom buffs to your character",
                new Vector2(bounds.X + 30, bounds.Y + 45),
                Color.LightGray);

            int startX = bounds.X + 30;
            int startY = bounds.Y + 60;
            int col2X = bounds.X + bounds.Width / 2 + 40;
            int spacing = 50;

            // Draw all stat input fields with labels (dynamic columns based on stat count)
            int statsPerColumn = (statNames.Count + 1) / 2;  // Ceiling division
            for (int i = 0; i < statNames.Count; i++)
            {
                string stat = statNames[i];
                int col = i < statsPerColumn ? 0 : 1;
                int row = i % statsPerColumn;
                int labelX = col == 0 ? startX : col2X;
                int y = startY + row * spacing;

                // Draw stat label
                b.DrawString(Game1.smallFont, $"{stat}:",
                    new Vector2(labelX, y + 5),
                    Game1.textColor);

                // Draw input textbox
                statInputs[stat].Draw(b);
            }

            // Draw duration section
            int durationY = startY + statsPerColumn * spacing + 20;
            b.DrawString(Game1.smallFont, "Duration (sec):",
                new Vector2(startX, durationY + 5),
                Game1.textColor);
            durationInput.Draw(b);

            // Draw duration help text
            b.DrawString(Game1.smallFont, "(-1 = all day)",
                new Vector2(durationInput.X + 90, durationY + 5),
                Color.Gray);

            // Draw action buttons
            DrawButton(b, applyBuffButton, "Apply Buff", Color.LightGreen);
            DrawButton(b, clearBuffsButton, "Clear All Buffs", new Color(255, 150, 150));

            // Draw preset section header
            int presetY = applyBuffButton.Y + applyBuffButton.Height + 15;
            b.DrawString(Game1.smallFont, "Quick Presets:",
                new Vector2(startX, presetY),
                Color.Orange);

            // Draw preset buttons with dynamic God Mode text
            DrawButton(b, speedBoostButton, "Speed +5", new Color(200, 255, 200));
            DrawButton(b, invincibleButton,
                IsInvincible ? "God Mode On" : "God Mode Off",
                IsInvincible ? Color.Yellow : new Color(200, 220, 255));
            DrawButton(b, maxLuckButton, "Max Luck", new Color(220, 255, 200));

            // Draw active buffs display section
            int buffsY = presetY + 60;
            b.DrawString(Game1.smallFont, "Active Buffs:",
                new Vector2(startX, buffsY),
                Color.Orange);

            // List currently active buffs (max 5 shown)
            int buffCount = 0;
            foreach (var buff in Game1.player.buffs.AppliedBuffs.Values)
            {
                if (buffCount >= 5) break;

                // Format buff info with duration
                string buffInfo = $"• {buff.displayName}";
                if (buff.millisecondsDuration > 0 && buff.millisecondsDuration != Buff.ENDLESS)
                    buffInfo += $" ({buff.millisecondsDuration / 1000}s)";
                else if (buff.millisecondsDuration == Buff.ENDLESS)
                    buffInfo += " (all day)";

                b.DrawString(Game1.smallFont, buffInfo,
                    new Vector2(startX + 10, buffsY + 25 + buffCount * 22),
                    Color.White);
                buffCount++;
            }

            // Show "no buffs" message if none active
            if (buffCount == 0)
            {
                b.DrawString(Game1.smallFont, "  No active buffs",
                    new Vector2(startX + 10, buffsY + 25),
                    Color.Gray);
            }

            // Draw help text at bottom
            b.DrawString(Game1.smallFont, "Tip: Use Tab to cycle through inputs. Set duration to -1 for all-day buffs.",
                new Vector2(bounds.X + 30, bounds.Bottom - 40),
                Color.Gray * 0.7f);
        }

        /// <summary>
        /// Draws a clickable button with hover effect.
        /// </summary>
        /// <param name="b">The SpriteBatch to draw with.</param>
        /// <param name="rect">The button's bounding rectangle.</param>
        /// <param name="text">The button's label text.</param>
        /// <param name="color">The button's background color (changes to wheat on hover).</param>
        private void DrawButton(SpriteBatch b, Rectangle rect, string text, Color color)
        {
            // Check if mouse is hovering over button
            bool hovered = rect.Contains(Game1.getMouseX(), Game1.getMouseY());

            // Draw button background (texture box with 9-slice scaling)
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                rect.X, rect.Y, rect.Width, rect.Height,
                hovered ? Color.Wheat : color, 4f, false);

            // Draw centered button text
            Vector2 textSize = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2),
                Color.Black);
        }

        #endregion
    }
}
