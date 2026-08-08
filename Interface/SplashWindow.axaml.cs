using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Pulsar.Interface;

internal partial class SplashWindow : Window
{
    // Unit length is one progress bar; Unit time is one second.
    // "Boost" values are used when the bar is lagging behind.
    private const double MaxSpeed = 1.25;
    private const double Acceleration = 5;
    private const double BoostMaxSpeed = 5;
    private const double BoostAcceleration = 20;

    private readonly DispatcherTimer tween = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch tweenClock = Stopwatch.StartNew();
    private double goal;
    private double velocity;
    private bool boost;

    public SplashWindow()
    {
        InitializeComponent();
        tween.Tick += TweenTick;
        tween.Start();
        Closed += (_, _) => tween.Stop();
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

    private void TweenTick(object sender, EventArgs e)
    {
        double elapsed = tweenClock.Elapsed.TotalSeconds;
        tweenClock.Restart();

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

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Max(minimum, Math.Min(maximum, value));
}
