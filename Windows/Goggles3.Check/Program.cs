using System.Text.Json;
using Goggles3.Core;

if (args.Length != 3)
{
    Console.Error.WriteLine("Goggles3.Check <ffmpeg> <captura.h264> <salida.mp4>"); return 2;
}
var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var recorded = new TaskCompletionSource<RecordingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
int frames = 0;
await using var session = new FfmpegVideoSession(args[0]);
session.Log += text => Console.Error.WriteLine(text);
session.Frame += pixels =>
{
    if (pixels.Length != FfmpegVideoSession.FrameSize) throw new InvalidDataException("Tamaño de fotograma inesperado.");
    Interlocked.Increment(ref frames);
};
session.Ended += _ => done.TrySetResult();
session.RecordingEnded += result => recorded.TrySetResult(result);
session.StartRecording(args[2]); session.StartReplay(args[1]);
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
await done.Task.WaitAsync(deadline.Token);
RecordingResult recording = await recorded.Task.WaitAsync(deadline.Token);
Console.WriteLine(JsonSerializer.Serialize(new { frames, recording },new JsonSerializerOptions { WriteIndented = true }));
return frames > 0 && recording.Frames > 0 && recording.Error == null ? 0 : 1;
