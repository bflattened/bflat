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

namespace System.Runtime.InteropServices
{
    public enum UnmanagedType { }

    public enum CharSet
    {
        None = 1,
        Ansi = 2,
        Unicode = 3,
        Auto = 4,
    }

    public enum CallingConvention
    {
        Winapi = 1,
        Cdecl = 2,
        StdCall = 3,
        ThisCall = 4,
        FastCall = 5,
    }

    public sealed class DllImportAttribute : Attribute
    {
        public string EntryPoint;
        public CharSet CharSet;
        public bool ExactSpelling;
        public CallingConvention CallingConvention;
        public DllImportAttribute(string dllName) { }
    }

    public sealed class UnmanagedCallersOnlyAttribute : Attribute
    {
        public string EntryPoint;
        public UnmanagedCallersOnlyAttribute() { }
    }

    public enum LayoutKind
    {
        Sequential = 0,
        Explicit = 2,
        Auto = 3,
    }

    public sealed class StructLayoutAttribute : Attribute
    {
        public int Size;
        public int Pack;
        public StructLayoutAttribute(LayoutKind layoutKind) { }
    }

    public sealed class InAttribute : Attribute
    {
    }

    public sealed class OutAttribute : Attribute
    {
    }

    public sealed class SuppressGCTransitionAttribute : Attribute
    {
        public SuppressGCTransitionAttribute() { }
    }
}
