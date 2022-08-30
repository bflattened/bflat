// bflat C# compiler
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

using System;
using System.Diagnostics;

internal struct PerfWatch : IDisposable
{
    private Stopwatch _sw;
    private string _name;

    private static bool IsEnabled { get; } = Environment.GetEnvironmentVariable("BFLAT_TIMINGS") == "1";

    public PerfWatch(string name)
    {
        if (IsEnabled)
        {
            _name = name;
            _sw = Stopwatch.StartNew();
        }
        else
        {
            _name = null;
            _sw = null;
        }
    }

    public void Complete()
    {
        if (_sw != null)
        {
            Console.WriteLine($"{_name}: {_sw.Elapsed}");
        }
    }

    public void Dispose() => Complete();
}
