using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PidSimulator.ViewModels;

namespace PidSimulator.App;

/// <summary>主窗口。趋势图用内置 Polyline 绘制（零第三方依赖）。</summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) => RedrawTrend();
        _timer.Start();

        SizeChanged += (_, _) => RedrawTrend();
    }

    private void RedrawTrend()
    {
        if (DataContext is not MainViewModel vm)
            return;

        var data = vm.Trend;
        double w = TrendCanvas.ActualWidth;
        double h = TrendCanvas.ActualHeight;

        if (w <= 1 || h <= 1 || data.Count < 2)
        {
            PvLine.Points = new PointCollection();
            SpLine.Points = new PointCollection();
            OpLine.Points = new PointCollection();
            return;
        }

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (var p in data)
        {
            min = Math.Min(min, Math.Min(p.SP, Math.Min(p.PV, p.OP)));
            max = Math.Max(max, Math.Max(p.SP, Math.Max(p.PV, p.OP)));
        }
        if (max - min < 1e-3f)
            max = min + 1f;

        float pad = (max - min) * 0.1f;
        min -= pad;
        max += pad;
        float range = max - min;

        var pv = new PointCollection(data.Count);
        var sp = new PointCollection(data.Count);
        var op = new PointCollection(data.Count);
        double dx = w / (data.Count - 1);

        for (int i = 0; i < data.Count; i++)
        {
            double x = i * dx;
            var p = data[i];
            pv.Add(new Point(x, h - (p.PV - min) / range * h));
            sp.Add(new Point(x, h - (p.SP - min) / range * h));
            op.Add(new Point(x, h - (p.OP - min) / range * h));
        }

        PvLine.Points = pv;
        SpLine.Points = sp;
        OpLine.Points = op;
    }
}
