using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;


namespace TwitchIRC
{
    class Program
    {
        const int BUFFER_SIZE = 50;
        static string[] Buffer = new string[BUFFER_SIZE];

        /// <summary>
        /// Set to true to cancel any running threads
        /// </summary>
        static volatile bool _cancel = false;

        /// <summary>
        /// Number of items currently in buffer
        /// </summary>
        static int count = -1;
        static object countLock = new object();

        static Semaphore empty = new Semaphore(50, 50);
        static Semaphore full = new Semaphore(0, 50);
        static Mutex mutex = new Mutex(false);

        // Dictionary that holds the word frequencies
        static Dictionary<string, int> WordFrequency = new Dictionary<string, int>();
        static object wordFreqLock = new object();

        static void Main(string[] args)
        {
            var options = new Options();
            // Valid arguments
            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                if (options.TimeToFetch < 2000)
                    Console.WriteLine("Time must be at least 2000ms");
                else
                {
                    // Create one producer thread for each channel & ignore duplicate channels
                    foreach (var channel in options.Channels.Split(',').Distinct())
                    {
                        new Thread(new ParameterizedThreadStart(CommentFetcher)).Start(channel);
                    }
                    // Create two consumer threads
                    new Thread(CommentConsumer).Start();
                    new Thread(CommentConsumer).Start();

                    Console.WriteLine($"Word frequency will be displayed in {options.TimeToFetch / 1000}s");

                    // UI thread sleeps for time indicated by user after which threads are cancelled
                    Thread.Sleep((int)options.TimeToFetch);
                    _cancel = true;

                    Console.Clear();
                    if (WordFrequency.Count == 0)
                        Console.WriteLine("No comments were fetched, please try another channel");
                    else
                    {
                        Console.WriteLine("***Word Frequency***");
                        WordFrequency.OrderBy(k => k.Key);
                        foreach (var pair in WordFrequency.OrderByDescending(i => i.Value).Take(15))
                        {
                            Console.WriteLine($"({pair.Value}) {pair.Key}");
                        }
                    }
                }
            }
            else // Invalid arguments
            {
                Console.WriteLine("Invalid arguments supplied");
            }

            Console.ReadLine();
        }

        static void CommentFetcher(object channel)
        {
            var client = new TwitchClient();
            client.CommentReceived += (s, comment) =>
            {
                empty.WaitOne();
                mutex.WaitOne();
                 
                // *** START CRITICAL SECTION ***

                lock (countLock)
                {
                    ++count;
                }

                Buffer[count] = comment;

                // *** END CRITICAL SECTON ***

                mutex.ReleaseMutex();
                full.Release();

                if (_cancel == true)
                    client.Cancel = true;

                // Uncomment to observe comments being received in real-time
                //Console.WriteLine($"{comment} produced");
            };
            client.ErrorReceived += (s, error) =>
            {
                Console.WriteLine(error);
                Console.WriteLine("Aborting...");
                _cancel = true;
            };

            client.Connect(channel.ToString());
        }

        static void CommentConsumer()
        {
            while (!_cancel)
            {
                full.WaitOne();
                mutex.WaitOne();

                // *** START CRITICAL SECTION ***

                string item = Buffer[count];

                lock (countLock)
                {
                    --count;
                }

                // *** END CRITICAL SECTON ***

                mutex.ReleaseMutex();
                empty.Release();

                // Split comment into words
                string[] words = item.Split(' ');

                lock (wordFreqLock)
                {
                    // Add words to word cloud
                    foreach (var rawWord in words)
                    {
                        string word = rawWord.Trim();

                        // If dictionary does not contain the word, add a new entry
                        if (!WordFrequency.Keys.Any(s => s == word))
                        {
                            WordFrequency.Add(word, 1);
                        }
                        else // Dictionary already contains word, increment its count
                        {
                            ++WordFrequency[word];
                        }
                    }
                } // end lock
            } // end while
        }
    }
}

