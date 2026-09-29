using System;
using System.Diagnostics;
using System.Threading.Tasks;

class Matrix
{
    public int[][] Data;

    public Matrix()
    {
        Data = Array.Empty<int[]>();
    }

    public Matrix(int n, double density = 0.3, int maxWeight = 100)
    {
        var rnd = new Random();
        Data = new int[n][];
        for (int i = 0; i < n; i++)
        {
            Data[i] = new int[n];
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                if (rnd.NextDouble() < density)
                    Data[i][j] = rnd.Next(1, maxWeight + 1);
            }
        }
    }
}

class Graph
{
    public const int INF = 100_000_000;

    private readonly int[] vershini;
    private readonly Matrix rebra;

    public Graph()
    {
        vershini = new[] { 0 };
        rebra = new Matrix();
    }

    // Матриця перетворюється "на місці" (0 -> INF), щоб не дублювати ~1.6 ГБ пам'яті
    public Graph(int n, Matrix f)
    {
        vershini = new int[n];
        for (int i = 0; i < n; i++) vershini[i] = i + 1;

        rebra = f;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                if (i == j) rebra.Data[i][j] = 0;
                else if (rebra.Data[i][j] == 0) rebra.Data[i][j] = INF;
            }
    }

    public int[] GetTop() => vershini;
    public Matrix GetEdge() => rebra;

    public int[] DijkstraAlgorithm(int start)
    {
        int n = vershini.Length;
        var visited = new bool[n];
        var dist = new int[n];
        Array.Fill(dist, INF);
        dist[start] = 0;

        for (int count = 0; count < n - 1; count++)
        {
            int minDist = INF;
            int u = -1;
            for (int i = 0; i < n; i++)
            {
                if (!visited[i] && dist[i] < minDist)
                {
                    minDist = dist[i];
                    u = i;
                }
            }

            if (u == -1) break;
            visited[u] = true;

            int[] row = rebra.Data[u];
            for (int v = 0; v < n; v++)
            {
                if (!visited[v] && row[v] != INF)
                    dist[v] = Math.Min(dist[v], dist[u] + row[v]);
            }
        }

        return dist;
    }

    public int[] DijkstraParallel(int start, int threads)
    {
        int n = vershini.Length;
        var dist = new int[n];
        Array.Fill(dist, INF);
        var visited = new bool[n];
        dist[start] = 0;

        var opts = new ParallelOptions { MaxDegreeOfParallelism = threads };
        var localMin = new int[threads];
        var localU = new int[threads];

        for (int count = 0; count < n - 1; count++)
        {
            // Паралельний пошук мінімуму: кожен потік обробляє свій блок
            Parallel.For(0, threads, opts, t =>
            {
                int from = (int)((long)t * n / threads);
                int to = (int)((long)(t + 1) * n / threads);
                int lMin = INF, lU = -1;
                for (int i = from; i < to; i++)
                {
                    if (!visited[i] && dist[i] < lMin)
                    {
                        lMin = dist[i];
                        lU = i;
                    }
                }
                localMin[t] = lMin;
                localU[t] = lU;
            });

            // Редукція результатів потоків
            int u = -1;
            int minDist = INF;
            for (int t = 0; t < threads; t++)
            {
                if (localMin[t] < minDist)
                {
                    minDist = localMin[t];
                    u = localU[t];
                }
            }

            if (u == -1) break;
            visited[u] = true;

            int[] row = rebra.Data[u];
            int du = dist[u];

            // Паралельна релаксація ребер
            Parallel.For(0, threads, opts, t =>
            {
                int from = (int)((long)t * n / threads);
                int to = (int)((long)(t + 1) * n / threads);
                for (int v = from; v < to; v++)
                {
                    if (!visited[v] && row[v] != INF && du + row[v] < dist[v])
                        dist[v] = du + row[v];
                }
            });
        }

        return dist;
    }
}

class Program
{
    static void Main()
    {
        int n = 20000;
        int desiredThreads = 4;

        var m = new Matrix(n, 0.2, 50);
        var g = new Graph(n, m);

        int actualThreads = Math.Min(desiredThreads, Environment.ProcessorCount);
        Console.WriteLine($"Num of Threads {actualThreads}");

        var sw = Stopwatch.StartNew();
        var seq = g.DijkstraAlgorithm(0);
        sw.Stop();
        double seqTime = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Seq time: {seqTime:F3} s");

        sw.Restart();
        var par = g.DijkstraParallel(0, actualThreads);
        sw.Stop();
        double parTime = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Par time: {parTime:F3} s");

        Console.WriteLine($"Speedup: {seqTime / parTime:F3}");
        Console.WriteLine($"Efficiency: {seqTime / parTime / actualThreads * 100:F1}%");

        // Перевірка коректності
        bool ok = true;
        for (int i = 0; i < n; i++)
            if (seq[i] != par[i]) { ok = false; break; }
        Console.WriteLine($"Results match: {ok}");
    }
}