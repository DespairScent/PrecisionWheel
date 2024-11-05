using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;
using System.Reflection.Emit;
using StardewModdingAPI.Events;
using StardewValley;

namespace DespairScent.PrecisionWheel
{
    internal sealed class ModEntry : Mod
    {

        private static ModConfig Config;

        private static bool movingWithWheel = false;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            var harmony = new Harmony(this.ModManifest.UniqueID);

            harmony.Patch(AccessTools.Method(typeof(InventoryMenu), nameof(InventoryMenu.rightClick)),
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(InventoryMenuPatch)));
            
            helper.Events.Input.MouseWheelScrolled += this.OnWheelScrolled;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
            {
                return;
            }

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddKeybindList(
                mod: this.ModManifest,
                name: () => "Toogle keybind (x1)",
                tooltip: () => "A button that must be held down to allow one item to be moved with the mouse wheel",
                getValue: () => Config.keybind1,
                setValue: value => Config.keybind1 = value
            );

            configMenu.AddKeybindList(
                mod: this.ModManifest,
                name: () => "Toogle keybind (x10)",
                tooltip: () => "A button that must be held down to allow 10 items to be moved with the mouse wheel",
                getValue: () => Config.keybind10,
                setValue: value => Config.keybind10 = value
            );

            configMenu.AddKeybindList(
                mod: this.ModManifest,
                name: () => "Toogle keybind (x100)",
                tooltip: () => "A button that must be held down to allow 100 items to be moved with the mouse wheel",
                getValue: () => Config.keybind100,
                setValue: value => Config.keybind100 = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Keybind inheritance",
                tooltip: () => "Determines whether it is necessary to hold down the keybinds for fewer items to activate the current",
                getValue: () => Config.keybindInheritance,
                setValue: value => Config.keybindInheritance = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Reverse wheel",
                tooltip: () => "Reverses wheel direction",
                getValue: () => Config.reverseWheel,
                setValue: value => Config.reverseWheel = value
            );
        }


        private void OnWheelScrolled(object? sender, MouseWheelScrolledEventArgs e)
        {
            if (e.Delta != 0 && Game1.activeClickableMenu is ItemGrabMenu && GetCountFromKeybins() != 0)
            {
                var itemGrabMenu = (ItemGrabMenu)Game1.activeClickableMenu;
                Game1.PushUIMode();
                movingWithWheel = true;

                Item hoverItem = itemGrabMenu.inventory.getItemAt(Game1.getMouseX(), Game1.getMouseY());

                if (hoverItem != null) // player inventory
                {
                    if (e.Delta > 0 ^ Config.reverseWheel)
                    {
                        Game1.activeClickableMenu.receiveRightClick(Game1.getMouseX(), Game1.getMouseY());
                    }
                    else
                    {
                        PullFromInventory(itemGrabMenu.ItemsToGrabMenu, hoverItem);
                    }
                }
                else // storage
                {
                    if (e.Delta < 0 ^ Config.reverseWheel)
                    {
                        Game1.activeClickableMenu.receiveRightClick(Game1.getMouseX(), Game1.getMouseY());
                    }
                    else if ((hoverItem = itemGrabMenu.ItemsToGrabMenu.getItemAt(Game1.getMouseX(), Game1.getMouseY())) != null)
                    {
                        PullFromInventory(itemGrabMenu.inventory, hoverItem);
                    }
                }
                Game1.PopUIMode();
                movingWithWheel = false;
            }
        }

        private static void PullFromInventory(InventoryMenu inventory, Item item)
        {
            if (item.Stack >= item.maximumStackSize())
            {
                return;
            }
            for (int i = 0; i < inventory.actualInventory.Count; i++)
            {
                if (inventory.actualInventory[i]?.canStackWith(item) == true)
                {
                    var bounds = inventory.inventory[i].bounds;
                    Game1.activeClickableMenu.receiveRightClick(bounds.X, bounds.Y);
                    return;
                }
            }
        }

        // TO-DO: handle (toAddTo != null) block
        public static IEnumerable<CodeInstruction> InventoryMenuPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            Label? labeltoAddToNull = null;
            int index_toAddToNull_StackUpdate = -1;

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Brtrue && codes[i - 1].opcode == OpCodes.Ldarg_3)
                {
                    labeltoAddToNull = (Label)codes[i].operand;
                }
                if (labeltoAddToNull != null && codes[i].labels.Contains((Label)labeltoAddToNull))
                {
                    break; // end of (toAddTo == null) block
                }

                if (labeltoAddToNull != null && codes[i].opcode == OpCodes.Callvirt && codes[i].operand == AccessTools.PropertySetter(typeof(Item), nameof(Item.Stack)))
                {
                    index_toAddToNull_StackUpdate = i;
                }
            }

            if (index_toAddToNull_StackUpdate != -1)
            {
                var instructionsToInsert = new List<CodeInstruction>
                {
                    new CodeInstruction(OpCodes.Ldarg_0), // (InventoryMenu) this               
                    new CodeInstruction(OpCodes.Ldloc_1), // (int) index
                    new CodeInstruction(OpCodes.Ldloc_S, 5), // (Item) newItem
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(Patch_rightClick_toAddToNull)))
                };

                codes.InsertRange(index_toAddToNull_StackUpdate + 1, instructionsToInsert);
            }

            return codes.AsEnumerable();
        }

        public static void Patch_rightClick_toAddToNull(InventoryMenu inventory, int index, Item newItem)
        {
            if (!movingWithWheel)
            {
                return;
            }

            int count = Math.Min(inventory.actualInventory[index].Stack, GetCountFromKeybins());
            if (count == 0)
            {
                return;
            }

            newItem.Stack = count;

            return;
        }

        private static int GetCountFromKeybins()
        {
            int count = 0;
            if (Config.keybindInheritance)
            {
                if (Config.keybind1.Keybinds.Length == 0 || Config.keybind1.IsDown())
                {
                    count = 1;
                    if (Config.keybind10.Keybinds.Length == 0 || Config.keybind10.IsDown())
                    {
                        count = 10;
                        if (Config.keybind10.Keybinds.Length == 0 || Config.keybind100.IsDown())
                        {
                            count = 100;
                        }
                    }
                }
            }
            else
            {
                if (Config.keybind100.Keybinds.Length == 0 || Config.keybind100.IsDown())
                {
                    count = 100;
                }
                else if (Config.keybind10.Keybinds.Length == 0 || Config.keybind10.IsDown())
                {
                    count = 10;
                }
                else if (Config.keybind1.Keybinds.Length == 0 || Config.keybind1.IsDown())
                {
                    count = 1;
                }
            }

            return count;
        }

    }
}
