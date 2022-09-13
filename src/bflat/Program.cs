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
using System.CommandLine.Help;
using System.Reflection;

class Program
{
    private readonly static Option<bool> InfoOption = new Option<bool>("--info", "Show .NET information");

    private static int Main(string[] args)
    {
        using PerfWatch total = new PerfWatch("Total");

        var root = new RootCommand(
            "Bflat C# compiler\n" +
            "Copyright (c) 2021-2022 Michal Strehovsky\n" +
            "https://flattened.net\n")
        {
            BuildCommand.Create(),
            ILBuildCommand.Create(),
            InfoOption,
        };
        root.SetHandler(ctx =>
        {
            if (ctx.ParseResult.GetValueForOption(InfoOption))
            {
                foreach (var attr in Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    string friendlyName = attr.Key switch
                    {
                        "BflatRuntimeVersion" => ".NET Runtime",
                        "MicrosoftCodeAnalysisCSharpVersion" => "C# Compiler",
                        _ => null,
                    };

                    if (friendlyName != null)
                    {
                        Console.WriteLine($"{friendlyName} Version:");
                        Console.WriteLine($"  {attr.Value}");
                    }
                }
            }
            else
            {
                ctx.HelpBuilder.Write(root, Console.Out);
            }
        });

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
