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
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace System.Runtime
{
    internal sealed class RuntimeExportAttribute : Attribute
    {
        public RuntimeExportAttribute(string entry) { }
    }

    internal sealed class RuntimeImportAttribute : Attribute
    {
        public RuntimeImportAttribute(string lib) { }
        public RuntimeImportAttribute(string lib, string entry) { }
    }

    internal unsafe struct MethodTable
    {
        internal ushort _usComponentSize;
        private ushort _usFlags;
        internal uint _uBaseSize;
        internal MethodTable* _relatedType;
        private ushort _usNumVtableSlots;
        private ushort _usNumInterfaces;
        private uint _uHashCode;
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    partial class ThrowHelpers
    {
        static void ThrowIndexOutOfRangeException() => Environment.FailFast(null);
        static void ThrowDivideByZeroException() => Environment.FailFast(null);
        static void ThrowPlatformNotSupportedException() => Environment.FailFast(null);
    }

    // A class that the compiler looks for that has helpers to initialize the
    // process. The compiler can gracefully handle the helpers not being present,
    // but the class itself being absent is unhandled. Let's add an empty class.
    unsafe partial class StartupCodeHelpers
    {
        // A couple symbols the generated code will need we park them in this class
        // for no particular reason. These aid in transitioning to/from managed code.
        // Since we don't have a GC, the transition is a no-op.
        [RuntimeExport("RhpReversePInvoke")]
        static void RhpReversePInvoke(IntPtr frame) { }
        [RuntimeExport("RhpReversePInvokeReturn")]
        static void RhpReversePInvokeReturn(IntPtr frame) { }
        [RuntimeExport("RhpPInvoke")]
        static void RhpPInvoke(IntPtr frame) { }
        [RuntimeExport("RhpPInvokeReturn")]
        static void RhpPInvokeReturn(IntPtr frame) { }
        [RuntimeExport("RhpGcPoll")]
        static void RhpGcPoll() { }

        [RuntimeExport("RhpFallbackFailFast")]
        static void RhpFallbackFailFast() { Environment.FailFast(null); }

        [RuntimeExport("RhpNewFast")]
        static unsafe void* RhpNewFast(MethodTable* pMT)
        {
            MethodTable** result = AllocObject(pMT->_uBaseSize);
            *result = pMT;
            return result;
        }

        [RuntimeExport("RhpNewArray")]
        static unsafe void* RhpNewArray(MethodTable* pMT, int numElements)
        {
            if (numElements < 0)
                Environment.FailFast(null);

            MethodTable** result = AllocObject((uint)(pMT->_uBaseSize + numElements * pMT->_usComponentSize));
            *result = pMT;
            *(int*)(result + 1) = numElements;
            return result;
        }

        internal struct ArrayElement
        {
            public object Value;
        }

        [RuntimeExport("RhpStelemRef")]
        public static unsafe void StelemRef(Array array, nint index, object obj)
        {
            ref object element = ref Unsafe.As<ArrayElement[]>(array)[index].Value;

            MethodTable* elementType = array.m_pMethodTable->_relatedType;

            if (obj == null)
                goto assigningNull;

            if (elementType != obj.m_pMethodTable)
                Environment.FailFast(null); /* covariance */

doWrite:
            element = obj;
            return;

assigningNull:
            element = null;
            return;
        }

        [RuntimeExport("RhpCheckedAssignRef")]
        public static unsafe void RhpCheckedAssignRef(void** dst, void* r)
        {
            *dst = r;
        }

        [RuntimeExport("RhpAssignRef")]
        public static unsafe void RhpAssignRef(void** dst, void* r)
        {
            *dst = r;
        }

        static unsafe MethodTable** AllocObject(uint size)
        {
#if WINDOWS
            [DllImport("kernel32"), SuppressGCTransition]
            static extern MethodTable** LocalAlloc(uint flags, uint size);
            MethodTable** result = LocalAlloc(0x40, size);
#elif LINUX
            [DllImport("libSystem.Native"), SuppressGCTransition]
            static extern MethodTable** SystemNative_Malloc(nuint size);
            MethodTable** result = SystemNative_Malloc(size);
#elif UEFI
            MethodTable** result;
            if (EfiSystemTable->BootServices->AllocatePool(2 /* LoaderData*/, (nint)size, (void**)&result) != 0)
                result = null;
#else
#error Nope
#endif

            if (result == null)
                Environment.FailFast(null);

            return result;
        }
    }
}
