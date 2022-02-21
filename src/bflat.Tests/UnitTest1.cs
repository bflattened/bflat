using System;
using Xunit;

namespace bflat.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        new BflatCompilation().Build("System.Console.WriteLine(\"Hello\");").Run("Hello" + Environment.NewLine);
    }
}