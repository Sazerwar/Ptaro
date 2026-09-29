using System;
using System.Collections.Generic;
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
            Data[i] = new int[n];

        // Неорієнтований граф: симетрична матриця
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (rnd.NextDouble() < density)
                {
                    int weight = rnd.Next(1, maxWeight + 1);
                    Data[i][j] = weight;
                    Data[j][i] = weight;
                }
            }
        }
    }
}

struct LocalMinEdge
{
    public int MinWeight;
    public int From;
    public int To;

    public static LocalMinEdge Empty => new LocalMinEdge { MinWeight = int.MaxValue, From = -1, To = -1 };
}

class Graph
{
    private readonly int[] vershini;
    private readonly Matrix rebra;

    public Graph()
    {
        vershini = new[] { 0 };
        rebra = new Matrix();
    }

    // Матриця копіюється (0 = немає ребра)
    public Graph(int n, Matrix f)
    {
        vershini = new int[n];
        for (int i = 0; i < n; i++) vershini[i] = i + 1;

        rebra = new Matrix { Data = new int[n][] };
        for (int i = 0; i < n; i++)
        {
            rebra.Data[i] = new int[n];
            for (int j = 0; j < n; j++)
                rebra.Data[i][j] = i == j ? 0 : f.Data[i][j];
        }
    }

    public int[] GetTop() => vershini;
    public Matrix GetEdge() => rebra;

    public List<(int, int)> PrimaAlgorithm(int startTop)
    {
        int n = vershini.Length;
        var inTree = new bool[n];
        var mstEdges = new List<(int, int)>();

        inTree[startTop] = true;

        for (int k = 0; k < n - 1; k++)
        {
            int minWeight = int.MaxValue;
            int from = -1, to = -1;

            for (int i = 0; i < n; i++)
            {
                if (!inTree[i]) continue;
                int[] row = rebra.Data[i];
                for (int j = 0; j < n; j++)
                {
                    if (!inTree[j] && row[j] != 0 && row[j] < minWeight)
                    {
                        minWeight = row[j];
                        from = i;
                        to = j;
                    }
                }
            }

            if (to != -1)
            {
                inTree[to] = true;
                mstEdges.Add((from, to));
            }
        }

        return mstEdges;
    }

    private void FindMinEdgeBlock(bool[] inTree, int startRow, int endRow, out LocalMinEdge result)
    {
        int n = vershini.Length;
        result = LocalMinEdge.Empty;

        for (int i = startRow; i < endRow; i++)
        {
            if (!inTree[i]) continue;
            int[] row = rebra.Data[i];
            for (int j = 0; j < n; j++)
            {
                if (!inTree[j] && row[j] > 0 && row[j] < result.MinWeight)
                {
                    result.MinWeight = row[j];
                    result.From = i;
                    result.To = j;
                }
            }
        }
    }

    public List<(int, int)> ParalelPrimaAlgorithm(int startTop, int numOfThreads)
    {
        int n = vershini.Length;
        var inTree = new bool[n];
        var mstEdges = new List<(int, int)>();
        var localResults = new LocalMinEdge[numOfThreads];
        var opts = new ParallelOptions { MaxDegreeOfParallelism = numOfThreads };

        inTree[startTop] = true;

        for (int k = 0; k < n - 1; k++)
        {
            // Кожен потік обробляє свій блок рядків
            Parallel.For(0, numOfThreads, opts, t =>
            {
                int rowsPerThread = n / numOfThreads;
                int startRow = t * rowsPerThread;
                int endRow = (t == numOfThreads - 1) ? n : startRow + rowsPerThread;
                FindMinEdgeBlock(inTree, startRow, endRow, out localResults[t]);
            });

            // Редукція: обираємо мінімальне ребро серед результатів потоків
            int minWeight = int.MaxValue;
            int from = -1, to = -1;
            foreach (var res in localResults)
            {
                if (res.To != -1 && res.MinWeight < minWeight)
                {
                    minWeight = res.MinWeight;
                    from = res.From;
                    to = res.To;
                }
            }

            if (to != -1)
            {
                inTree[to] = true;
                mstEdges.Add((from, to));
            }
        }

        return mstEdges;
    }

    public long TotalWeight(List<(int, int)> edges)
    {
        long sum = 0;
        foreach (var (a, b) in edges) sum += rebra.Data[a][b];
        return sum;
    }
}

class Program
{
    static void Main()
    {
        int n = 2000;
        int numThreads = 4;

        Console.WriteLine($"Генерація графа на {n} вершин...");
        var M = new Matrix(n, 0.3, 100);
        var G = new Graph(n, M);

        Console.WriteLine("Запуск ПОСЛІДОВНОГО алгоритму Пріма...");
        var sw = Stopwatch.StartNew();
        var seq = G.PrimaAlgorithm(1);
        sw.Stop();
        double timeSeq = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Час виконання (послідовний): {timeSeq:F3} секунд");

        Console.WriteLine($"\nЗапуск ПАРАЛЕЛЬНОГО алгоритму Пріма ({numThreads} потоків)...");
        sw.Restart();
        var par = G.ParalelPrimaAlgorithm(1, numThreads);
        sw.Stop();
        double timePar = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Час виконання (паралельний): {timePar:F3} секунд");

        double speedup = timeSeq / timePar;
        double efficiency = speedup / numThreads;
        Console.WriteLine("\n-----------------------------------");
        Console.WriteLine($"Прискорення (Speedup, S): {speedup:F3}x");
        Console.WriteLine($"Ефективність (Efficiency, E): {efficiency * 100.0:F1}%");

        // Перевірка: вага кістяка має збігатися
        Console.WriteLine($"Вага MST: seq = {G.TotalWeight(seq)}, par = {G.TotalWeight(par)}");
    }
}