using System;
using System.Diagnostics;
using System.Threading;

namespace MatrixMultiplication
{
    class Matrix
    {
        private int rows;
        private int columns;
        private int[,] data;

        public int Rows => rows;
        public int Columns => columns;

        public Matrix(int n, int m, int value = 0)
        {
            rows = n;
            columns = m;
            data = new int[n, m];
            Fill(value);
        }

        public Matrix(Matrix other)
        {
            rows = other.rows;
            columns = other.columns;
            data = new int[rows, columns];
            Array.Copy(other.data, data, data.Length);
        }

        public int this[int i, int j]
        {
            get
            {
                if (i < 0 || i >= rows || j < 0 || j >= columns)
                    throw new IndexOutOfRangeException("Matrix index out of bounds");
                return data[i, j];
            }
            set
            {
                if (i < 0 || i >= rows || j < 0 || j >= columns)
                    throw new IndexOutOfRangeException("Matrix index out of bounds");
                data[i, j] = value;
            }
        }

        public void Fill(int value)
        {
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < columns; j++)
                    data[i, j] = value;
        }

        public void Print()
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                    Console.Write(data[i, j] + " ");
                Console.WriteLine();
            }
        }

        public static Matrix operator *(Matrix a, Matrix b)
        {
            if (a.columns != b.rows)
                throw new ArgumentException("Matrix dimensions must match: A.columns must equal B.rows");

            Matrix res = new Matrix(a.rows, b.columns);
            for (int i = 0; i < a.rows; i++)
            {
                for (int j = 0; j < b.columns; j++)
                {
                    long sum = 0;
                    for (int k = 0; k < a.columns; k++)
                        sum += (long)a[i, k] * b[k, j];
                    res[i, j] = (int)sum;
                }
            }
            return res;
        }
        public static void MultiplyRowRange(Matrix a, Matrix b, Matrix res, int startRow, int endRow)
        {
            for (int i = startRow; i < endRow; i++)
            {
                for (int j = 0; j < b.Columns; j++)
                {
                    long sum = 0;
                    for (int k = 0; k < a.Columns; k++)
                        sum += (long)a[i, k] * b[k, j];
                    res[i, j] = (int)sum;
                }
            }
        }

        public bool Equals(Matrix other)
        {
            if (rows != other.rows || columns != other.columns) return false;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < columns; j++)
                    if (data[i, j] != other.data[i, j]) return false;
            return true;
        }
    }

    class Program
    {
        static void Main()
        {
            int n = 2500;
            int m = 2500;
            int l = 2500;

            int k = 2;

            Console.WriteLine("==================================================");
            Console.WriteLine("           MATRICES MULTIPLICATION (C#)");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Matrix A: [{n} x {m}],  Matrix B: [{m} x {l}],  Result C: [{n} x {l}]");
            Console.WriteLine($"Threads (k): {k}");
            Console.WriteLine("--------------------------------------------------");

            Matrix A = new Matrix(n, m, 3);
            Matrix B = new Matrix(m, l, 4);
            Matrix C_parallel = new Matrix(n, l);
            var sw = Stopwatch.StartNew();

            int step = n / k;
            Thread[] threads = new Thread[k];

            for (int t = 0; t < k; t++)
            {
                int startRow = t * step;
                int endRow = (t == k - 1) ? n : startRow + step; 
                threads[t] = new Thread(() => Matrix.MultiplyRowRange(A, B, C_parallel, startRow, endRow));
                threads[t].Start();
            }
            for (int t = 0; t < k; t++)
                threads[t].Join();

            sw.Stop();
            double parallelTime = sw.Elapsed.TotalSeconds;

            sw.Restart();
            Matrix C_sequential = A * B;
            sw.Stop();
            double sequentialTime = sw.Elapsed.TotalSeconds;

            bool correct = C_parallel.Equals(C_sequential);

            double speedup = sequentialTime / parallelTime;
            double efficiency = (speedup / k) * 100.0;

            Console.WriteLine("==================================================");
            Console.WriteLine("                   RESULTS");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Threads (k):          {k}");
            Console.WriteLine($"Parallel time:        {parallelTime:F4} seconds");
            Console.WriteLine($"Sequential time:      {sequentialTime:F4} seconds");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"Speedup:              {speedup:F2}x");
            Console.WriteLine($"Efficiency:           {efficiency:F2}%");
            Console.WriteLine($"Performance gain:     {(sequentialTime - parallelTime):F4} seconds");
            Console.WriteLine($"Results match:        {correct}");
            Console.WriteLine("==================================================");
        }
    }
}