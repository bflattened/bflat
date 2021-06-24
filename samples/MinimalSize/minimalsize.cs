using System;
using System.Diagnostics;

Console.WriteLine($"NullReferenceException message is: {new NullReferenceException().Message}");
Console.WriteLine($"The runtime type of int is named: {typeof(int)}");
Console.WriteLine($"Type of boxed integer is{(123.GetType() == typeof(int) ? "" : " not")} equal to typeof(int)");
Console.WriteLine($"Type of boxed integer is{(123.GetType() == typeof(byte) ? "" : " not")} equal to typeof(byte)");
Console.WriteLine($"Upper case of 'Вторник' is '{"Вторник".ToUpper()}'");
Console.WriteLine($"Current stack frame is {new StackTrace().GetFrame(0)}");
