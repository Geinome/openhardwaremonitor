/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

namespace OpenHardwareMonitor.Hardware.LPC;

internal enum Chip : ushort
{
    Unknown = 0,

    ATK0110 = 0x0110,

    F71858 = 0x0507,
    F71862 = 0x0601,
    F71869 = 0x0814,
    F71869A = 0x1007,
    F71878Ad = 0x1106,
    F71882 = 0x0541,
    F71889Ad = 0x1005,
    F71889Ed = 0x0909,
    F71889F = 0x0723,
    F71808E = 0x0901,

    It8620E = 0x8620,
    It8628E = 0x8628,
    It8655E = 0x8655,
    It8665E = 0x8665,
    It8686E = 0x8686,
    It8688E = 0x8688,
    It8705F = 0x8705,
    It8712F = 0x8712,
    It8716F = 0x8716,
    It8718F = 0x8718,
    It8720F = 0x8720,
    It8721F = 0x8721,
    It8726F = 0x8726,
    It8728F = 0x8728,
    It879Xe = 0x8733, // IT8792E, IT8795E
    It8771E = 0x8771,
    It8772E = 0x8772,

    Nct6771F = 0xB470,
    Nct6776F = 0xC330,
    Nct610X = 0xC452,
    Nct6779D = 0xC560,
    Nct6791D = 0xC803,
    Nct6792D = 0xC911,
    Nct6792Da = 0xC913,
    Nct6793D = 0xD121,
    Nct6795D = 0xD352,
    Nct6796D = 0xD423,
    Nct6796Dr = 0xD42A,
    Nct6797D = 0xD451,
    Nct6798D = 0xD42B,

    W83627Dhg = 0xA020,
    W83627Dhgp = 0xB070,
    W83627Ehf = 0x8800,
    W83627Hf = 0x5200,
    W83627Thf = 0x8280,
    W83667Hg = 0xA510,
    W83667Hgb = 0xB350,
    W83687Thf = 0x8541
}

internal class ChipName
{
    private ChipName()
    {
    }

    public static string GetName(Chip chip)
    {
        switch (chip)
        {
            case Chip.ATK0110: return "Asus ATK0110";

            case Chip.F71858: return "Fintek F71858";
            case Chip.F71862: return "Fintek F71862";
            case Chip.F71869: return "Fintek F71869";
            case Chip.F71878Ad: return "Fintek F71878AD";
            case Chip.F71869A: return "Fintek F71869A";
            case Chip.F71882: return "Fintek F71882";
            case Chip.F71889Ad: return "Fintek F71889AD";
            case Chip.F71889Ed: return "Fintek F71889ED";
            case Chip.F71889F: return "Fintek F71889F";
            case Chip.F71808E: return "Fintek F71808E";

            case Chip.It8620E: return "ITE IT8620E";
            case Chip.It8628E: return "ITE IT8628E";
            case Chip.It8655E: return "ITE IT8655E";
            case Chip.It8665E: return "ITE IT8665E";
            case Chip.It8686E: return "ITE IT8686E";
            case Chip.It8688E: return "ITE IT8688E";
            case Chip.It8705F: return "ITE IT8705F";
            case Chip.It8712F: return "ITE IT8712F";
            case Chip.It8716F: return "ITE IT8716F";
            case Chip.It8718F: return "ITE IT8718F";
            case Chip.It8720F: return "ITE IT8720F";
            case Chip.It8721F: return "ITE IT8721F";
            case Chip.It8726F: return "ITE IT8726F";
            case Chip.It8728F: return "ITE IT8728F";
            case Chip.It879Xe: return "ITE IT879XE";
            case Chip.It8771E: return "ITE IT8771E";
            case Chip.It8772E: return "ITE IT8772E";

            case Chip.Nct610X: return "Nuvoton NCT610X";

            case Chip.Nct6771F: return "Nuvoton NCT6771F";
            case Chip.Nct6776F: return "Nuvoton NCT6776F";
            case Chip.Nct6779D: return "Nuvoton NCT6779D";
            case Chip.Nct6791D: return "Nuvoton NCT6791D";
            case Chip.Nct6792D: return "Nuvoton NCT6792D+";
            case Chip.Nct6792Da: return "Nuvoton NCT6792D-A";
            case Chip.Nct6793D: return "Nuvoton NCT6793D";
            case Chip.Nct6795D: return "Nuvoton NCT6795D";
            case Chip.Nct6796D: return "Nuvoton NCT6796D";
            case Chip.Nct6796Dr: return "Nuvoton NCT6796D-R";
            case Chip.Nct6797D: return "Nuvoton NCT6797D";
            case Chip.Nct6798D: return "Nuvoton NCT6798D";

            case Chip.W83627Dhg: return "Winbond W83627DHG";
            case Chip.W83627Dhgp: return "Winbond W83627DHG-P";
            case Chip.W83627Ehf: return "Winbond W83627EHF";
            case Chip.W83627Hf: return "Winbond W83627HF";
            case Chip.W83627Thf: return "Winbond W83627THF";
            case Chip.W83667Hg: return "Winbond W83667HG";
            case Chip.W83667Hgb: return "Winbond W83667HG-B";
            case Chip.W83687Thf: return "Winbond W83687THF";

            case Chip.Unknown: return "Unkown";
            default: return "Unknown";
        }
    }
}
