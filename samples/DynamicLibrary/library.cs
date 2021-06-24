using System;
using System.Runtime.InteropServices;

internal static class Library
{
    [UnmanagedCallersOnly(EntryPoint = "Add")]
    private static int Add(int num1, int num2)
    {
        return num1 + num2;
    }

    // Note that parameters to UnmanagedCallersOnly methods may only be primitive types (except bool),
    // pointers and structures consisting of the above. Reference types are not permitted because they don't
    // have an ABI representation on the native side.
    [UnmanagedCallersOnly(EntryPoint = "CountCharacters")]
    private static unsafe nint CountCharacters(byte* pChars)
    {
        byte* pCurrent = pChars;
        while (*pCurrent++ != 0) ;
        return (nint)(pCurrent - pChars) - 1;
    }

    // Note that it is not allowed to leak exceptions out of UnmanagedCallersOnly methods.
    // There's no ABI-defined way to propagate the exception across native code.
    [UnmanagedCallersOnly(EntryPoint = "TryDivide")]
    private static unsafe int TryDivide(int num1, int num2, int* result)
    {
        try
        {
            *result = num1 / num2;
        }
        catch (DivideByZeroException ex)
        {
            return 0;
        }
        return 1;
    }
}
