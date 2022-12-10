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

#if WINDOWS

using System.Runtime.InteropServices;

namespace System
{
    public static partial class Environment
    {
        [DllImport("kernel32")]
        private static extern long GetTickCount64();

        public static long TickCount64 => GetTickCount64();

        [DllImport("kernel32")]
        private static extern void RaiseFailFastException(IntPtr a, IntPtr b, int flags);

        public static void FailFast(string message)
        {
            RaiseFailFastException(default, default, default);
        }
    }
}

#endif
