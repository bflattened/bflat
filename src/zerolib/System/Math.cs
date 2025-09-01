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
    public static class Math
    {
        internal static int ConvertToInt32Checked(double value)
        {
            Environment.FailFast(null);
            return 0;
        }

        internal static uint ConvertToUInt32Checked(double value)
        {
            Environment.FailFast(null);
            return 0;
        }

        internal static long ConvertToInt64Checked(double value)
        {
            Environment.FailFast(null);
            return 0;
        }

        internal static ulong ConvertToUInt64Checked(double value)
        {
            Environment.FailFast(null);
            return 0;
        }
    }
}
