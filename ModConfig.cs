using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DespairScent.PrecisionWheel
{
    internal class ModConfig
    {
        public KeybindList keybind1 = new KeybindList();
        public KeybindList keybind10 = new KeybindList(SButton.LeftShift);
        public KeybindList keybind100 = new KeybindList(SButton.LeftControl);

        public bool keybindInheritance = false;

        public bool reverseWheel = false;
    }
}
