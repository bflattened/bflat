using System.Runtime.InteropServices;
using static System.Console;

WriteLine($"Add(1, 2) = {Add(1, 2)}");
WriteLine($"CountCharacters(\"Hello\") = {CountCharacters("Hello")}");
WriteLine($"TryDivide(1, 0, out _) = {TryDivide(1, 0, out _)}");

[DllImport("library")]
static extern int Add(int num1, int num2);

[DllImport("library", CharSet = CharSet.Ansi)]
static extern nint CountCharacters(string s);

[DllImport("library")]
static extern bool TryDivide(int num1, int num2, out int result);
