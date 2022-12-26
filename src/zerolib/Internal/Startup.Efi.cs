// bflat minimal runtime library
// Copyright (C) 2021-2022 Michal Strehovsky
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

#if UEFI

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    unsafe partial class StartupCodeHelpers
    {
        internal static EFI_SYSTEM_TABLE* s_efiSystemTable;

        [RuntimeImport("*", "__managed__Main")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        static extern int ManagedMain(int argc, char** argv);

        [RuntimeExport("EfiMain")]
        static long EfiMain(IntPtr imageHandle, EFI_SYSTEM_TABLE* systemTable)
        {
            s_efiSystemTable = systemTable;
            ManagedMain(0, null);

            while (true) ;
        }

        internal static unsafe void InitializeCommandLineArgsW(int argc, char** argv)
        {
            // argc and argv are garbage because EfiMain didn't pass any
        }

        private static string[] GetMainMethodArguments()
        {
            return new string[0];
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EFI_HANDLE
    {
        private IntPtr _handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly struct EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL
    {
        private readonly IntPtr _pad0;
        public readonly delegate* unmanaged<void*, char*, void*> OutputString;
        private readonly IntPtr _pad1;
        private readonly IntPtr _pad2;
        private readonly IntPtr _pad3;
        public readonly delegate* unmanaged<void*, uint, void> SetAttribute;
        private readonly IntPtr _pad4;
        public readonly delegate* unmanaged<void*, uint, uint, void> SetCursorPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly struct EFI_INPUT_KEY
    {
        public readonly ushort ScanCode;
        public readonly ushort UnicodeChar;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly struct EFI_SIMPLE_TEXT_INPUT_PROTOCOL
    {
        private readonly IntPtr _pad0;
        public readonly delegate*<void*, EFI_INPUT_KEY*, ulong> ReadKeyStroke;
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly struct EFI_TABLE_HEADER
    {
        public readonly ulong Signature;
        public readonly uint Revision;
        public readonly uint HeaderSize;
        public readonly uint Crc32;
        public readonly uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly struct EFI_SYSTEM_TABLE
    {
        public readonly EFI_TABLE_HEADER Hdr;
        public readonly char* FirmwareVendor;
        public readonly uint FirmwareRevision;
        public readonly EFI_HANDLE ConsoleInHandle;
        public readonly EFI_SIMPLE_TEXT_INPUT_PROTOCOL* ConIn;
        public readonly EFI_HANDLE ConsoleOutHandle;
        public readonly EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL* ConOut;
        public readonly EFI_HANDLE StandardErrorHandle;
        public readonly EFI_SIMPLE_TEXT_OUTPUT_PROTOCOL* StdErr;
        public readonly EFI_RUNTIME_SERVICES* RuntimeServices;
        public readonly EFI_BOOT_SERVICES* BootServices;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EFI_TIME
    {
        public ushort Year;
        public byte Month;
        public byte Day;
        public byte Hour;
        public byte Minute;
        public byte Second;
        public byte Pad1;
        public uint Nanosecond;
        public short TimeZone;
        public byte Daylight;
        public byte PAD2;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly struct EFI_RUNTIME_SERVICES
    {
        public readonly EFI_TABLE_HEADER Hdr;
        public readonly delegate*<EFI_TIME*, void*, ulong> GetTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe readonly struct EFI_BOOT_SERVICES
    {
        readonly EFI_TABLE_HEADER Hdr;
        private readonly void* pad0;
        private readonly void* pad1;
        private readonly void* pad2;
        private readonly void* pad3;
        private readonly void* pad4;
        public readonly delegate*<int, nint, void**, ulong> AllocatePool;
        private readonly void* pad6;
        private readonly void* pad7;
        private readonly void* pad8;
        private readonly void* pad9;
        private readonly void* pad10;
        private readonly void* pad11;
        private readonly void* pad12;
        private readonly void* pad13;
        private readonly void* pad14;
        private readonly void* pad15;
        private readonly void* pad16;
        private readonly void* pad17;
        private readonly void* pad18;
        private readonly void* pad19;
        private readonly void* pad20;
        private readonly void* pad21;
        private readonly void* pad22;
        private readonly void* pad23;
        private readonly void* pad24;
        private readonly void* pad25;
        private readonly void* pad26;
        private readonly void* pad27;
        public readonly delegate*<uint, ulong> Stall;
    }
}

#endif
