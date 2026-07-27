namespace Saab.Foundation.Unity.MapStreamer.Streaming.Pipeline
{
    internal enum StreamingPipelineState
    {
        Unlocked,
        Editing,
        Rendering,
        PostProcessing,
        Aborted,
    }
}
