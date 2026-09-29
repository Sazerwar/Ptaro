using System;
using System.Diagnostics;
using System.Threading;

namespace FloydWarshall
{
    class Matrix
    {
        public int[][] Data;

        public Matrix()
        {
            Data = new int[0][];
        }

        public Matrix(int n, double density = 0.3, int maxWeight = 100)
        {
            var rand = new Random();
            Data = new int[n][];
            for (int i = 0; i < n; i++)
                Data[i] = new int[n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        Data[i][j] = 0;
                    }
                    else
                    {
                        double p = rand.Next(1000) / 1000.0;
                        Data[i][j] = p < density ? rand.Next(maxWeight) + 1 : 0;
                    }
                }
            }
        }

        public Matrix Clone()
        {
            var m = new Matrix();
            m.Data = new int[Data.Length][];
            for (int i = 0; i < Data.Length; i++)
                m.Data[i] = (int[])Data[i].Clone();
            return m;
        }
    }

    class Graph
    {
        private int[] vershini;
        private Matrix rebra;

        public const int INF = 100_000_000;

        public Graph()
        {
            vershini = new int[] { 0 };
            rebra = new Matrix();
        }

        public Graph(int n, Matrix f)
        {
            vershini = new int[n];
            for (int i = 0; i < n; i++)
                vershini[i] = i + 1;

            rebra = new Matrix();
            rebra.Data = new int[f.Data.Length][];
            for (int i = 0; i < f.Data.Length; i++)
            {
                rebra.Data[i] = new int[f.Data.Length];
                for (int j = 0; j < f.Data.Length; j++)
                {
                    if (i == j) rebra.Data[i][j] = 0;
                    else if (f.Data[i][j] != 0) rebra.Data[i][j] = f.Data[i][j];
                    else rebra.Data[i][j] = INF;
                }
            }
        }

        public void FloydAlgorithm()
        {
            int n = rebra.Data.Length;
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        rebra.Data[i][j] = Math.Min(rebra.Data[i][j], rebra.Data[i][k] + rebra.Data[k][j]);
        }

        public int[] GetTop() => vershini;
        public Matrix GetEdge() => rebra;

        public void Print(int a, int b)
        {
            Console.WriteLine($"Shortest path from {a} to {b} = {rebra.Data[a - 1][b - 1]}");
        }
    }

    class Program
    {
        static void RunParallelFloyd(Graph g, int n, int numThreads)
        {
            int[][] dist = g.GetEdge().Data;
            int chunk = (n + numThreads - 1) / numThreads;

            using var barrier = new Barrier(numThreads);
            var threads = new Thread[numThreads];

            for (int t = 0; t < numThreads; t++)
            {
                int tid = t;
                int start = tid * chunk;
                int end = Math.Min(n, start + chunk);

                threads[t] = new Thread(() =>
                {
                    for (int k = 0; k < n; k++)
                    {
                        for (int i = start; i < end; i++)
                        {
                            int dik = dist[i][k];
                            for (int j = 0; j < n; j++)
                            {
                                int alt = dik + dist[k][j];
                                if (alt < dist[i][j])
                                    dist[i][j] = alt;
                            }
                        }
                        barrier.SignalAndWait();
                    }
                });
                threads[t].Start();
            }

            foreach (var th in threads)
                th.Join();
        }

        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.Write("Кількість потоків: ");
            int numThreads = int.Parse(Console.ReadLine());
            Console.WriteLine();

            int n = 1000;
            var f = new Matrix(n, 0.8, 100);

            var g = new Graph(n, f);
            var gPar = new Graph(n, f);
            var swSeq = Stopwatch.StartNew();
            g.FloydAlgorithm();
            swSeq.Stop();
            double timeSeq = swSeq.Elapsed.TotalSeconds;
            var swPar = Stopwatch.StartNew();
            RunParallelFloyd(gPar, n, numThreads);
            swPar.Stop();
            double timePar = swPar.Elapsed.TotalSeconds;

            double speedup = timeSeq / timePar;
            double efficiency = speedup / numThreads * 100;

            Console.WriteLine($"Time seq: {timeSeq} | Time par: {timePar}");
            Console.WriteLine($"Speedup: {speedup} | Efficiency: {efficiency}%");
            g.Print(1, 500);
            gPar.Print(1, 500);
        }
    }
}