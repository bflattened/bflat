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
using System.CommandLine;

class Program
{
    private static int Main(string[] args)
    {

        var root = new RootCommand();
        root.Add(BuildCommand.Create());
        root.Add(ILBuildCommand.Create());

#if DEBUG
        return root.Invoke(args);
#else
        try
        {
            return root.Invoke(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error: " + e.Message);
            Console.Error.WriteLine(e.ToString());
            return 1;
        }
#endif
    }
}
