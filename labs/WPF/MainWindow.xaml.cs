using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ThreeCylindersVisualization
{
    public partial class MainWindow : Window
    {
        //private readonly int[] _times1 = {5, 2, 4, 5 };
        //private readonly int[] _times2 = {3, 3, 3, 6 };
        //private readonly int[] _times3 = {4, 2, 2, 5 };

        private int[] _times1 = Array.Empty<int>();
        private int[] _times2 = Array.Empty<int>();
        private int[] _times3 = Array.Empty<int>();

        private readonly Queue<int> _queue1 = new Queue<int>();
        private readonly Queue<int> _queue2 = new Queue<int>();
        private readonly Queue<int> _queue3 = new Queue<int>();

        private volatile bool _isWorking1, _isWorking2, _isWorking3;
        private volatile bool _isRunning;
        private int _completedCount;
        private readonly object _lock = new object();

        private DispatcherTimer _visualTimer;
        private const int TickMs = 500;
        private const int MaxBlocks = 20;
        private const int TaskCount = 6;
        private const int MinTime = 1;
        private const int MaxTime = 6;

        private static readonly Random Random = new Random();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            ClearCylinders();

            GenerateRandomTimes();

            InitializeQueues();
            _completedCount = 0;
            _isRunning = true;

            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            StartBtn.Content = "Running...";
            StatusLabel.Text = "Наша симуляція";

            StartThreads();
            StartVisualization();

            Dispatcher.Invoke(() => OnVisualTimerTick());
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e) => StopSimulation();
        private void ExitBtn_Click(object sender, RoutedEventArgs e) => Close();


        private void GenerateRandomTimes()
        {
            _times1 = GenerateRandomArray(TaskCount, MinTime, MaxTime);
            _times2 = GenerateRandomArray(TaskCount, MinTime, MaxTime);
            _times3 = GenerateRandomArray(TaskCount, MinTime, MaxTime);
        }

        private int[] GenerateRandomArray(int length, int min, int max)
        {
            var arr = new int[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = Random.Next(min, max + 1);
            }
            return arr;
        }


        private void InitializeQueues()
        {
            _queue1.Clear(); _queue2.Clear(); _queue3.Clear();
            _queue1.Enqueue(_times1[0]);
            _queue2.Enqueue(_times2[0]);
            _queue3.Enqueue(_times3[0]);
        }

        private void StartThreads()
        {
            int n = _times1.Length;


            new Thread(() =>
            {
                for (int i = 0; i < n; i++)
                {
                    while (_queue1.Count == 0 && _isRunning)
                        Thread.Sleep(10);
                    if (!_isRunning) return;

                    int time = DequeueSafely(_queue1);
                    _isWorking1 = true;
                    Thread.Sleep(TickMs * time);
                    _isWorking1 = false;

                    if (i + 1 < n)
                    {
                        lock (_lock)
                            _queue2.Enqueue(_times2[i + 1]);
                    }
                }
                OnThreadCompleted();
            })
            { IsBackground = true }.Start();

            new Thread(() =>
            {
                for (int i = 0; i < n; i++)
                {
                    while (_queue2.Count == 0 && _isRunning)
                        Thread.Sleep(10);
                    if (!_isRunning) return;

                    int time = DequeueSafely(_queue2);
                    _isWorking2 = true;
                    Thread.Sleep(TickMs * time);
                    _isWorking2 = false;

                    if (i + 1 < n)
                    {
                        lock (_lock)
                            _queue3.Enqueue(_times3[i + 1]);
                    }
                }
                OnThreadCompleted();
            })
            { IsBackground = true }.Start();

            new Thread(() =>
            {
                for (int i = 0; i < n; i++)
                {
                    while (_queue3.Count == 0 && _isRunning)
                        Thread.Sleep(10);
                    if (!_isRunning) return;

                    int time = DequeueSafely(_queue3);
                    _isWorking3 = true;
                    Thread.Sleep(TickMs * time);
                    _isWorking3 = false;

                    if (i + 1 < n)
                    {
                        lock (_lock)
                            _queue1.Enqueue(_times1[i + 1]);
                    }
                }
                OnThreadCompleted();
            })
            { IsBackground = true }.Start();
        }

        private int DequeueSafely(Queue<int> queue)
        {
            lock (_lock)
            {
                return queue.Dequeue();
            }
        }

        private void OnThreadCompleted()
        {
            lock (_lock)
            {
                _completedCount++;
                if (_completedCount == 3)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _visualTimer?.Stop();
                        StartBtn.IsEnabled = true;
                        StopBtn.IsEnabled = false;
                        StartBtn.Content = "Run";
                        StatusLabel.Text = "Симуляція завершена!";
                        _isRunning = false;
                    });
                }
            }
        }

        private void StartVisualization()
        {
            _visualTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
            _visualTimer.Tick += OnVisualTimerTick;
            _visualTimer.Start();
        }

        private void OnVisualTimerTick(object sender = null, EventArgs e = null)
        {
            AddBlock(Cylinder1, _isWorking1 ? Brushes.Blue : Brushes.Red);
            AddBlock(Cylinder2, _isWorking2 ? Brushes.Blue : Brushes.Red);
            AddBlock(Cylinder3, _isWorking3 ? Brushes.Blue : Brushes.Red);
        }

        private void AddBlock(StackPanel panel, Brush color)
        {
            var rect = new Rectangle
            {
                Height = 14,
                Fill = color,
                Margin = new Thickness(1),
                RadiusX = 2,
                RadiusY = 2
            };
            panel.Children.Add(rect);
            if (panel.Children.Count > MaxBlocks)
                panel.Children.RemoveAt(0);
        }

        private void ClearCylinders()
        {
            Cylinder1.Children.Clear();
            Cylinder2.Children.Clear();
            Cylinder3.Children.Clear();
        }

        private void StopSimulation()
        {
            _isRunning = false;
            _visualTimer?.Stop();
            Dispatcher.Invoke(() =>
            {
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                StartBtn.Content = "Run";
                StatusLabel.Text = "Симуляція зупинена.";
            });
        }
    }
}