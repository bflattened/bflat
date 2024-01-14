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
    public readonly ref struct Span<T>
    {
        private readonly ref T _reference;
        private readonly int _length;

        public int Length => _length;

        public Span(T[] array)
        {
            if (array == null)
            {
                this = default;
                return;
            }

            _reference = ref MemoryMarshal.GetArrayDataReference(array);
            _length = array.Length;
        }

        public unsafe Span(void* pointer, int length)
        {
            _reference = ref Unsafe.As<byte, T>(ref *(byte*)pointer);
            _length = length;
        }

        public Span(T[] array, int start, int length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0)
                    Environment.FailFast(null);
                this = default;
                return; // returns default
            }
#if X64 || ARM64
            if ((ulong)(uint)start + (ulong)(uint)length > (ulong)(uint)array.Length)
                Environment.FailFast(null);
#elif X86 || ARM
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
                Environment.FailFast(null);
#else
#error Nope
#endif

            _reference = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), (nint)(uint)start);
            _length = length;
        }

        public ref T this[int index]
        {
            [Intrinsic]
            get
            {
                if ((uint)index >= (uint)_length)
                    Environment.FailFast(null);
                return ref Unsafe.Add(ref _reference, (nint)(uint)index);
            }
        }

        public unsafe void Clear()
        {
            for (int i = 0; i < _length; i++)
                Unsafe.Add(ref _reference, i) = default;
        }

        public unsafe void Fill(T value)
        {
            for (int i = 0; i < _length; i++)
                Unsafe.Add(ref _reference, i) = value;
        }
    }
}
