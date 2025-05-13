using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using ScottPlot;

namespace TPProj
{
    class Program
    {
        static double int_z = 20.0;
        static double int_o = 2.0;
        static int count_zayvok = 100;
        static int count_potok = 5;
        static double[] parameters = Statistic(count_potok, int_z, int_o);

        static void Main()
        {
            List<long> periods = new List<long>();

            Server server = new Server(int_o, count_potok);
            Client client = new Client(server);

            Console.WriteLine("Теоретические показатели:");
            Console.WriteLine("1. Вероятность простоя системы: {0:F4}", parameters[0]);
            Console.WriteLine("2. Вероятность отказа системы: {0:F4}", parameters[1]);
            Console.WriteLine("3. Относительная пропускная способность: {0:F4}", parameters[2]);
            Console.WriteLine("4. Абсолютная пропускная способность: {0:F4}", parameters[3]);
            Console.WriteLine("5. Среднее число занятых каналов: {0:F4}", parameters[4]);
            Console.WriteLine();

            for (int id = 1; id <= count_zayvok; id++)
            {
                double time_son = 1000 / int_z;
                Thread.Sleep((int)time_son);
                client.send(id);
            }

            double lambda = server.requestCount / (server.requestCount * (1000 / int_z) / 1000.0);
            double mew = 1.0 / (server.periods.Average() / 1000.0);

            double[] parameters2 = Statistic(count_potok, lambda, mew);

            Thread.Sleep(1000);

            Console.WriteLine("\nПрактические результаты:");
            Console.WriteLine("Всего заявок: {0}", server.requestCount);
            Console.WriteLine("Обработано заявок: {0}", server.processedCount);
            Console.WriteLine("Отклонено заявок: {0}", server.rejectedCount);

            Console.WriteLine("\nПрактические показатели:");
            Console.WriteLine("1. Вероятность простоя системы: {0:F4}", parameters2[0]);
            Console.WriteLine("2. Вероятность отказа системы: {0:F4}", parameters2[1]);
            Console.WriteLine("3. Относительная пропускная способность: {0:F4}", parameters2[2]);
            Console.WriteLine("4. Абсолютная пропускная способность: {0:F4}", parameters2[3]);
            Console.WriteLine("5. Среднее число занятых каналов: {0:F4}", parameters2[4]);

            var (theoryParams, expParams) = GatherDataForGraphics();
            DrawGraphics(theoryParams, expParams);
        }

        static (double[][], double[][]) GatherDataForGraphics()
        {
            double[] inputIntensities = { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 };
            double serviceIntensity = 2.0;
            int requestCount = 100;
            int channelCount = 5;
            int pointsCount = inputIntensities.Length;

            double[][] theoryParams = new double[pointsCount][];
            double[][] expParams = new double[pointsCount][];

            for (int j = 0; j < pointsCount; j++)
            {
                Server server = new Server(serviceIntensity, channelCount);
                Client client = new Client(server);

                for (int id = 1; id <= requestCount; id++)
                {
                    client.send(id);
                    Thread.Sleep((int)(1000 / inputIntensities[j]));
                }

                Thread.Sleep(500);

                double rejectionProb = (double)server.rejectedCount / requestCount;
                double relativeThroughput = 1 - rejectionProb;
                double absoluteThroughput = inputIntensities[j] * relativeThroughput;
                double avgBusyChannels = absoluteThroughput / serviceIntensity;
                if (avgBusyChannels > 5) avgBusyChannels = 5;

                expParams[j] = new double[] {
                    server.idleProbability,
                    rejectionProb,
                    relativeThroughput,
                    absoluteThroughput,
                    avgBusyChannels
                };

                theoryParams[j] = Statistic(channelCount, inputIntensities[j], serviceIntensity);

                Console.WriteLine($"Завершена точка {j + 1}/{pointsCount} (λ={inputIntensities[j]})");
            }

            return (theoryParams, expParams);
        }

        static void DrawGraphics(double[][] theoryParams, double[][] expParams)
        {
            double[] inputIntensities = { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 };
            string[] titles = {
                "Вероятность простоя системы",
                "Вероятность отказа системы",
                "Относительная пропускная способность",
                "Абсолютная пропускная способность",
                "Среднее число занятых каналов"
            };

            string solutionDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
            string outputDir = Path.Combine(solutionDir, "result");
            Directory.CreateDirectory(outputDir);

            for (int i = 0; i < 5; i++)
            {
                var plot = new Plot();
                plot.XLabel("Интенсивность входного потока (λ)");
                plot.YLabel(titles[i]);
                plot.Title($"Зависимость {titles[i]} от интенсивности потока");

                var theory = plot.Add.Scatter(
                    inputIntensities,
                    theoryParams.Select(p => p[i]).ToArray(),
                    Colors.Red
                );
                theory.LegendText = "Теоретические";

                var experimental = plot.Add.Scatter(
                    inputIntensities,
                    expParams.Select(p => p[i]).ToArray(),
                    Colors.Blue
                );
                experimental.LegendText = "Экспериментальные";

                plot.ShowLegend();
                plot.Legend.Alignment = Alignment.UpperRight;

                plot.SavePng(Path.Combine(outputDir, $"p-{i + 1}.png"), 800, 600);
            }
        }


        static int Fact(int t)
        {
            int j = t;
            int fact = 1;
            while (j != 0)
            {
                fact *= j;
                j--;
            }
            return fact;
        }

        static double[] Statistic(int count_potok, double int_z, double int_o)
        {
            double p = int_z / int_o;
            double ver_pr = 0;
            for (int i = 0; i <= count_potok; i++)
            {
                ver_pr += Math.Pow(p, i) / Fact(i);
            }
            ver_pr = Math.Pow(ver_pr, (-1));
            double ver_ot = Math.Pow(p, count_potok) / Fact(count_potok) * ver_pr;
            double otn_pr_spos = 1 - ver_ot;
            double abs_pr_spos = int_z * otn_pr_spos;
            double sr_count = abs_pr_spos / int_o;
            return [ver_pr, ver_ot, otn_pr_spos, abs_pr_spos, sr_count];
        }
    }
    struct PoolRecord
    {
        public Thread thread;
        public bool in_use;
    }

    class Server
    {
        private PoolRecord[] pool;
        private object threadLock = new object();
        public int requestCount = 0;
        public int processedCount = 0;
        public int rejectedCount = 0;
        public double int_o;
        public int count_potok;

        public double idleProbability = 0;
        private int idleCount = 0;
        private int totalSamples = 0;

        public int BusyThreads => pool.Count(x => x.in_use);

        public List<long> periods = new List<long>();

        public Server(double int_o, int count_potok)
        {
            pool = new PoolRecord[count_potok];
            this.int_o = int_o;
            this.count_potok = count_potok;
        }

        public void UpdateStatistics()
        {
            lock (threadLock)
            {
                totalSamples++;
                if (BusyThreads == 0) idleCount++;
                idleProbability = (double)idleCount / totalSamples;
            }
        }

        public void proc(object sender, procEventArgs e)
        {
            lock (threadLock)
            {
                Console.WriteLine("Заявка с номером: {0}", e.id);
                requestCount++;
                for (int i = 0; i < count_potok; i++)
                {
                    if (!pool[i].in_use)
                    {
                        pool[i].in_use = true;
                        pool[i].thread = new Thread(new ParameterizedThreadStart(Answer));
                        pool[i].thread.Start(e.id);
                        processedCount++;
                        return;
                    }
                }
                rejectedCount++;
            }
            UpdateStatistics();
        }
        public void Answer(object arg)
        {
            var timer = Stopwatch.StartNew();
            int id = (int)arg;
            Console.WriteLine("Обработка заявки: {0}", id);
            double time_son = 1000 / int_o;
            Thread.Sleep((int)time_son);
            timer.Stop();

            lock (threadLock)
            {
                periods.Add(timer.ElapsedMilliseconds);
                for (int i = 0; i < count_potok; i++)
                    if (pool[i].thread == Thread.CurrentThread)
                        pool[i].in_use = false;
            }
            UpdateStatistics();
        }
    }
    class Client
    {
        private Server server;
        public Client(Server server)
        {
            this.server = server;
            this.request += server.proc;
        }
        public void send(int id)
        {
            procEventArgs args = new procEventArgs();
            args.id = id;
            OnProc(args);
        }
        protected virtual void OnProc(procEventArgs e)
        {
            EventHandler<procEventArgs> handler = request;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        public event EventHandler<procEventArgs> request;
    }
    public class procEventArgs : EventArgs
    {
        public int id { get; set; }
    }
}