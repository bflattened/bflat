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

namespace System
{
    public abstract class Delegate
    {
        internal object m_firstParameter;
        internal object m_helperObject;
        internal nint m_extraFunctionPointerOrData;
        internal IntPtr m_functionPointer;

        private void InitializeClosedStaticThunk(object firstParameter, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            m_extraFunctionPointerOrData = functionPointer;
            m_helperObject = firstParameter;
            m_functionPointer = functionPointerThunk;
            m_firstParameter = this;
        }

        private void InitializeOpenStaticThunk(object firstParameter, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            m_firstParameter = this;
            m_functionPointer = functionPointerThunk;
            m_extraFunctionPointerOrData = functionPointer;
        }

        private void InitializeClosedInstance(object firstParameter, IntPtr functionPointer)
        {
            m_functionPointer = functionPointer;
            m_firstParameter = firstParameter;
        }
    }

    public abstract class MulticastDelegate : Delegate { }
}
