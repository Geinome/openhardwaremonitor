/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

namespace OpenHardwareMonitor.Hardware.Mainboard;

internal class Identification
{
    public static Manufacturer GetManufacturer(string name)
    {
        switch (name)
        {
            case "acer":
            case "Acer":
            case "ACER":
            case "Acer, Inc.":
                return Manufacturer.Acer;
            case "AMD":
            case "AMD Corp.":
            case "AMD Corporation":
                return Manufacturer.AMD;
            case "Alienware":
                return Manufacturer.Alienware;
            case "AOpen":
            case "AOpen Inc.":
                return Manufacturer.AOpen;
            case "Apple Computer, Inc.":
            case "Apple Inc.":
                return Manufacturer.Apple;
            case "ASRock":
                return Manufacturer.AsRock;
            case "ASUS CORPORATION":
            case "ASUSTek Computer Inc.":
            case "ASUSTek Computer INC.":
            case "ASUSTeK Computer Inc.":
            case "ASUSTeK Computer INC.":
            case "ASUSTeK COMPUTER INC.":
                return Manufacturer.ASUS;
            case "Biostar":
            case "Biostar Group":
            case "BIOSTAR Group":
                return Manufacturer.Biostar;
            case "clevo":
            case "Clevo":
            case "CLEVO":
            case "CLEVO Co.":
            case "CLEVO CO.":
                return Manufacturer.Clevo;
            case "Dell Computer Corp.":
            case "Dell Computer Corporation":
            case "Dell Inc":
            case "Dell Inc.":
            case "DELL Inc.":
            case "DellInc.":
                return Manufacturer.Dell;
            case "DFI":
            case "DFI Inc.":
                return Manufacturer.DFI;
            case "ECS":
            case "ELITEGROUP":
            case "ELITEGROUP COMPUTER SYSTEM CO.,LTD.":
                return Manufacturer.ECS;
            case "EPoX COMPUTER CO., LTD":
                return Manufacturer.EPoX;
            case "EVGA":
            case "EVGA INTERNATIONAL CO.,LTD":
                return Manufacturer.EVGA;
            case "FIC":
            case "First International Computer, Inc.":
                return Manufacturer.FIC;
            case "Foxconn":
            case "FOXCONN":
                return Manufacturer.Foxconn;
            case "FUJITSU":
            case "FUJITSU SIEMENS":
            case "FUJITSU-SV":
                return Manufacturer.Fujitsu;
            case "Gateway":
            case "GATEWAY":
                return Manufacturer.Gateway;
            case "Gigabyte":
            case "GIGABYTE":
            case "Gigabyte Technology Co., Ltd.":
            case "Gigabyte Technology Co.,Ltd":
            case "Gigabyte Technology Co.,Ltd.":
            case "Gigabyte Tecohnology Co., Ltd.":
                return Manufacturer.Gigabyte;
            case "Hewleet-Packard":
            case "Hewlett-Packard":
            case "HP":
                return Manufacturer.HP;
            case "http://www.abit.com.tw/":
            case "www.abit.com.tw":
                return Manufacturer.Abit;
            case "IBM":
                return Manufacturer.IBM;
            case "Intel":
            case "INTEL":
            case "Intel Corp.":
            case "Intel Corporation":
            case "INTEL Corporation":
            case "Intel.":
                return Manufacturer.Intel;
            case "JETWAY":
                return Manufacturer.Jetway;
            case "LattePanda":
                return Manufacturer.LattePanda;
            case "Lenovo":
            case "LENOVO":
                return Manufacturer.Lenovo;
            case "Medion":
            case "MEDION":
            case "MEDIONPC":
                return Manufacturer.Medion;
            case "Microsoft Corporation":
                return Manufacturer.Microsoft;
            case "Micro Star":
            case "Micro-Star":
            case "MICRO-STAR INC.":
            case "MICRO-STAR INTERANTIONAL CO.,LTD":
            case "MICRO-STAR INTERANTONAL CO.,LTD":
            case "Micro-Star International":
            case "Micro-Star International Co., Ltd":
            case "MICRO-STAR INTERNATIONAL CO., LTD":
            case "Micro-Star International Co., Ltd.":
            case "MICRO-STAR INTERNATIONAL CO.,LTD":
            case "msi":
            case "MSI":
                return Manufacturer.MSI;
            case "NEC":
            case "NEC COMPUTERS INTERNATIONAL":
                return Manufacturer.NEC;
            case "PEGATRON CORPORATION":
            case "PEGATRON CORPORATION.":
            case "PEGATRON INC.":
                return Manufacturer.Pegatron;
            case "SAMSUNG ELECTRONICS CO., LTD.":
            case "SAMSUNG ELECTRONICS CO.,LTD":
                return Manufacturer.Samsung;
            case "SAPPHIRE":
            case "Sapphire Tech":
                return Manufacturer.Sapphire;
            case "Shuttle":
            case "Shuttle Inc":
            case "Shuttle Inc.":
                return Manufacturer.Shuttle;
            case "Sony Corporation":
                return Manufacturer.Sony;
            case "Supermicro":
                return Manufacturer.Supermicro;
            case "TOSHIBA":
                return Manufacturer.Toshiba;
            case "XFX":
                return Manufacturer.XFX;
            case "ZOTAC":
                return Manufacturer.ZOTAC;
            case "To be filled by O.E.M.":
                return Manufacturer.Unknown;
            default:
                return Manufacturer.Unknown;
        }
    }

    public static Model GetModel(string name)
    {
        switch (name)
        {
            case "880GMH/USB3":
                return Model._880GMH_USB3;
            case "ASRock AOD790GX/128M":
                return Model.Aod790Gx128M;
            case "P55 Deluxe":
                return Model.P55Deluxe;
            case "Crosshair III Formula":
                return Model.CrosshairIiiFormula;
            case "M2N-SLI DELUXE":
                return Model.M2NSliDeluxe;
            case "M4A79XTD EVO":
                return Model.M4A79XtdEvo;
            case "P5W DH Deluxe":
                return Model.P5WDhDeluxe;
            case "P6T":
                return Model.P6T;
            case "P6X58D-E":
                return Model.P6X58DE;
            case "P8P67":
                return Model.P8P67;
            case "P8P67 EVO":
                return Model.P8P67Evo;
            case "P8P67 PRO":
                return Model.P8P67Pro;
            case "P8P67-M PRO":
                return Model.P8P67MPro;
            case "P8Z77-V":
                return Model.P8Z77V;
            case "P9X79":
                return Model.P9X79;
            case "Rampage Extreme":
                return Model.RampageExtreme;
            case "Rampage II GENE":
                return Model.RampageIiGene;
            case "LP BI P45-T2RS Elite":
                return Model.LpBiP45T2RsElite;
            case "LP DK P55-T3eH9":
                return Model.LpDkP55T3EH9;
            case "A890GXM-A":
                return Model.A890GxmA;
            case "X58 SLI Classified":
                return Model.X58SliClassified;
            case "965P-S3":
                return Model._965P_S3;
            case "EP45-DS3R":
                return Model.Ep45Ds3R;
            case "EP45-UD3R":
                return Model.Ep45Ud3R;
            case "EX58-EXTREME":
                return Model.EX58_EXTREME;
            case "EX58-UD3R":
                return Model.Ex58Ud3R;
            case "G41M-Combo":
                return Model.G41MCombo;
            case "G41MT-S2":
                return Model.G41MtS2;
            case "G41MT-S2P":
                return Model.G41MtS2P;
            case "GA-970A-UD3":
                return Model.Ga970AUd3;
            case "GA-MA770T-UD3":
                return Model.GaMa770TUd3;
            case "GA-MA770T-UD3P":
                return Model.GaMa770TUd3P;
            case "GA-MA785GM-US2H":
                return Model.GaMa785GmUs2H;
            case "GA-MA785GMT-UD2H":
                return Model.GaMa785GmtUd2H;
            case "GA-MA78LM-S2H":
                return Model.GaMa78LmS2H;
            case "GA-MA790X-UD3P":
                return Model.GaMa790XUd3P;
            case "H55-USB3":
                return Model.H55_USB3;
            case "H55N-USB3":
                return Model.H55NUsb3;
            case "H61M-DS2 REV 1.2":
                return Model.H61MDs2Rev12;
            case "H61M-USB3-B3 REV 2.0":
                return Model.H61MUsb3B3Rev20;
            case "H67A-UD3H-B3":
                return Model.H67AUd3HB3;
            case "H67A-USB3-B3":
                return Model.H67AUsb3B3;
            case "P35-DS3":
                return Model.P35_DS3;
            case "P35-DS3L":
                return Model.P35Ds3L;
            case "P55-UD4":
                return Model.P55_UD4;
            case "P55A-UD3":
                return Model.P55AUd3;
            case "P55M-UD4":
                return Model.P55MUd4;
            case "P67A-UD3-B3":
                return Model.P67AUd3B3;
            case "P67A-UD3R-B3":
                return Model.P67AUd3RB3;
            case "P67A-UD4-B3":
                return Model.P67AUd4B3;
            case "P8Z68-V PRO":
                return Model.P8Z68VPro;
            case "X38-DS5":
                return Model.X38_DS5;
            case "X570 AORUS MASTER":
                return Model.X570_AORUS_MASTER;
            case "X58A-UD3R":
                return Model.X58AUd3R;
            case "Z68A-D3H-B3":
                return Model.Z68AD3HB3;
            case "Z68AP-D3":
                return Model.Z68ApD3;
            case "Z68X-UD3H-B3":
                return Model.Z68XUd3HB3;
            case "Z68X-UD7-B3":
                return Model.Z68XUd7B3;
            case "Z390 M GAMING-CF":
                return Model.Z390_M_GAMING;
            case "Z390 AORUS ULTRA":
                return Model.Z390_AORUS_ULTRA;
            case "Z390 UD":
                return Model.Z390_UD;
            case "FH67":
                return Model.FH67;
            case "Base Board Product Name":
            case "To be filled by O.E.M.":
                return Model.Unknown;
            default:
                return Model.Unknown;
        }
    }
}
