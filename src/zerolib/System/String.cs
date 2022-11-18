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

using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    public sealed class String
    {
        // The layout of the string type is a contract with the compiler.
        private readonly int _length;
        private char _firstChar;

        public int Length => _length;

        [IndexerName("Chars")]
        public unsafe char this[int index]
        {
            [System.Runtime.CompilerServices.Intrinsic]
            get
            {
                return System.Runtime.CompilerServices.Unsafe.Add(ref _firstChar, index);
            }
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe String(char* value);

        private static unsafe string Ctor(char* ptr)
        {
            char* cur = ptr;
            while (*cur++ != 0) ;

            string result = FastNewString((int)(cur - ptr - 1));
            for (int i = 0; i < cur - ptr - 1; i++)
                Unsafe.Add(ref result._firstChar, i) = ptr[i];
            return result;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe String(sbyte* value);

        private static unsafe string Ctor(sbyte* ptr)
        {
            sbyte* cur = ptr;
            while (*cur++ != 0) ;

            string result = FastNewString((int)(cur - ptr - 1));
            for (int i = 0; i < cur - ptr - 1; i++)
            {
                if (ptr[i] > 0x7F)
                    Environment.FailFast(null);
                Unsafe.Add(ref result._firstChar, i) = (char)ptr[i];
            }
            return result;
        }

        static unsafe string FastNewString(int numChars)
        {
            return NewString("".m_pMethodTable, numChars);

            [MethodImpl(MethodImplOptions.InternalCall)]
            [RuntimeImport("*", "RhpNewArray")]
            static extern string NewString(MethodTable* pMT, int numElements);
        }
    }
}
