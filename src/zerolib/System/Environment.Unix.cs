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

#if !WINDOWS

using System.Runtime.InteropServices;

namespace System
{
    static partial class Environment
    {
        [DllImport("libSystem.Native")]
        public static extern long SystemNative_GetTimestamp();

        public static long TickCount64 => SystemNative_GetTimestamp() / 1_000_000;
    }
}

#endif
