/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Text;

namespace OpenHardwareMonitor.Hardware.CPU;

internal enum Vendor
{
    Unknown,
    Intel,
    AMD
}

internal class Cpuid
{
    private readonly int _group;
    private readonly int _thread;
    private readonly GroupAffinity _affinity;

    private readonly Vendor _vendor = Vendor.Unknown;

    private readonly string _cpuBrandString = "";
    private readonly string _name = "";

    private readonly uint[,] _cpuidData = new uint[0, 0];
    private readonly uint[,] _cpuidExtData = new uint[0, 0];

    private readonly uint _family;
    private readonly uint _model;
    private readonly uint _stepping;

    private readonly uint _apicId;

    private readonly uint _threadMaskWith;
    private readonly uint _coreMaskWith;

    private readonly uint _processorId;
    private readonly uint _coreId;
    private readonly uint _threadId;

    public const uint Cpuid0 = 0;
    public const uint CpuidExt = 0x80000000;

    private static void AppendRegister(StringBuilder b, uint value)
    {
        b.Append((char)(value & 0xff));
        b.Append((char)((value >> 8) & 0xff));
        b.Append((char)((value >> 16) & 0xff));
        b.Append((char)((value >> 24) & 0xff));
    }

    private static uint NextLog2(long x)
    {
        if (x <= 0)
            return 0;

        x--;
        uint count = 0;
        while (x > 0)
        {
            x >>= 1;
            count++;
        }

        return count;
    }

    public static Cpuid Get(int group, int thread)
    {
        if (thread >= 64)
            return null;

        var affinity = GroupAffinity.Single((ushort)group, thread);

        var previousAffinity = ThreadAffinity.Set(affinity);
        if (previousAffinity == GroupAffinity.Undefined)
            return null;

        try
        {
            return new Cpuid(group, thread, affinity);
        }
        finally
        {
            ThreadAffinity.Set(previousAffinity);
        }
    }

    private Cpuid(int group, int thread, GroupAffinity affinity)
    {
        this._group = group;
        this._thread = thread;
        this._affinity = affinity;

        uint maxCpuid = 0;
        uint maxCpuidExt = 0;

        uint eax, ebx, ecx, edx;

        Opcode.Cpuid(Cpuid0, 0, out eax, out ebx, out ecx, out edx);
        if (eax > 0)
            maxCpuid = eax;
        else
            return;

        var vendorBuilder = new StringBuilder();
        AppendRegister(vendorBuilder, ebx);
        AppendRegister(vendorBuilder, edx);
        AppendRegister(vendorBuilder, ecx);
        var cpuVendor = vendorBuilder.ToString();
        switch (cpuVendor)
        {
            case "GenuineIntel":
                _vendor = Vendor.Intel;
                break;
            case "AuthenticAMD":
                _vendor = Vendor.AMD;
                break;
            default:
                _vendor = Vendor.Unknown;
                break;
        }

        eax = ebx = ecx = edx = 0;
        Opcode.Cpuid(CpuidExt, 0, out eax, out ebx, out ecx, out edx);
        if (eax > CpuidExt)
            maxCpuidExt = eax - CpuidExt;
        else
            return;

        maxCpuid = Math.Min(maxCpuid, 1024);
        maxCpuidExt = Math.Min(maxCpuidExt, 1024);

        _cpuidData = new uint[maxCpuid + 1, 4];
        for (uint i = 0; i < maxCpuid + 1; i++)
            Opcode.Cpuid(Cpuid0 + i, 0,
                out _cpuidData[i, 0], out _cpuidData[i, 1],
                out _cpuidData[i, 2], out _cpuidData[i, 3]);

        _cpuidExtData = new uint[maxCpuidExt + 1, 4];
        for (uint i = 0; i < maxCpuidExt + 1; i++)
            Opcode.Cpuid(CpuidExt + i, 0,
                out _cpuidExtData[i, 0], out _cpuidExtData[i, 1],
                out _cpuidExtData[i, 2], out _cpuidExtData[i, 3]);

        var nameBuilder = new StringBuilder();
        for (uint i = 2; i <= 4; i++)
        {
            Opcode.Cpuid(CpuidExt + i, 0, out eax, out ebx, out ecx, out edx);
            AppendRegister(nameBuilder, eax);
            AppendRegister(nameBuilder, ebx);
            AppendRegister(nameBuilder, ecx);
            AppendRegister(nameBuilder, edx);
        }

        nameBuilder.Replace('\0', ' ');
        _cpuBrandString = nameBuilder.ToString().Trim();
        nameBuilder.Replace("Dual-Core Processor", "");
        nameBuilder.Replace("Triple-Core Processor", "");
        nameBuilder.Replace("Quad-Core Processor", "");
        nameBuilder.Replace("Six-Core Processor", "");
        nameBuilder.Replace("Eight-Core Processor", "");
        nameBuilder.Replace("Dual Core Processor", "");
        nameBuilder.Replace("Quad Core Processor", "");
        nameBuilder.Replace("12-Core Processor", "");
        nameBuilder.Replace("16-Core Processor", "");
        nameBuilder.Replace("24-Core Processor", "");
        nameBuilder.Replace("32-Core Processor", "");
        nameBuilder.Replace("64-Core Processor", "");
        nameBuilder.Replace("6-Core Processor", "");
        nameBuilder.Replace("8-Core Processor", "");
        nameBuilder.Replace("with Radeon Vega Mobile Gfx", "");
        nameBuilder.Replace("w/ Radeon Vega Mobile Gfx", "");
        nameBuilder.Replace("with Radeon Vega Graphics", "");
        nameBuilder.Replace("with Radeon Graphics", "");
        nameBuilder.Replace("APU with Radeon(tm) HD Graphics", "");
        nameBuilder.Replace("APU with Radeon(TM) HD Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R2 Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R3 Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R4 Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R5 Graphics", "");
        nameBuilder.Replace("APU with Radeon(tm) R3", "");
        nameBuilder.Replace("RADEON R2, 4 COMPUTE CORES 2C+2G", "");
        nameBuilder.Replace("RADEON R4, 5 COMPUTE CORES 2C+3G", "");
        nameBuilder.Replace("RADEON R5, 5 COMPUTE CORES 2C+3G", "");
        nameBuilder.Replace("RADEON R5, 10 COMPUTE CORES 4C+6G", "");
        nameBuilder.Replace("RADEON R7, 10 COMPUTE CORES 4C+6G", "");
        nameBuilder.Replace("RADEON R7, 12 COMPUTE CORES 4C+8G", "");
        nameBuilder.Replace("Radeon R5, 6 Compute Cores 2C+4G", "");
        nameBuilder.Replace("Radeon R5, 8 Compute Cores 4C+4G", "");
        nameBuilder.Replace("Radeon R6, 10 Compute Cores 4C+6G", "");
        nameBuilder.Replace("Radeon R7, 10 Compute Cores 4C+6G", "");
        nameBuilder.Replace("Radeon R7, 12 Compute Cores 4C+8G", "");
        nameBuilder.Replace("R5, 10 Compute Cores 4C+6G", "");
        nameBuilder.Replace("R7, 12 COMPUTE CORES 4C+8G", "");
        nameBuilder.Replace("(R)", " ");
        nameBuilder.Replace("(TM)", " ");
        nameBuilder.Replace("(tm)", " ");
        nameBuilder.Replace("CPU", " ");

        for (var i = 0; i < 10; i++) nameBuilder.Replace("  ", " ");
        _name = nameBuilder.ToString();
        if (_name.Contains("@"))
            _name = _name.Remove(_name.LastIndexOf('@'));
        _name = _name.Trim();

        _family = ((_cpuidData[1, 0] & 0x0FF00000) >> 20) +
                 ((_cpuidData[1, 0] & 0x0F00) >> 8);
        _model = ((_cpuidData[1, 0] & 0x0F0000) >> 12) +
                ((_cpuidData[1, 0] & 0xF0) >> 4);
        _stepping = _cpuidData[1, 0] & 0x0F;

        _apicId = (_cpuidData[1, 1] >> 24) & 0xFF;

        switch (_vendor)
        {
            case Vendor.Intel:
                var maxCoreAndThreadIdPerPackage = (_cpuidData[1, 1] >> 16) & 0xFF;
                uint maxCoreIdPerPackage;
                if (maxCpuid >= 4)
                    maxCoreIdPerPackage = ((_cpuidData[4, 0] >> 26) & 0x3F) + 1;
                else
                    maxCoreIdPerPackage = 1;
                _threadMaskWith =
                    NextLog2(maxCoreAndThreadIdPerPackage / maxCoreIdPerPackage);
                _coreMaskWith = NextLog2(maxCoreIdPerPackage);
                break;
            case Vendor.AMD:
                if (_family == 0x17 || _family == 0x19)
                {
                    _coreMaskWith = (_cpuidExtData[8, 2] >> 12) & 0xF;
                    _threadMaskWith =
                        NextLog2(((_cpuidExtData[0x1E, 1] >> 8) & 0xFF) + 1);
                }
                else
                {
                    uint corePerPackage;
                    if (maxCpuidExt >= 8)
                        corePerPackage = (_cpuidExtData[8, 2] & 0xFF) + 1;
                    else
                        corePerPackage = 1;
                    _coreMaskWith = NextLog2(corePerPackage);
                    _threadMaskWith = 0;
                }

                break;
            default:
                _threadMaskWith = 0;
                _coreMaskWith = 0;
                break;
        }

        _processorId = _apicId >> (int)(_coreMaskWith + _threadMaskWith);
        _coreId = (_apicId >> (int)_threadMaskWith)
                 - (_processorId << (int)_coreMaskWith);
        _threadId = _apicId
                   - (_processorId << (int)(_coreMaskWith + _threadMaskWith))
                   - (_coreId << (int)_threadMaskWith);
    }

    public string Name => _name;

    public string BrandString => _cpuBrandString;

    public int Group => _group;

    public int Thread => _thread;

    public GroupAffinity Affinity => _affinity;

    public Vendor Vendor => _vendor;

    public uint Family => _family;

    public uint Model => _model;

    public uint Stepping => _stepping;

    public uint ApicId => _apicId;

    public uint ProcessorId => _processorId;

    public uint CoreId => _coreId;

    public uint ThreadId => _threadId;

    public uint[,] Data => _cpuidData;

    public uint[,] ExtData => _cpuidExtData;
}
