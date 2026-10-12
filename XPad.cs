using System;
using System.Runtime.InteropServices;

namespace GK2Skateboard
{
    /// <summary>
    /// Direct XInput read for pad 0-3. Used instead of the game's Rewired actions so skate buttons never share a binding with
    /// game actions (A = Interaction in GK2). Steam Input presents PlayStation/Switch pads as XInput too.
    /// </summary>
    internal static class XPad
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct State
        {
            public uint packet;
            public ushort buttons;
            public byte leftTrigger;
            public byte rightTrigger;
            public short lx, ly, rx, ry;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int GetState14(int user, out State state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern int GetState910(int user, out State state);

        private static bool use910;
        private static bool unavailable;
        private static int prevMask, curMask;

        // Bit layout: XInput button bits, plus LT/RT as virtual bits above 16.
        private const int LT = 1 << 16, RT = 1 << 17;

        public static int MaskFor(string name)
        {
            switch ((name ?? "").Trim().ToUpperInvariant())
            {
                case "DPADUP": return 0x0001;
                case "DPADDOWN": return 0x0002;
                case "DPADLEFT": return 0x0004;
                case "DPADRIGHT": return 0x0008;
                case "START": return 0x0010;
                case "BACK": return 0x0020;
                case "L3": return 0x0040;
                case "R3": return 0x0080;
                case "LB": return 0x0100;
                case "RB": return 0x0200;
                case "A": return 0x1000;
                case "B": return 0x2000;
                case "X": return 0x4000;
                case "Y": return 0x8000;
                case "LT": return LT;
                case "RT": return RT;
                default: return 0;
            }
        }

        /// <summary>Poll once per frame.</summary>
        public static void Poll()
        {
            prevMask = curMask;
            curMask = 0;
            if (unavailable) return;
            for (int user = 0; user < 4; user++)
            {
                State s;
                int rc;
                try
                {
                    rc = use910 ? GetState910(user, out s) : GetState14(user, out s);
                }
                catch (DllNotFoundException)
                {
                    if (!use910) { use910 = true; return; }
                    unavailable = true;
                    Plugin.Log.LogWarning("XInput not available; gamepad skate buttons disabled.");
                    return;
                }
                catch (EntryPointNotFoundException)
                {
                    unavailable = true;
                    return;
                }
                if (rc != 0) continue;
                curMask |= s.buttons;
                if (s.leftTrigger > 128) curMask |= LT;
                if (s.rightTrigger > 128) curMask |= RT;
            }
        }

        public static bool Down(int mask) => mask != 0 && (curMask & mask) == mask && (prevMask & mask) != mask;
    }
}
