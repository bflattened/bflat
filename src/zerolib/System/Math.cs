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

        internal static int DivInt32(int dividend, int divisor)
        {
            if ((uint)(divisor + 1) <= 1)
            {
                if (divisor == 0)
                {
                    Environment.FailFast(null);
                    return 0;
                }
                else if (divisor == -1)
                {
                    if (dividend == int.MinValue)
                    {
                        Environment.FailFast(null);
                        return 0;
                    }
                    return -dividend;
                }
            }

            return DivInt32Internal(dividend, divisor);
        }

        internal static uint DivUInt32(uint dividend, uint divisor)
        {
            if (divisor == 0)
            {
                Environment.FailFast(null);
                return 0;
            }

            return DivUInt32Internal(dividend, divisor);
        }

        internal static long DivInt64(long dividend, long divisor)
        {
            if ((int)((ulong)divisor >> 32) == (int)(((ulong)(int)divisor) >> 32))
            {
                if ((int)divisor == 0)
                {
                    Environment.FailFast(null);
                    return 0;
                }

                if ((int)divisor == -1)
                {
                    if (dividend == long.MinValue)
                    {
                        Environment.FailFast(null);
                        return 0;
                    }
                    return -dividend;
                }

                if ((int)((ulong)dividend >> 32) == (int)(((ulong)(int)dividend) >> 32))
                {
                    return DivInt32Internal((int)dividend, (int)divisor);
                }
            }

            return DivInt64Internal(dividend, divisor);
        }

        internal static ulong DivUInt64(ulong dividend, ulong divisor)
        {
            if ((int)(divisor >> 32) == 0)
            {
                if ((uint)divisor == 0)
                {
                    Environment.FailFast(null);
                    return 0;
                }

                if ((int)(dividend >> 32) == 0)
                {
                    return DivUInt32Internal((uint)dividend, (uint)divisor);
                }
            }

            return DivUInt64Internal(dividend, divisor);
        }

        internal static int ModInt32(int dividend, int divisor)
        {
            if ((uint)(divisor + 1) <= 1)
            {
                if (divisor == 0)
                {
                    Environment.FailFast(null);
                    return 0;
                }
                else if (divisor == -1)
                {
                    if (dividend == int.MinValue)
                    {
                        Environment.FailFast(null);
                        return 0;
                    }
                    return 0;
                }
            }

            return ModInt32Internal(dividend, divisor);
        }

        internal static uint ModUInt32(uint dividend, uint divisor)
        {
            if (divisor == 0)
            {
                Environment.FailFast(null);
                return 0;
            }

            return ModUInt32Internal(dividend, divisor);
        }

        internal static long ModInt64(long dividend, long divisor)
        {
            if ((int)((ulong)divisor >> 32) == (int)(((ulong)(int)divisor) >> 32))
            {
                if ((int)divisor == 0)
                {
                    Environment.FailFast(null);
                    return 0;
                }

                if ((int)divisor == -1)
                {
                    if (dividend == long.MinValue)
                    {
                        Environment.FailFast(null);
                        return 0;
                    }
                    return 0;
                }

                if ((int)((ulong)dividend >> 32) == (int)(((ulong)(int)dividend) >> 32))
                {
                    return ModInt32Internal((int)dividend, (int)divisor);
                }
            }

            return ModInt64Internal(dividend, divisor);
        }

        internal static ulong ModUInt64(ulong dividend, ulong divisor)
        {
            if ((int)(divisor >> 32) == 0)
            {
                if ((uint)divisor == 0)
                {
                    Environment.FailFast(null);
                    return 0;
                }

                if ((int)(dividend >> 32) == 0)
                {
                    return ModUInt32Internal((uint)dividend, (uint)divisor);
                }
            }

            return ModUInt64Internal(dividend, divisor);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "DivInt32Internal")]
        private static extern int DivInt32Internal(int dividend, int divisor);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "DivUInt32Internal")]
        private static extern uint DivUInt32Internal(uint dividend, uint divisor);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "DivInt64Internal")]
        private static extern long DivInt64Internal(long dividend, long divisor);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "DivUInt64Internal")]
        private static extern ulong DivUInt64Internal(ulong dividend, ulong divisor);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "ModInt32Internal")]
        private static extern int ModInt32Internal(int dividend, int divisor);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "ModUInt32Internal")]
        private static extern uint ModUInt32Internal(uint dividend, uint divisor);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "ModInt64Internal")]
        private static extern long ModInt64Internal(long dividend, long divisor);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "ModUInt64Internal")]
        private static extern ulong ModUInt64Internal(ulong dividend, ulong divisor);
    }
}
