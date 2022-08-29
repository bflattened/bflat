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

using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

internal abstract class CommandBase : ICommandHandler
{
    public Task<int> InvokeAsync(InvocationContext context) => Task.FromResult(Handle(context.ParseResult));
    public int Invoke(InvocationContext context) => Handle(context.ParseResult);
    public abstract int Handle(ParseResult result);
}
