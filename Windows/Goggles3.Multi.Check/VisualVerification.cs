using System.Diagnostics;
using System.Text.Json;

internal static class VisualVerification
{
    // Run with a full FFmpeg build after copying the five Windows MP4s.
    // Checks pixel contents, not just successful encoder exit or MP4 metadata.
    internal static async Task<int> Run(string ffmpeg, string folder)
    {
        int[][] expected = [[0,0,255], [0,128,0], [255,0,0], [0,255,255]];
        var samples = new List<object>();
        bool ok = true;
        for (int file = 0; file < 5; file++)
        {
            string name = file == 4 ? "mosaic.mp4" : $"channel-{file + 1}.mp4";
            var info = new ProcessStartInfo(ffmpeg) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string arg in new[] { "-hide_banner", "-loglevel", "error", "-i", Path.Combine(folder, name), "-frames:v", "1", "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1" }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new IOException("FFmpeg did not start.");
            var errors = process.StandardError.ReadToEndAsync();
            using var pixels = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(pixels);
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || pixels.Length != 1920 * 1080 * 4) throw new IOException(name + ": " + await errors);
            byte[] frame = pixels.ToArray();
            for (int slot = 0; slot < (file == 4 ? 4 : 1); slot++)
            {
                int color = file == 4 ? slot : file;
                // Several rows catch alternating-line chroma corruption.
                foreach (int dy in new[] { 0, 1, 37, 38 })
                {
                    int x = file == 4 ? slot % 2 * 960 + 240 : 480;
                    int y = (file == 4 ? slot / 2 * 540 + 135 : 270) + dy;
                    int offset = (y * 1920 + x) * 4;
                    int[] actual = [frame[offset], frame[offset + 1], frame[offset + 2]];
                    bool pass = actual.Zip(expected[color], (a, b) => Math.Abs(a - b)).All(delta => delta <= 20);
                    ok &= pass; samples.Add(new { name, slot, x, y, actual, pass });
                }
            }
        }
        string report = JsonSerializer.Serialize(new { ok, samples }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(folder, "visual-verification.json"), report);
        Console.WriteLine(report);
        return ok ? 0 : 1;
    }
}
