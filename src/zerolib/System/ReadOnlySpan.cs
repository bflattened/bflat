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
using System.Runtime.InteropServices;

namespace System
{
    public readonly ref struct ReadOnlySpan<T>
    {
        private readonly ref T _reference;
        private readonly int _length;

        public ReadOnlySpan(T[] array)
        {
            if (array == null)
            {
                this = default;
                return;
            }

            _reference = ref MemoryMarshal.GetArrayDataReference(array);
            _length = array.Length;
        }

        public unsafe ReadOnlySpan(void* pointer, int length)
        {
            _reference = ref Unsafe.As<byte, T>(ref *(byte*)pointer);
            _length = length;
        }

        public ref readonly T this[int index]
        {
            [Intrinsic]
            get
            {
                if ((uint)index >= (uint)_length)
                    Environment.FailFast("Index out of range");
                return ref Unsafe.Add(ref _reference, (nint)(uint)index);
            }
        }

        public static implicit operator ReadOnlySpan<T>(T[] array) => new ReadOnlySpan<T>(array);
    }
}
