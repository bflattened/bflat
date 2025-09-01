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

using System.Runtime.CompilerServices;

namespace System
{
    internal static class SpanHelpers
    {
        [Intrinsic]
        public static unsafe void ClearWithoutReferences(ref byte dest, nuint len)
        {
            Fill(ref dest, 0, len);
        }

        [Intrinsic]
        internal static unsafe void Memmove(ref byte dest, ref byte src, nuint len)
        {
            if ((nuint)(nint)Unsafe.ByteOffset(ref src, ref dest) >= len)
                for (nuint i = 0; i < len; i++)
                    Unsafe.Add(ref dest, (nint)i) = Unsafe.Add(ref src, (nint)i);
            else
                for (nuint i = len; i > 0; i--)
                    Unsafe.Add(ref dest, (nint)(i - 1)) = Unsafe.Add(ref src, (nint)(i - 1));
        }


        internal static void Fill(ref byte dest, byte value, nuint len)
        {
            for (nuint i = 0; i < len;  i++)
                Unsafe.Add(ref dest, (nint)i) = value;
        }
    }
}
