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

using System;
using System.Runtime.InteropServices;

namespace Internal.Runtime.CompilerHelpers
{
    internal unsafe static class InteropHelpers
    {
        private static IntPtr ResolvePInvoke(MethodFixupCell* pCell)
        {
            if (pCell->Target != default)
                return pCell->Target;

            return ResolvePInvokeSlow(pCell);
        }

        private static IntPtr ResolvePInvokeSlow(MethodFixupCell* pCell)
        {
            ModuleFixupCell* pModuleCell = pCell->Module;
            if (pModuleCell->Handle == default)
            {
#if WINDOWS
                pModuleCell->Handle = LoadLibraryA(pModuleCell->ModuleName);

                [DllImport("kernel32"), SuppressGCTransition]
                extern static IntPtr LoadLibraryA(IntPtr name);
#elif LINUX
                pModuleCell->Handle = SystemNative_LoadLibrary(pModuleCell->ModuleName);

                [DllImport("libSystem.Native"), SuppressGCTransition]
                extern static IntPtr SystemNative_LoadLibrary(IntPtr name);
#endif
                if (pModuleCell->Handle == default)
                    Environment.FailFast(null);
            }

#if WINDOWS
            pCell->Target = GetProcAddress(pModuleCell->Handle, pCell->MethodName);

            [DllImport("kernel32"), SuppressGCTransition]
            extern static IntPtr GetProcAddress(IntPtr hModule, IntPtr name);
#elif LINUX
            pCell->Target = SystemNative_GetProcAddress(pModuleCell->Handle, pCell->MethodName);

            [DllImport("libSystem.Native"), SuppressGCTransition]
            extern static IntPtr SystemNative_GetProcAddress(IntPtr hModule, IntPtr name);
#endif

            if (pCell->Target == default)
                Environment.FailFast(null);

            return pCell->Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ModuleFixupCell
        {
            public IntPtr Handle;
            public IntPtr ModuleName;
            public IntPtr CallingAssemblyType;
            public uint DllImportSearchPathAndCookie;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MethodFixupCell
        {
            public IntPtr Target;
            public IntPtr MethodName;
            public ModuleFixupCell* Module;
            private int Flags;
        }
    }
}
