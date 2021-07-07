using System;
using System.Runtime.InteropServices;

using Sokol;

App.Run(new() {
    InitCb = &Init,
    FrameCb = &Frame,
    CleanupCb = &Cleanup,
    Width = 640,
    Height = 480,
    WindowTitle = "Saudio (sokol-app)",
    Icon = { SokolDefault = true },
});

[UnmanagedCallersOnly]
static void Init()
{
    Gfx.Setup(new()
    {
        Context = App.Context(),
    });

    Audio.Setup(default);

    State.PassAction.Colors[0] = new()
    {
        Action = Gfx.Action.Clear,
        Value = new() { R = 1, G = 0.5f, B = 0, A = 1 },
    };
}

[UnmanagedCallersOnly]
static void Frame()
{
    Gfx.BeginDefaultPass(State.PassAction, App.Width(), App.Height());
    var numFrames = Audio.Expect();
    float s;
    for (int i = 0; i < numFrames; i++)
    {
        if ((State.EvenOdd++ & (1 << 5)) != 0)
        {
            s = 0.05f;
        }
        else
        {
            s = -0.05f;
        }
        State.Samples[State.SamplePos++] = s;
        if (State.SamplePos == State.Samples.Length)
        {
            State.SamplePos = 0;
            Audio.Push(State.Samples[0], State.Samples.Length);
        }

    }
    Gfx.EndPass();
    Gfx.Commit();
}

[UnmanagedCallersOnly]
static void Cleanup()
{
    Audio.Shutdown();
    Gfx.Shutdown();
}

static class State
{
    public static Gfx.PassAction PassAction;
    public static int EvenOdd;
    public static readonly float[] Samples = new float[32];
    public static int SamplePos;
}
