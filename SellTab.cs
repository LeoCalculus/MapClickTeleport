using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MapClickTeleport
{
    /// <summary>Instant sell tab - 12x3 grid for quick selling items.</summary>
    public class SellTab
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly Rectangle bounds;

        // Grid constants
        private const int GridColumns = 12;
        private const int GridRows = 3;
        private const int SlotSize = 64;
        private const int SlotPadding = 4;

        // Items in the sell grid
        private readonly Item?[,] sellGrid = new Item?[GridColumns, GridRows];
        
        // UI
        private Rectangle gridBounds;
        private Rectangle sellButton;
        private Rectangle clearButton;
        private Rectangle inventoryBounds;
        
        // Inventory display
        private readonly List<ClickableComponent> inventorySlots = new();
        private int inventoryScrollOffset = 0;
        private const int InventoryColumns = 12;
        private const int InventoryRows = 3;

        // Hover info
        private Item? hoveredItem = null;
        private int hoveredGridX = -1;
        private int hoveredGridY = -1;

        public bool IsTyping => false;

        public SellTab(Rectangle bounds, IModHelper helper, IMonitor monitor)
        {
            this.bounds = bounds;
            this.helper = helper;
            this.monitor = monitor;

            InitializeUI();
        }

        private void InitializeUI()
        {
            int gridWidth = GridColumns * (SlotSize + SlotPadding);
            int gridHeight = GridRows * (SlotSize + SlotPadding);
            
            // Center the grid
            int gridX = bounds.X + (bounds.Width - gridWidth) / 2;
            int gridY = bounds.Y + 80;
            gridBounds = new Rectangle(gridX, gridY, gridWidth, gridHeight);

            // Buttons below grid
            int buttonY = gridY + gridHeight + 20;
            int buttonWidth = 150;
            int buttonHeight = 45;
            
            sellButton = new Rectangle(gridX + gridWidth / 2 - buttonWidth - 20, buttonY, buttonWidth, buttonHeight);
            clearButton = new Rectangle(gridX + gridWidth / 2 + 20, buttonY, buttonWidth, buttonHeight);

            // Inventory section below buttons
            int invY = buttonY + buttonHeight + 30;
            int invWidth = InventoryColumns * (SlotSize + SlotPadding);
            int invX = bounds.X + (bounds.Width - invWidth) / 2;
            inventoryBounds = new Rectangle(invX, invY, invWidth, InventoryRows * (SlotSize + SlotPadding));

            // Create inventory slot components
            UpdateInventorySlots();
        }

        private void UpdateInventorySlots()
        {
            inventorySlots.Clear();
            
            var inventory = Game1.player.Items;
            int startIndex = inventoryScrollOffset * InventoryColumns;
            
            for (int row = 0; row < InventoryRows; row++)
            {
                for (int col = 0; col < InventoryColumns; col++)
                {
                    int x = inventoryBounds.X + col * (SlotSize + SlotPadding);
                    int y = inventoryBounds.Y + row * (SlotSize + SlotPadding);
                    int index = startIndex + row * InventoryColumns + col;
                    
                    inventorySlots.Add(new ClickableComponent(
                        new Rectangle(x, y, SlotSize, SlotSize),
                        index.ToString()));
                }
            }
        }

        public void ReceiveLeftClick(int x, int y)
        {
            // Check sell button
            if (sellButton.Contains(x, y))
            {
                SellAllItems();
                return;
            }

            // Check clear button
            if (clearButton.Contains(x, y))
            {
                ClearSellGrid();
                return;
            }

            // Check sell grid - remove item back to inventory
            for (int col = 0; col < GridColumns; col++)
            {
                for (int row = 0; row < GridRows; row++)
                {
                    Rectangle slotRect = GetGridSlotRect(col, row);
                    if (slotRect.Contains(x, y) && sellGrid[col, row] != null)
                    {
                        // Return item to inventory
                        Item item = sellGrid[col, row]!;
                        if (Game1.player.addItemToInventoryBool(item))
                        {
                            sellGrid[col, row] = null;
                            Game1.playSound("coin");
                        }
                        else
                        {
                            Game1.addHUDMessage(new HUDMessage("Inventory full!", HUDMessage.error_type));
                        }
                        return;
                    }
                }
            }

            // Check inventory slots - add item to sell grid
            foreach (var slot in inventorySlots)
            {
                if (slot.containsPoint(x, y))
                {
                    int index = int.Parse(slot.name);
                    if (index < Game1.player.Items.Count && Game1.player.Items[index] != null)
                    {
                        Item item = Game1.player.Items[index];
                        
                        // Check if item can be sold
                        if (!CanBeSold(item))
                        {
                            Game1.playSound("cancel");
                            Game1.addHUDMessage(new HUDMessage("Cannot sell this item!", HUDMessage.error_type));
                            return;
                        }

                        // Find empty slot in sell grid
                        if (TryAddToSellGrid(item))
                        {
                            Game1.player.Items[index] = null;
                            Game1.playSound("pickUpItem");
                        }
                        else
                        {
                            Game1.addHUDMessage(new HUDMessage("Sell grid is full!", HUDMessage.error_type));
                        }
                    }
                    return;
                }
            }
        }

        public void ReceiveRightClick(int x, int y)
        {
            // Right-click on inventory to add single item from stack
            foreach (var slot in inventorySlots)
            {
                if (slot.containsPoint(x, y))
                {
                    int index = int.Parse(slot.name);
                    if (index < Game1.player.Items.Count && Game1.player.Items[index] != null)
                    {
                        Item item = Game1.player.Items[index];
                        
                        if (!CanBeSold(item))
                        {
                            Game1.playSound("cancel");
                            return;
                        }

                        // Take one from stack
                        Item singleItem = item.getOne();
                        if (TryAddToSellGrid(singleItem))
                        {
                            item.Stack--;
                            if (item.Stack <= 0)
                                Game1.player.Items[index] = null;
                            Game1.playSound("pickUpItem");
                        }
                    }
                    return;
                }
            }
        }

        private bool CanBeSold(Item item)
        {
            // Check if item has a valid sell price
            if (item is StardewValley.Object obj)
            {
                return obj.canBeShipped() || obj.sellToStorePrice() > 0;
            }
            return item.salePrice() > 0;
        }

        private int GetSellPrice(Item item)
        {
            if (item is StardewValley.Object obj)
            {
                // Use shipping price if available, otherwise store price
                int shipPrice = obj.sellToStorePrice();
                return shipPrice > 0 ? shipPrice : (int)(obj.salePrice() * 0.5f);
            }
            return (int)(item.salePrice() * 0.5f);
        }

        private bool TryAddToSellGrid(Item item)
        {
            // First try to stack with existing items
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    if (sellGrid[col, row] != null && 
                        sellGrid[col, row]!.canStackWith(item) &&
                        sellGrid[col, row]!.Stack < sellGrid[col, row]!.maximumStackSize())
                    {
                        int space = sellGrid[col, row]!.maximumStackSize() - sellGrid[col, row]!.Stack;
                        int toAdd = Math.Min(space, item.Stack);
                        sellGrid[col, row]!.Stack += toAdd;
                        item.Stack -= toAdd;
                        if (item.Stack <= 0)
                            return true;
                    }
                }
            }

            // Find empty slot
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    if (sellGrid[col, row] == null)
                    {
                        sellGrid[col, row] = item;
                        return true;
                    }
                }
            }

            return false;
        }

        private Rectangle GetGridSlotRect(int col, int row)
        {
            return new Rectangle(
                gridBounds.X + col * (SlotSize + SlotPadding),
                gridBounds.Y + row * (SlotSize + SlotPadding),
                SlotSize,
                SlotSize);
        }

        private void SellAllItems()
        {
            int totalValue = 0;
            int itemCount = 0;

            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    if (sellGrid[col, row] != null)
                    {
                        Item item = sellGrid[col, row]!;
                        int price = GetSellPrice(item) * item.Stack;
                        totalValue += price;
                        itemCount += item.Stack;
                        sellGrid[col, row] = null;
                    }
                }
            }

            if (totalValue > 0)
            {
                Game1.player.Money += totalValue;
                Game1.playSound("purchaseClick");
                Game1.playSound("coin");
                Game1.addHUDMessage(new HUDMessage($"Sold {itemCount} items for {totalValue}g!", HUDMessage.newQuest_type));
                monitor.Log($"Instant sell: {itemCount} items for {totalValue}g", LogLevel.Debug);
            }
            else
            {
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage("Nothing to sell!", HUDMessage.error_type));
            }
        }

        private void ClearSellGrid()
        {
            int returned = 0;
            
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    if (sellGrid[col, row] != null)
                    {
                        Item item = sellGrid[col, row]!;
                        if (Game1.player.addItemToInventoryBool(item))
                        {
                            sellGrid[col, row] = null;
                            returned++;
                        }
                        else
                        {
                            // Drop on ground if inventory full
                            Game1.createItemDebris(item, Game1.player.Position, -1);
                            sellGrid[col, row] = null;
                            returned++;
                        }
                    }
                }
            }

            if (returned > 0)
            {
                Game1.playSound("coin");
                Game1.addHUDMessage(new HUDMessage($"Returned {returned} items", HUDMessage.achievement_type));
            }
        }

        public void ReceiveScrollWheel(int direction)
        {
            int maxRows = (Game1.player.Items.Count + InventoryColumns - 1) / InventoryColumns;
            int maxScroll = Math.Max(0, maxRows - InventoryRows);

            if (direction > 0 && inventoryScrollOffset > 0)
            {
                inventoryScrollOffset--;
                UpdateInventorySlots();
            }
            else if (direction < 0 && inventoryScrollOffset < maxScroll)
            {
                inventoryScrollOffset++;
                UpdateInventorySlots();
            }
        }

        public void ReceiveKeyPress(Keys key)
        {
            // No text input needed
        }

        public void Draw(SpriteBatch b)
        {
            // Title
            string title = "Instant Sell";
            Vector2 titleSize = Game1.smallFont.MeasureString(title);
            b.DrawString(Game1.smallFont, title,
                new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + 20),
                Color.Gold);

            // Instructions
            b.DrawString(Game1.smallFont, "Click items from inventory to add. Click items in grid to return.",
                new Vector2(bounds.X + 30, bounds.Y + 50),
                Color.LightGray);

            // Draw sell grid background
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(384, 396, 15, 15),
                gridBounds.X - 10, gridBounds.Y - 10, 
                gridBounds.Width + 20, gridBounds.Height + 20,
                new Color(60, 60, 80), 4f, false);

            // Draw grid label
            b.DrawString(Game1.smallFont, "Sell Box (12x3):",
                new Vector2(gridBounds.X, gridBounds.Y - 25),
                Color.Orange);

            // Calculate total value
            int totalValue = 0;

            // Draw sell grid slots
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    Rectangle slotRect = GetGridSlotRect(col, row);
                    bool isHovered = slotRect.Contains(Game1.getMouseX(), Game1.getMouseY());

                    // Slot background
                    Color slotColor = isHovered ? Color.Wheat : Color.White;
                    IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                        new Rectangle(384, 396, 15, 15),
                        slotRect.X, slotRect.Y, slotRect.Width, slotRect.Height,
                        slotColor, 4f, false);

                    // Draw item if present
                    Item? item = sellGrid[col, row];
                    if (item != null)
                    {
                        item.drawInMenu(b, new Vector2(slotRect.X, slotRect.Y), 1f);
                        
                        // Draw price
                        int price = GetSellPrice(item) * item.Stack;
                        totalValue += price;

                        // Set hover info
                        if (isHovered)
                        {
                            hoveredItem = item;
                            hoveredGridX = col;
                            hoveredGridY = row;
                        }
                    }
                }
            }

            // Draw total value
            string totalText = $"Total: {totalValue}g";
            Vector2 totalSize = Game1.smallFont.MeasureString(totalText);
            b.DrawString(Game1.smallFont, totalText,
                new Vector2(gridBounds.Right - totalSize.X, gridBounds.Y - 25),
                Color.LightGreen);

            // Draw buttons
            DrawButton(b, sellButton, $"💰 Sell All ({totalValue}g)", 
                totalValue > 0 ? new Color(150, 255, 150) : Color.Gray);
            DrawButton(b, clearButton, "↩ Return All", new Color(255, 200, 150));

            // Draw inventory section
            b.DrawString(Game1.smallFont, "Your Inventory:",
                new Vector2(inventoryBounds.X, inventoryBounds.Y - 25),
                Color.Orange);

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(384, 396, 15, 15),
                inventoryBounds.X - 10, inventoryBounds.Y - 10,
                inventoryBounds.Width + 20, inventoryBounds.Height + 20,
                new Color(50, 50, 70), 4f, false);

            // Draw inventory slots
            var inventory = Game1.player.Items;
            foreach (var slot in inventorySlots)
            {
                int index = int.Parse(slot.name);
                bool isHovered = slot.containsPoint(Game1.getMouseX(), Game1.getMouseY());

                Color slotColor = isHovered ? Color.Wheat : Color.White;
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                    new Rectangle(384, 396, 15, 15),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height,
                    slotColor, 4f, false);

                if (index < inventory.Count && inventory[index] != null)
                {
                    Item item = inventory[index];
                    item.drawInMenu(b, new Vector2(slot.bounds.X, slot.bounds.Y), 1f);

                    if (isHovered)
                    {
                        hoveredItem = item;
                        hoveredGridX = -1;
                        hoveredGridY = -1;
                    }
                }
            }

            // Draw scroll hint
            int maxRows = (inventory.Count + InventoryColumns - 1) / InventoryColumns;
            if (maxRows > InventoryRows)
            {
                string scrollHint = $"Scroll for more ({inventoryScrollOffset + 1}/{Math.Max(1, maxRows - InventoryRows + 1)})";
                b.DrawString(Game1.smallFont, scrollHint,
                    new Vector2(inventoryBounds.Right - Game1.smallFont.MeasureString(scrollHint).X, inventoryBounds.Y - 25),
                    Color.Gray);
            }

            // Draw hover tooltip
            if (hoveredItem != null)
            {
                int mouseX = Game1.getMouseX();
                int mouseY = Game1.getMouseY();
                
                string name = hoveredItem.DisplayName;
                int sellPrice = GetSellPrice(hoveredItem);
                string priceText = $"Sell: {sellPrice}g each";
                if (hoveredItem.Stack > 1)
                    priceText += $" ({sellPrice * hoveredItem.Stack}g total)";

                // Tooltip background
                Vector2 nameSize = Game1.smallFont.MeasureString(name);
                Vector2 priceSize = Game1.smallFont.MeasureString(priceText);
                int tooltipWidth = (int)Math.Max(nameSize.X, priceSize.X) + 20;
                int tooltipHeight = 50;
                int tooltipX = Math.Min(mouseX + 20, Game1.uiViewport.Width - tooltipWidth - 10);
                int tooltipY = Math.Min(mouseY + 20, Game1.uiViewport.Height - tooltipHeight - 10);

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
                    new Rectangle(384, 396, 15, 15),
                    tooltipX, tooltipY, tooltipWidth, tooltipHeight,
                    new Color(40, 40, 60), 4f, false);

                b.DrawString(Game1.smallFont, name,
                    new Vector2(tooltipX + 10, tooltipY + 5), Color.White);
                b.DrawString(Game1.smallFont, priceText,
                    new Vector2(tooltipX + 10, tooltipY + 25), Color.Gold);
            }

            // Help text
            b.DrawString(Game1.smallFont, "Left-click: Add/Remove item | Right-click: Add one from stack | Scroll: Navigate inventory",
                new Vector2(bounds.X + 30, bounds.Bottom - 40),
                Color.Gray * 0.7f);

            // Reset hover state for next frame
            hoveredItem = null;
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

