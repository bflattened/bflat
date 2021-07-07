using System;
using System.Runtime.InteropServices;

using Sokol;

App.Run(new() {
    InitCb = &Init,
    FrameCb = &Frame,
    CleanupCb = &Cleanup,
    Width = 640,
    Height = 480,
    GlForceGles2 = true,
    WindowTitle = "Triangle (sokol-app)",
    Icon = { SokolDefault = true },
});

[UnmanagedCallersOnly]
static void Init()
{
    Gfx.Setup(new()
    {
        Context = App.Context(),
    });

    /* a vertex buffer with 3 vertices */
    ReadOnlySpan<float> vertices = stackalloc float[] {
        // positions            // colors
         0.0f,  0.5f, 0.5f,     1.0f, 0.0f, 0.0f, 1.0f,
         0.5f, -0.5f, 0.5f,     0.0f, 1.0f, 0.0f, 1.0f,
        -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 1.0f, 1.0f
    };

    State.Bindings.VertexBuffers[0] = Gfx.MakeBuffer(
        vertices,
        "triangle-vertices");

    Gfx.Shader shd = Gfx.MakeShader(GetShaderDesc());
    Gfx.PipelineDesc pipelineDesc = new()
    {
        Shader = shd,
        Label = "triangle-pipeline",
    };
    pipelineDesc.Layout.Attrs[0].Format = Gfx.VertexFormat.Float3;
    pipelineDesc.Layout.Attrs[1].Format = Gfx.VertexFormat.Float4;
    State.Pipeline = Gfx.MakePipeline(pipelineDesc);
}

[UnmanagedCallersOnly]
static void Frame()
{
    Gfx.BeginDefaultPass(default, App.Width(), App.Height());
    Gfx.ApplyPipeline(State.Pipeline);
    Gfx.ApplyBindings(State.Bindings);
    Gfx.Draw(0, 3, 1);
    Gfx.EndPass();
    Gfx.Commit();
}

[UnmanagedCallersOnly]
static void Cleanup()
{
    Gfx.Shutdown();
}

// build a backend-specific ShaderDesc struct
// NOTE: the other samples are using shader-cross-compilation via the
// sokol-shdc tool, but this sample uses a manual shader setup to
// demonstrate how it works without a shader-cross-compilation tool
//
static Gfx.ShaderDesc GetShaderDesc()
{
    Gfx.ShaderDesc desc = default;
    switch (Gfx.QueryBackend())
    {
        case Gfx.Backend.D3d11:
            desc.Attrs[0].SemName = "POS";
            desc.Attrs[1].SemName = "COLOR";
            desc.Vs.Source = @"
                struct vs_in {
                  float4 pos: POS;
                  float4 color: COLOR;
                };
                struct vs_out {
                  float4 color: COLOR0;
                  float4 pos: SV_Position;
                };
                vs_out main(vs_in inp) {
                  vs_out outp;
                  outp.pos = inp.pos;
                  outp.color = inp.color;
                  return outp;
                }";
            desc.Fs.Source = @"
                float4 main(float4 color: COLOR0): SV_Target0 {
                  return color;
                }";
            break;
        case Gfx.Backend.Glcore33:
            desc.Attrs[0].Name = "position";
            desc.Attrs[1].Name = "color0";
            desc.Vs.Source = @"
                #version 330
                in vec4 position;
                in vec4 color0;
                out vec4 color;
                void main() {
                  gl_Position = position;
                  color = color0;
                }";

            desc.Fs.Source = @"
                #version 330
                in vec4 color;
                out vec4 frag_color;
                void main() {
                  frag_color = color;
                }";
            break;
        case Gfx.Backend.MetalMacos:
            desc.Vs.Source = @"
                #include <metal_stdlib>
                using namespace metal;
                struct vs_in {
                  float4 position [[attribute(0)]];
                  float4 color [[attribute(1)]];
                };
                struct vs_out {
                  float4 position [[position]];
                  float4 color;
                };
                vertex vs_out _main(vs_in inp [[stage_in]]) {
                  vs_out outp;
                  outp.position = inp.position;
                  outp.color = inp.color;
                  return outp;
                }";
            desc.Fs.Source = @"
                #include <metal_stdlib>
                using namespace metal;
                fragment float4 _main(float4 color [[stage_in]]) {
                   return color;
                };";
            break;
    }
    return desc;
}

static class State
{
    public static Gfx.Pipeline Pipeline;
    public static Gfx.Bindings Bindings;
}
