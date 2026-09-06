using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

namespace Pulsar.Interface;

internal partial class SplashWindow : Window
{
    // Unit length is one progress bar; Unit time is one second.
    // "Boost" values are used when the bar is lagging behind.
    private const double MaxSpeed = 1.25;
    private const double Acceleration = 5;
    private const double BoostMaxSpeed = 5;
    private const double BoostAcceleration = 20;

    private readonly List<(Bitmap Image, int End)> throbberFrames = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch stopwatch = new();
    private double lastTick;
    private double goal;
    private double velocity;
    private bool boost;

    public SplashWindow()
    {
        InitializeComponent();
        LoadThrobber();
        stopwatch.Start();
        timer.Tick += OnTick;
        timer.Start();
    }

    public void SetText(string text)
    {
        ProgressText.Text = text;
        SetProgress(null);
    }

    public void SetProgress(float? progress)
    {
        ProgressBar.IsVisible = progress.HasValue;

        if (progress.HasValue)
        {
            goal = Clamp(progress.Value, ProgressBar.Minimum, ProgressBar.Maximum);
            boost = goal >= ProgressBar.Maximum;
            return;
        }

        goal = ProgressBar.Minimum;
        ProgressBar.Value = ProgressBar.Minimum;
        velocity = 0;
        boost = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        timer.Stop();
        foreach (var (Image, _) in throbberFrames)
            Image.Dispose();
    }

    private void OnTick(object sender, EventArgs e)
    {
        double now = stopwatch.Elapsed.TotalMilliseconds;
        double elapsed = (now - lastTick) / 1000;
        double throbberElapsed = now % throbberFrames.Last().End;
        lastTick = now;

        int frameIndex = 0;
        while (throbberElapsed >= throbberFrames[frameIndex].End)
            frameIndex++;

        Bitmap frame = throbberFrames[frameIndex].Image;
        if (!ReferenceEquals(Throbber.Source, frame))
            Throbber.Source = frame;

        double remaining = goal - ProgressBar.Value;
        if (remaining == 0)
        {
            velocity = 0;
            boost = false;
            return;
        }

        double acceleration = boost ? BoostAcceleration : Acceleration;
        double targetVelocity =
            Math.Sign(remaining)
            * Math.Min(
                boost ? BoostMaxSpeed : MaxSpeed,
                Math.Sqrt(2 * acceleration * Math.Abs(remaining))
            );
        double velocityChange = acceleration * elapsed;
        velocity += Clamp(targetVelocity - velocity, -velocityChange, velocityChange);

        ProgressBar.Value += Clamp(
            velocity * elapsed,
            Math.Min(0, remaining),
            Math.Max(0, remaining)
        );
    }

    private void LoadThrobber()
    {
        using Stream stream = AssetLoader.Open(new Uri("avares://Interface/Assets/throbber.gif"));
        using SKCodec codec = SKCodec.Create(stream);
        SKImageInfo imageInfo = new(
            codec.Info.Width,
            codec.Info.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul
        );
        SKCodecFrameInfo[] frameInfo = codec.FrameInfo;

        int frameEnd = 0;
        for (int index = 0; index < frameInfo.Length; index++)
        {
            using SKBitmap pixels = new(imageInfo);
            pixels.Erase(SKColors.Transparent);

            SKCodecResult result = codec.GetPixels(
                imageInfo,
                pixels.GetPixels(),
                new SKCodecOptions(index, -1)
            );
            if (result != SKCodecResult.Success)
                throw new InvalidDataException($"Could not decode frame {index}: {result}");

            Bitmap frame = new(
                PixelFormats.Bgra8888,
                AlphaFormat.Premul,
                pixels.GetPixels(),
                new PixelSize(imageInfo.Width, imageInfo.Height),
                new Vector(96, 96),
                pixels.RowBytes
            );

            frameEnd += frameInfo[index].Duration;
            throbberFrames.Add((frame, frameEnd));
        }

        Throbber.Source = throbberFrames[0].Image;
    }

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Max(minimum, Math.Min(maximum, value));
}
