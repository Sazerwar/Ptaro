using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JacobiSolver
{
    class Matrix
    {
        private readonly int rows;
        private readonly int columns;
        private readonly double[][] data;

        public Matrix(int n, int m, double value = 0.0)
        {
            rows = n;
            columns = m;
            data = new double[n][];
            for (int i = 0; i < n; i++)
            {
                data[i] = new double[m];
                for (int j = 0; j < m; j++)
                    data[i][j] = value;
            }
        }

        public Matrix(Matrix other)
        {
            rows = other.rows;
            columns = other.columns;
            data = new double[rows][];
            for (int i = 0; i < rows; i++)
            {
                data[i] = new double[columns];
                for (int j = 0; j < columns; j++)
                    data[i][j] = other.data[i][j];
            }
        }

        public int Rows => rows;
        public int Columns => columns;

        public double this[int i, int j]
        {
            get
            {
                if (i < 0 || i >= rows) throw new IndexOutOfRangeException("Row index out of bounds");
                return data[i][j];
            }
            set
            {
                if (i < 0 || i >= rows) throw new IndexOutOfRangeException("Row index out of bounds");
                data[i][j] = value;
            }
        }

        public void Fill(double value)
        {
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < columns; j++)
                    data[i][j] = value;
        }

        public void Print()
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                    Console.Write(data[i][j] + " ");
                Console.WriteLine();
            }
        }

        public static Matrix operator +(Matrix a, Matrix b)
        {
            if (a.rows != b.rows || a.columns != b.columns)
                throw new ArgumentException("Matrix dimensions must match");

            var res = new Matrix(a.rows, a.columns);
            for (int i = 0; i < a.rows; i++)
                for (int j = 0; j < a.columns; j++)
                    res[i, j] = a[i, j] + b[i, j];
            return res;
        }

        public static Matrix operator -(Matrix a, Matrix b)
        {
            if (a.rows != b.rows || a.columns != b.columns)
                throw new ArgumentException("Matrix dimensions must match");

            var res = new Matrix(a.rows, a.columns);
            for (int i = 0; i < a.rows; i++)
                for (int j = 0; j < a.columns; j++)
                    res[i, j] = a[i, j] - b[i, j];
            return res;
        }

        public static Matrix operator *(Matrix a, Matrix b)
        {
            if (a.columns != b.rows)
                throw new ArgumentException("Matrix dimensions must match");

            var res = new Matrix(a.rows, b.columns, 0.0);
            for (int i = 0; i < a.rows; i++)
                for (int j = 0; j < b.columns; j++)
                    for (int k = 0; k < a.columns; k++)
                        res[i, j] += a[i, k] * b[k, j];
            return res;
        }
    }

    class SLAR
    {
        public Matrix Coef { get; }
        public double[] B { get; }
        public double[] X;
        private const double EPSILON = 1e-6;
        private const int MAX_ITERS = 1000;
        public SLAR(Matrix a, double[] equals)
        {
            Coef = a;
            B = equals;
            X = new double[B.Length];
        }

        public bool Jacobi()
        {
            double[] xOld = (double[])X.Clone();
            double[] xNew = (double[])X.Clone();
            for (int iter = 0; iter < MAX_ITERS; iter++)
            {
                double maxDiff = 0;
                for (int i = 0; i < Coef.Rows; i++)
                {
                    double sum = 0;
                    for (int j = 0; j < Coef.Columns; j++)
                        if (i != j) sum += Coef[i, j] * xOld[j];

                    xNew[i] = (B[i] - sum) / Coef[i, i];
                    maxDiff = Math.Max(maxDiff, Math.Abs(xNew[i] - xOld[i]));
                }
                (xOld, xNew) = (xNew, xOld);
                if (maxDiff < EPSILON)
                {
                    X = xOld;
                    return true;
                }
            }
            X = xOld;
            return false;
        }
    }

    class Program
    {
        const int N = 2000;
        const int MAX_ITERS = 1000;
        const double EPSILON = 1e-6;
        static readonly object mtx = new object();

        static void JacobiWorker(int start, int end, SLAR system, double[] xOld,
            double[] xNew, ref double maxDiff)
        {
            double localMax = 0;
            for (int i = start; i < end; i++)
            {
                double sum = 0;
                for (int j = 0; j < system.Coef.Columns; j++)
                    if (i != j) sum += system.Coef[i, j] * xOld[j];

                xNew[i] = (system.B[i] - sum) / system.Coef[i, i];
                localMax = Math.Max(localMax, Math.Abs(xNew[i] - xOld[i]));
            }

            lock (mtx)
            {
                maxDiff = Math.Max(maxDiff, localMax);
            }
        }

        static bool ParallelJacobi(SLAR system, int numThreads)
        {
            double[] xOld = (double[])system.X.Clone();
            double[] xNew = (double[])system.X.Clone();
            var threads = new Thread[numThreads];
            int step = N / numThreads;
            for (int iter = 0; iter < MAX_ITERS; iter++)
            {
                double maxDiff = 0;
                for (int t = 0; t < numThreads; t++)
                {
                    int start = t * step;
                    int end = (t == numThreads - 1) ? N : start + step;
                    double[] localOld = xOld;
                    double[] localNew = xNew;
                    threads[t] = new Thread(() =>
                    {
                        double localMax = 0;
                        for (int i = start; i < end; i++)
                        {
                            double sum = 0;
                            for (int j = 0; j < system.Coef.Columns; j++)
                                if (i != j) sum += system.Coef[i, j] * localOld[j];

                            localNew[i] = (system.B[i] - sum) / system.Coef[i, i];
                            localMax = Math.Max(localMax, Math.Abs(localNew[i] - localOld[i]));
                        }
                        lock (mtx)
                        {
                            maxDiff = Math.Max(maxDiff, localMax);
                        }
                    });
                    threads[t].Start();
                }
                foreach (var th in threads) th.Join();
                (xOld, xNew) = (xNew, xOld);

                if (maxDiff < EPSILON)
                {
                    system.X = xOld;
                    return true;
                }
            }
            system.X = xOld;
            return false;
        }

        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("==================================================");
            Console.WriteLine("          Розв'язування Систем лінійних алгебраїчних рівнянь");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Розмір матриці коефіцієнтів: [{N} x {N}]");
            Console.WriteLine($"Кількість елементів:    {N * N}");
            Console.WriteLine($"Використано пам'яті для матриці:      {(N * N * sizeof(int)) / (1024 * 1024)} MB");
            Console.WriteLine("--------------------------------------------------");

            var rand = new Random();
            var a = new Matrix(N, N);
            var b = new double[N];

            for (int i = 0; i < N; i++)
            {
                double sum = 0;
                for (int j = 0; j < N; j++)
                {
                    if (i != j)
                    {
                        a[i, j] = rand.Next(10);
                        sum += Math.Abs(a[i, j]);
                    }
                    else
                    {
                        a[i, j] = 0;
                    }
                }

                a[i, i] = sum + (rand.Next(10) + 1) + 1000;
                b[i] = rand.Next(100);
            }

            var system = new SLAR(a, b);

            // послідовний
            var swSeq = Stopwatch.StartNew();
            bool seqZbig = system.Jacobi();
            swSeq.Stop();
            double timeSeq = swSeq.Elapsed.TotalSeconds;

            // паралельний
            Array.Fill(system.X, 0.0);
            int numOfThreads = 8;
            var swPar = Stopwatch.StartNew();
            bool parZbig = ParallelJacobi(system, numOfThreads);
            swPar.Stop();
            double timePar = swPar.Elapsed.TotalSeconds;

            double speedup = timeSeq / timePar;
            double efficiency = (speedup / numOfThreads) * 100;

            Console.WriteLine("СЛАР збіжна послідовно: " + (seqZbig ? "так" : "ні"));
            Console.WriteLine("СЛАР збіжна паралельно: " + (parZbig ? "так" : "ні"));

            Console.WriteLine("==================================================");
            Console.WriteLine("                   Результати");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Кількість ітерацій:      {MAX_ITERS}");
            Console.WriteLine($"Кількість потоків(k):  {numOfThreads}");
            Console.WriteLine($"Час виконання послідовно:    {timeSeq} seconds");
            Console.WriteLine($"Час виконання паралельно:      {timePar} seconds");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"Прискорення:            {speedup}x");
            Console.WriteLine($"Ефективність:         {efficiency}%");
            Console.WriteLine($"Різниця в часі виконання:   {timeSeq - timePar} секунд");
            Console.WriteLine("==================================================");
        }
    }
}