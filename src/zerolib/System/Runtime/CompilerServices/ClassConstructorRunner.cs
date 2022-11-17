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

using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    // A class responsible for running static constructors. The compiler will call into this
    // code to ensure static constructors run and that they only run once.
    internal static class ClassConstructorRunner
    {
        private static IntPtr CheckStaticClassConstructionReturnNonGCStaticBase(ref StaticClassConstructionContext context, IntPtr nonGcStaticBase)
        {
            CheckStaticClassConstruction(ref context);
            return nonGcStaticBase;
        }

        private static unsafe void CheckStaticClassConstruction(ref StaticClassConstructionContext context)
        {
            // Not dealing with multithreading issues.
            if (context.Initialized != 1)
            {
                context.Initialized = 1;
                context.ClassConstructor();
            }
        }
    }

    // This data structure is a contract with the compiler. It holds the address of a static
    // constructor and a flag that specifies whether the constructor already executed.
    internal unsafe struct StaticClassConstructionContext
    {
        public delegate*<void> ClassConstructor;
        public int Initialized;
    }
}
