using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


namespace TwitchIRC
{
    class Program
    {
        const int BUFFER_SIZE = 50;
        static string[] Buffer = new string[BUFFER_SIZE];

        static volatile bool _cancel = false;
        static int count = -1;
        static object countLock = new object();

        static Semaphore empty = new Semaphore(50, 50);
        static Semaphore full = new Semaphore(0, 50);
        static Mutex mutex = new Mutex(false);

        // Dictionary that simulates word cloud
        static Dictionary<string, int> WordCloudDic = new Dictionary<string, int>();
        static object wordCloudLock = new object();

        static Random rnd = new Random();

        static void Main(string[] args)
        {
            var producerThread1 = new Thread(new ParameterizedThreadStart(CommentFetcher));
            var producerThread2 = new Thread(new ParameterizedThreadStart(CommentFetcher));
            var consumerThread1 = new Thread(CommentConsumer);
            var consumerThread2 = new Thread(CommentConsumer);

            //producerThread1.Start("esl_csgo");
            producerThread2.Start("sodapoppin");
            consumerThread1.Start();
            consumerThread2.Start();

            Thread.Sleep(100000);
            _cancel = true;

            WordCloudDic.OrderBy(k => k.Key);
            foreach (var pair in WordCloudDic.OrderByDescending(i => i.Value).Take(15))
            {
                Console.WriteLine($"{pair.Key} ({pair.Value})");
            }

            Console.ReadLine();
        }

        static void CommentFetcher(object channel)
        {
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            var client = new TwitchClient(channel.ToString());
            string localMessage = string.Empty;
            client.CommentReceived += (s, e) =>
            {
                empty.WaitOne();
                mutex.WaitOne();

                // START CRITICAL SECTION

                lock (countLock)
                {
                    ++count;
                }

                Buffer[count] = e;

                // END CRITICAL SECTON

                mutex.ReleaseMutex();
                full.Release();

                if (_cancel == true)
                    client.Cancel = true;

                //Console.WriteLine($"{e} produced");
            };

            client.Connect(channel.ToString());
        }

        static void CommentConsumer()
        {
            while (!_cancel)
            {
                full.WaitOne();
                mutex.WaitOne();

                // START CRITICAL SECTION WITH BUFFER

                string item = Buffer[count];

                lock (countLock)
                {
                    --count;
                }

                // END CRITICAL SECTION  WITH BUFFER

                mutex.ReleaseMutex();
                empty.Release();

                // Split comment into words
                string[] words = item.Split(' ');

                lock (wordCloudLock)
                {
                    // Add words to word cloud
                    foreach (var rawWord in words)
                    {
                        string word = rawWord.Trim();

                        // If dictionary does not contain the word, add a new entry
                        if (!WordCloudDic.Keys.Any(s => s == word))
                        {
                            WordCloudDic.Add(word, 1);
                        }
                        else // Dictionary already contains word, so increment its count
                        {
                            ++WordCloudDic[word];
                        }
                    }
                }
            }
        }
    }
}


//using ChatSharp;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace TwitchIRC
//{
//    class Program
//    {
//        static void Main(string[] args)
//        {
//            var client = new TwitchClient();
//            client.CommentReceived += (s, e) => Console.WriteLine(e.Message);
//            client.ErrorReceived += (s, e) => Console.WriteLine(e.Error);
//            client.Connect("pgl");

//            Console.ReadLine();
//        }
//    }
//}
