using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using GenericModConfigMenu;

namespace MyControllerMod
{
    public class ModConfig
    {
        public KeybindList SuppressedButtons { get; set; } = new KeybindList();
        public SButton DropItemButton { get; set; } = SButton.DPadDown;
    }

    public class ModEntry : Mod
    {
        private ModConfig Config;

        // 可丢弃工具名称列表（仅这些工具可丢弃）
        private static readonly HashSet<string> DisposableToolNames = new()
        {
            "FishingRod",
            "AutoGrabber",
            "Heater",
            "CopperPan"
        };

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            this.Monitor.Log($"{this.ModManifest.Name} 已加载。", LogLevel.Info);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                this.Monitor.Log("未检测到 GMCM，配置菜单不可用。", LogLevel.Warn);
                return;
            }

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            gmcm.AddSectionTitle(this.ModManifest, () => "手柄按键抑制");
            gmcm.AddKeybindList(
                this.ModManifest,
                getValue: () => this.Config.SuppressedButtons,
                setValue: (val) => this.Config.SuppressedButtons = val,
                name: () => "被抑制的按键",
                tooltip: () => "这些按键的原版功能将被禁用。"
            );

            gmcm.AddSectionTitle(this.ModManifest, () => "一键丢弃物品");
            gmcm.AddKeybind(
                this.ModManifest,
                getValue: () => this.Config.DropItemButton,
                setValue: (val) => this.Config.DropItemButton = val,
                name: () => "丢弃物品按键",
                tooltip: () => "在背包中拿起物品后按此键，将物品丢出。遵循原版规则：武器可丢，大部分工具不可丢。"
            );
        }

        private bool IsInventoryMenu()
        {
            if (Game1.activeClickableMenu is InventoryPage)
                return true;
            if (Game1.activeClickableMenu is GameMenu menu && menu.currentTab == 0)
                return true;
            return false;
        }

        private bool CanDropItem(Item item)
        {
            if (item == null) return false;
            if (item is MeleeWeapon) return true;
            if (item is Tool tool)
                return DisposableToolNames.Contains(tool.GetType().Name);
            return true;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            SButton pressed = e.Button;

            // ---------- 丢弃功能 ----------
            if (pressed == this.Config.DropItemButton)
            {
                if (IsInventoryMenu())
                {
                    var player = Game1.player;
                    Item itemToDrop = player.CursorSlotItem;
                    if (itemToDrop != null)
                    {
                        if (CanDropItem(itemToDrop))
                        {
                            player.CursorSlotItem = null;
                            Game1.createItemDebris(itemToDrop, player.getStandingPosition(), player.FacingDirection);
                            this.Monitor.Log($"丢弃了 {itemToDrop.DisplayName} x{itemToDrop.Stack}", LogLevel.Info);
                        }
                        else
                        {
                            this.Monitor.Log($"无法丢弃 {itemToDrop.DisplayName}，原版规则禁止丢弃。", LogLevel.Info);
                        }
                    }
                    this.Helper.Input.Suppress(pressed);
                }
                else
                {
                    this.Helper.Input.Suppress(pressed);
                }
                return;
            }

            // ---------- B 键：关闭任何菜单，如果是背包则取消拖拽并归位或掉落 ----------
            if (pressed == SButton.ControllerB && Game1.activeClickableMenu != null)
            {
                this.Helper.Input.Suppress(pressed);

                // 如果是背包菜单，处理悬空物品
                if (IsInventoryMenu())
                {
                    var player = Game1.player;
                    Item heldItem = player.CursorSlotItem;
                    if (heldItem != null)
                    {
                        bool placed = false;
                        // 尝试放回原槽位（当前选中槽）
                        int slot = player.CurrentToolIndex;
                        if (slot >= 0 && slot < player.Items.Count && player.Items[slot] == null)
                        {
                            player.Items[slot] = heldItem;
                            placed = true;
                        }
                        else
                        {
                            // 原槽位被占用，找第一个空位
                            for (int i = 0; i < player.Items.Count; i++)
                            {
                                if (player.Items[i] == null)
                                {
                                    player.Items[i] = heldItem;
                                    placed = true;
                                    break;
                                }
                            }
                        }

                        if (!placed)
                        {
                            // 背包已满，无法放入，将物品掉落在地上
                            Game1.createItemDebris(heldItem, player.getStandingPosition(), player.FacingDirection);
                            this.Monitor.Log($"背包已满，物品 {heldItem.DisplayName} 掉落在地上", LogLevel.Info);
                        }
                        else
                        {
                            this.Monitor.Log($"物品 {heldItem.DisplayName} 已归位", LogLevel.Trace);
                        }
                        // 清空光标
                        player.CursorSlotItem = null;
                    }
                }

                // 关闭当前菜单
                Game1.activeClickableMenu.exitThisMenu();
                this.Monitor.Log($"B键关闭了菜单", LogLevel.Trace);
                return;
            }

            // ---------- 其他抑制按键 ----------
            bool inSuppressedList = false;
            foreach (var keybind in this.Config.SuppressedButtons.Keybinds)
            {
                if (Array.IndexOf(keybind.Buttons, pressed) >= 0)
                {
                    inSuppressedList = true;
                    break;
                }
            }
            if (inSuppressedList)
            {
                this.Helper.Input.Suppress(pressed);
            }
        }
    }
}