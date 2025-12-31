using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MapClickTeleport
{
    /// <summary>Relationship editing tab - modify friendship with NPCs.</summary>
    public class RelationshipTab
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly Rectangle bounds;

        // NPC list
        private List<NPCData> npcList = new();
        private List<NPCData> filteredList = new();
        private readonly List<ClickableComponent> listItems = new();

        // UI
        private readonly TextBox searchBox;
        private Rectangle searchBoxBounds;
        private int scrollOffset = 0;
        private int maxVisibleItems;
        private const int ItemHeight = 60;
        
        private Rectangle maxAllButton;
        private Rectangle add1HeartButton;
        private Rectangle remove1HeartButton;
        
        private string currentSearch = "";
        private bool isTyping = false;

        // Selected NPC for editing
        private NPCData? selectedNPC = null;
        private TextBox heartsInput;
        private Rectangle heartsInputBounds;
        private Rectangle setHeartsButton;
        private Rectangle datingToggle;
        private Rectangle marriedToggle;
        private Rectangle divorcedToggle;
        
        // Door bypass feature
        private Rectangle doorBypassToggle;
        public static bool DoorBypassEnabled { get; private set; } = false;
        
        public static void SetDoorBypass(bool enabled) => DoorBypassEnabled = enabled;

        public bool IsTyping => isTyping;

        public RelationshipTab(Rectangle bounds, IModHelper helper, IMonitor monitor)
        {
            this.bounds = bounds;
            this.helper = helper;
            this.monitor = monitor;

            int listHeight = bounds.Height - 250;
            maxVisibleItems = listHeight / ItemHeight;

            // Search box
            searchBox = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null, Game1.smallFont, Game1.textColor)
            {
                X = bounds.X + 30,
                Y = bounds.Y + 60,
                Width = 250,
                Text = ""
            };
            searchBoxBounds = new Rectangle(searchBox.X, searchBox.Y, searchBox.Width, 36);

            // Bulk action buttons
            int btnY = bounds.Y + 60;
            maxAllButton = new Rectangle(bounds.X + 300, btnY, 130, 36);
            add1HeartButton = new Rectangle(bounds.X + 440, btnY, 100, 36);
            remove1HeartButton = new Rectangle(bounds.X + 550, btnY, 100, 36);

            // Edit panel (right side)
            int editX = bounds.X + bounds.Width - 300;
            int editY = bounds.Y + 110;

            heartsInput = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null, Game1.smallFont, Game1.textColor)
            {
                X = editX + 80,
                Y = editY + 40,
                Width = 60,
                Text = "0"
            };
            heartsInputBounds = new Rectangle(heartsInput.X, heartsInput.Y, 60, 36);
            setHeartsButton = new Rectangle(editX + 150, editY + 40, 80, 36);

            datingToggle = new Rectangle(editX, editY + 90, 200, 30);
            marriedToggle = new Rectangle(editX, editY + 130, 200, 30);
            divorcedToggle = new Rectangle(editX, editY + 170, 200, 30);
            
            // Door bypass toggle (at top right)
            doorBypassToggle = new Rectangle(bounds.X + bounds.Width - 200, bounds.Y + 60, 180, 36);

            RefreshNPCList();
        }

        private void RefreshNPCList()
        {
            npcList.Clear();

            foreach (var npc in Utility.getAllCharacters())
            {
                if (npc == null || !npc.IsVillager)
                    continue;

                // Get friendship data
                Game1.player.friendshipData.TryGetValue(npc.Name, out var friendship);
                int hearts = friendship?.Points ?? 0;
                int heartLevel = hearts / 250;

                npcList.Add(new NPCData
                {
                    Name = npc.Name,
                    DisplayName = npc.displayName,
                    Hearts = heartLevel,
                    Points = hearts,
                    IsDating = friendship?.IsDating() ?? false,
                    IsMarried = friendship?.IsMarried() ?? false,
                    IsDivorced = friendship?.IsDivorced() ?? false,
                    CanDate = npc.datable.Value,
                    CanMarry = npc.datable.Value
                });
            }

            // Sort by name
            npcList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            
            FilterNPCs();
        }

        private void FilterNPCs()
        {
            scrollOffset = 0;

            if (string.IsNullOrWhiteSpace(currentSearch))
                filteredList = npcList.ToList();
            else
                filteredList = npcList
                    .Where(n => n.DisplayName.ToLower().Contains(currentSearch.ToLower()) ||
                               n.Name.ToLower().Contains(currentSearch.ToLower()))
                    .ToList();

            UpdateListItems();
        }

        private void UpdateListItems()
        {
            listItems.Clear();
            int startY = bounds.Y + 110;
            int itemWidth = bounds.Width - 330;

            for (int i = 0; i < maxVisibleItems && i + scrollOffset < filteredList.Count; i++)
            {
                listItems.Add(new ClickableComponent(
                    new Rectangle(bounds.X + 30, startY + i * ItemHeight, itemWidth, ItemHeight - 4),
                    (i + scrollOffset).ToString()));
            }
        }

        public void ReceiveLeftClick(int x, int y)
        {
            // Search box
            if (searchBoxBounds.Contains(x, y))
            {
                searchBox.SelectMe();
                isTyping = true;
                return;
            }

            // Hearts input
            if (selectedNPC != null && heartsInputBounds.Contains(x, y))
            {
                heartsInput.SelectMe();
                isTyping = true;
                return;
            }

            isTyping = false;
            searchBox.Selected = false;
            heartsInput.Selected = false;

            // Door bypass toggle
            if (doorBypassToggle.Contains(x, y))
            {
                DoorBypassEnabled = !DoorBypassEnabled;
                Game1.playSound(DoorBypassEnabled ? "coin" : "cancel");
                string msg = DoorBypassEnabled 
                    ? "🚪 Door Bypass ON - Enter ANY door (ignores friendship & time!)" 
                    : "Door Bypass OFF";
                Game1.addHUDMessage(new HUDMessage(msg, HUDMessage.newQuest_type));
                return;
            }

            // Max All button
            if (maxAllButton.Contains(x, y))
            {
                MaxAllFriendships();
                Game1.playSound("achievement");
                return;
            }

            // Add 1 heart to all
            if (add1HeartButton.Contains(x, y))
            {
                AddHeartsToAll(1);
                Game1.playSound("coin");
                return;
            }

            // Remove 1 heart from all
            if (remove1HeartButton.Contains(x, y))
            {
                AddHeartsToAll(-1);
                Game1.playSound("smallSelect");
                return;
            }

            // Set Hearts button
            if (selectedNPC != null && setHeartsButton.Contains(x, y))
            {
                if (int.TryParse(heartsInput.Text, out int hearts))
                {
                    SetFriendship(selectedNPC.Name, hearts);
                    RefreshNPCList();
                    // Re-select
                    selectedNPC = filteredList.FirstOrDefault(n => n.Name == selectedNPC.Name);
                    Game1.playSound("coin");
                }
                return;
            }

            // Dating toggle
            if (selectedNPC != null && selectedNPC.CanDate && datingToggle.Contains(x, y))
            {
                ToggleDating(selectedNPC.Name);
                RefreshNPCList();
                selectedNPC = filteredList.FirstOrDefault(n => n.Name == selectedNPC.Name);
                Game1.playSound("drumkit6");
                return;
            }

            // Married toggle
            if (selectedNPC != null && selectedNPC.CanMarry && marriedToggle.Contains(x, y))
            {
                ToggleMarried(selectedNPC.Name);
                RefreshNPCList();
                selectedNPC = filteredList.FirstOrDefault(n => n.Name == selectedNPC.Name);
                Game1.playSound("drumkit6");
                return;
            }

            // Divorced toggle
            if (selectedNPC != null && divorcedToggle.Contains(x, y))
            {
                ToggleDivorced(selectedNPC.Name);
                RefreshNPCList();
                selectedNPC = filteredList.FirstOrDefault(n => n.Name == selectedNPC.Name);
                Game1.playSound("drumkit6");
                return;
            }

            // NPC list click
            foreach (var item in listItems)
            {
                if (item.containsPoint(x, y))
                {
                    int index = int.Parse(item.name);
                    if (index < filteredList.Count)
                    {
                        selectedNPC = filteredList[index];
                        heartsInput.Text = selectedNPC.Hearts.ToString();
                        Game1.playSound("smallSelect");
                    }
                    return;
                }
            }
        }

        private void SetFriendship(string npcName, int hearts)
        {
            hearts = Math.Clamp(hearts, 0, 14);
            int points = hearts * 250;

            if (!Game1.player.friendshipData.ContainsKey(npcName))
                Game1.player.friendshipData.Add(npcName, new Friendship());

            Game1.player.friendshipData[npcName].Points = points;
            monitor.Log($"Set {npcName} friendship to {hearts} hearts ({points} points)", LogLevel.Debug);
        }

        private void ToggleDating(string npcName)
        {
            if (!Game1.player.friendshipData.ContainsKey(npcName))
                Game1.player.friendshipData.Add(npcName, new Friendship());

            var friendship = Game1.player.friendshipData[npcName];
            
            if (friendship.IsDating())
            {
                friendship.Status = FriendshipStatus.Friendly;
                monitor.Log($"Stopped dating {npcName}", LogLevel.Debug);
            }
            else
            {
                friendship.Status = FriendshipStatus.Dating;
                if (friendship.Points < 2000)
                    friendship.Points = 2000; // 8 hearts minimum for dating
                monitor.Log($"Now dating {npcName}", LogLevel.Debug);
            }
        }

        private void ToggleMarried(string npcName)
        {
            if (!Game1.player.friendshipData.ContainsKey(npcName))
                Game1.player.friendshipData.Add(npcName, new Friendship());

            var friendship = Game1.player.friendshipData[npcName];
            
            if (friendship.IsMarried())
            {
                friendship.Status = FriendshipStatus.Friendly;
                Game1.player.spouse = null;
                monitor.Log($"Unmarried {npcName}", LogLevel.Debug);
            }
            else
            {
                // Unmarry anyone else first
                foreach (var kvp in Game1.player.friendshipData.Pairs)
                {
                    if (kvp.Value.IsMarried())
                        kvp.Value.Status = FriendshipStatus.Friendly;
                }
                
                friendship.Status = FriendshipStatus.Married;
                friendship.Points = Math.Max(friendship.Points, 3500); // 14 hearts for married
                Game1.player.spouse = npcName;
                monitor.Log($"Married {npcName}", LogLevel.Debug);
            }
        }

        private void ToggleDivorced(string npcName)
        {
            if (!Game1.player.friendshipData.ContainsKey(npcName))
                Game1.player.friendshipData.Add(npcName, new Friendship());

            var friendship = Game1.player.friendshipData[npcName];
            
            if (friendship.IsDivorced())
            {
                friendship.Status = FriendshipStatus.Friendly;
                monitor.Log($"Undivorced {npcName}", LogLevel.Debug);
            }
            else
            {
                friendship.Status = FriendshipStatus.Divorced;
                if (Game1.player.spouse == npcName)
                    Game1.player.spouse = null;
                monitor.Log($"Divorced {npcName}", LogLevel.Debug);
            }
        }

        private void MaxAllFriendships()
        {
            foreach (var npc in npcList)
            {
                SetFriendship(npc.Name, npc.CanDate ? 10 : 10);
            }
            RefreshNPCList();
            Game1.addHUDMessage(new HUDMessage("All friendships maxed!", HUDMessage.newQuest_type));
        }

        private void AddHeartsToAll(int amount)
        {
            foreach (var npc in npcList)
            {
                int newHearts = Math.Clamp(npc.Hearts + amount, 0, npc.CanDate ? 14 : 10);
                SetFriendship(npc.Name, newHearts);
            }
            RefreshNPCList();
            Game1.addHUDMessage(new HUDMessage($"{(amount > 0 ? "+" : "")}{amount} heart(s) to all NPCs", HUDMessage.newQuest_type));
        }

        public void ReceiveScrollWheel(int direction)
        {
            int maxScroll = Math.Max(0, filteredList.Count - maxVisibleItems);
            if (direction > 0 && scrollOffset > 0)
            {
                scrollOffset--;
                UpdateListItems();
            }
            else if (direction < 0 && scrollOffset < maxScroll)
            {
                scrollOffset++;
                UpdateListItems();
            }
        }

        public void ReceiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                isTyping = false;
                searchBox.Selected = false;
                heartsInput.Selected = false;
            }
        }

        public void Update()
        {
            if (searchBox.Text != currentSearch)
            {
                currentSearch = searchBox.Text;
                FilterNPCs();
            }
        }

        public void Draw(SpriteBatch b)
        {
            // Title
            string title = "Relationship Editor";
            Vector2 titleSize = Game1.smallFont.MeasureString(title);
            b.DrawString(Game1.smallFont, title,
                new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + 20),
                Color.Gold);

            // Search box
            b.DrawString(Game1.smallFont, "Search:",
                new Vector2(searchBoxBounds.X - 60, searchBoxBounds.Y + 8),
                Game1.textColor);
            searchBox.Draw(b);

            // Bulk buttons
            DrawButton(b, maxAllButton, "Max All", Color.Gold);
            DrawButton(b, add1HeartButton, "+1 ❤ All", Color.LightGreen);
            DrawButton(b, remove1HeartButton, "-1 ❤ All", Color.LightCoral);
            
            // Door bypass toggle
            DrawButton(b, doorBypassToggle, 
                DoorBypassEnabled ? "🚪 Doors: OPEN" : "🚪 Doors: Normal",
                DoorBypassEnabled ? Color.LightGreen : new Color(150, 150, 150));

            // NPC List
            int startY = bounds.Y + 110;
            int listWidth = bounds.Width - 330;

            // List background
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(384, 396, 15, 15),
                bounds.X + 25, startY - 5, listWidth + 10, maxVisibleItems * ItemHeight + 10,
                new Color(50, 50, 60), 4f, false);

            // Draw list items
            for (int i = 0; i < listItems.Count; i++)
            {
                var item = listItems[i];
                int index = int.Parse(item.name);
                if (index >= filteredList.Count) continue;

                var npc = filteredList[index];
                bool isHovered = item.containsPoint(Game1.getMouseX(), Game1.getMouseY());
                bool isSelected = selectedNPC?.Name == npc.Name;

                Color bgColor = isSelected ? Color.LightBlue :
                    isHovered ? Color.Wheat :
                    npc.IsMarried ? new Color(255, 220, 220) :
                    npc.IsDating ? new Color(255, 240, 220) : Color.White;

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                    new Rectangle(384, 396, 15, 15),
                    item.bounds.X, item.bounds.Y, item.bounds.Width, item.bounds.Height,
                    bgColor, 4f, false);

                // Name
                b.DrawString(Game1.smallFont, npc.DisplayName,
                    new Vector2(item.bounds.X + 10, item.bounds.Y + 5),
                    Game1.textColor);

                // Status text
                string status = "";
                if (npc.IsMarried) status = " 💒 Married";
                else if (npc.IsDating) status = " 💕 Dating";
                else if (npc.IsDivorced) status = " 💔 Divorced";
                
                if (!string.IsNullOrEmpty(status))
                {
                    b.DrawString(Game1.smallFont, status,
                        new Vector2(item.bounds.X + 10 + Game1.smallFont.MeasureString(npc.DisplayName).X, item.bounds.Y + 5),
                        npc.IsMarried ? Color.DeepPink : npc.IsDating ? Color.Orange : Color.Gray);
                }

                // Hearts display
                int displayHearts = Math.Min(npc.Hearts, 14);
                for (int h = 0; h < Math.Min(displayHearts, 10); h++)
                {
                    b.Draw(Game1.mouseCursors,
                        new Vector2(item.bounds.X + 10 + h * 18, item.bounds.Y + 28),
                        new Rectangle(211, 428, 7, 6),
                        Color.Red, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
                }
                // Extra hearts for spouse (gold)
                for (int h = 10; h < displayHearts; h++)
                {
                    b.Draw(Game1.mouseCursors,
                        new Vector2(item.bounds.X + 10 + h * 18, item.bounds.Y + 28),
                        new Rectangle(211, 428, 7, 6),
                        Color.Gold, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
                }
                // Empty hearts
                for (int h = displayHearts; h < (npc.CanDate ? 14 : 10); h++)
                {
                    b.Draw(Game1.mouseCursors,
                        new Vector2(item.bounds.X + 10 + h * 18, item.bounds.Y + 28),
                        new Rectangle(218, 428, 7, 6),
                        Color.White * 0.5f, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
                }

                // Points text
                b.DrawString(Game1.smallFont, $"({npc.Points} pts)",
                    new Vector2(item.bounds.Right - 80, item.bounds.Y + 20),
                    Color.Gray * 0.7f);
            }

            // Edit panel (right side)
            int editX = bounds.X + bounds.Width - 290;
            int editY = bounds.Y + 110;

            // Panel background
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(384, 396, 15, 15),
                editX - 10, editY - 10, 280, 250,
                new Color(60, 60, 80), 4f, false);

            if (selectedNPC != null)
            {
                b.DrawString(Game1.smallFont, $"Editing: {selectedNPC.DisplayName}",
                    new Vector2(editX, editY),
                    Color.Gold);

                // Hearts input
                b.DrawString(Game1.smallFont, "Hearts:",
                    new Vector2(editX, editY + 48),
                    Game1.textColor);
                heartsInput.Draw(b);
                DrawButton(b, setHeartsButton, "Set", Color.LightGreen);

                // Status toggles (only for datable NPCs)
                if (selectedNPC.CanDate)
                {
                    DrawToggle(b, datingToggle, "Dating", selectedNPC.IsDating);
                    DrawToggle(b, marriedToggle, "Married", selectedNPC.IsMarried);
                }
                DrawToggle(b, divorcedToggle, "Divorced", selectedNPC.IsDivorced);
            }
            else
            {
                b.DrawString(Game1.smallFont, "Select an NPC to edit",
                    new Vector2(editX, editY + 50),
                    Color.Gray);
            }

            // Scroll indicator
            if (filteredList.Count > maxVisibleItems)
            {
                b.DrawString(Game1.smallFont,
                    $"Showing {scrollOffset + 1}-{Math.Min(scrollOffset + maxVisibleItems, filteredList.Count)} of {filteredList.Count}",
                    new Vector2(bounds.X + 30, bounds.Bottom - 50),
                    Color.Gray);
            }

            // Help text
            b.DrawString(Game1.smallFont, "Click NPC to select | Scroll to navigate list | Hearts: 0-14 (10 for non-datable)",
                new Vector2(bounds.X + 30, bounds.Bottom - 30),
                Color.Gray * 0.7f);
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

        private void DrawToggle(SpriteBatch b, Rectangle rect, string label, bool isOn)
        {
            bool hovered = rect.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color bgColor = isOn ? Color.LightGreen : (hovered ? Color.Wheat : new Color(100, 100, 100));

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                rect.X, rect.Y, rect.Width, rect.Height,
                bgColor, 4f, false);

            string text = isOn ? $"✓ {label}" : label;
            Vector2 textSize = Game1.smallFont.MeasureString(text);
            b.DrawString(Game1.smallFont, text,
                new Vector2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2),
                isOn ? Color.DarkGreen : Color.Gray);
        }

        private class NPCData
        {
            public string Name { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int Hearts { get; set; }
            public int Points { get; set; }
            public bool IsDating { get; set; }
            public bool IsMarried { get; set; }
            public bool IsDivorced { get; set; }
            public bool CanDate { get; set; }
            public bool CanMarry { get; set; }
        }
    }
}

