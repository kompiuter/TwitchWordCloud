using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;


namespace TwitchIRC
{
    class Program
    {
        const int BUFFER_SIZE = 50;
        static string[] Buffer = new string[BUFFER_SIZE];
        
        // Number of items currently in buffer
        static int count = -1;

        static uint _timeToRun = 2000;
        static bool _cancel = false;

        static Semaphore empty = new Semaphore(50, 50);
        static Semaphore full = new Semaphore(0, 50);
        static Mutex mutex = new Mutex(false);

        // Dictionary that holds the word frequencies
        static Dictionary<string, int> WordFrequency = new Dictionary<string, int>();

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
                    _timeToRun = options.TimeToFetch;

                    // Create one producer thread for each channel & ignore duplicate channels
                    foreach (var channel in options.Channels.Split(',').Distinct())
                        new Thread(new ParameterizedThreadStart(CommentFetcher)).Start(channel);

                    var consumer = new Thread(CommentConsumer);
                    consumer.Start();

                    Console.WriteLine($"Word frequency will be displayed in {options.TimeToFetch / 1000}s");

                    // UI thread sleeps for time indicated by user after which threads are cancelled
                    Thread.Sleep((int)options.TimeToFetch + 500);
                    _cancel = true;
                    try
                    {
                        if (consumer.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                            consumer.Abort();
                    }
                    catch (ThreadAbortException) { }

                    Console.Clear();
                    if (WordFrequency.Count == 0)
                        Console.WriteLine("No comments were fetched, please try another channel or a longer time period");
                    else
                    {
                        Console.WriteLine("***Word Frequency***");
                        WordFrequency.OrderBy(k => k.Key);
                        foreach (var pair in WordFrequency.OrderByDescending(i => i.Value).Take(15))
                        {
                            Console.WriteLine($"({pair.Value}) {pair.Key}");
                        }
                        AskExportToCSV();
                    }
                }
            }
            else // Invalid arguments
            {
                Console.WriteLine("Invalid arguments supplied");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        static void AskExportToCSV()
        {
            Console.WriteLine("Would you like to export the above list to a CSV file? (y/n)");

            string userInput;
            do
            {
                userInput = Console.ReadLine()?.ToLower();
            } while (userInput != "y" && userInput != "n");

            if (userInput == "y")
            {
                string fileTitle = $"wordFreq--{DateTime.Now.ToString("dd-MM-yyyy--hh-mm-ss")}.csv";
                string headers = "Word,Frequency\n";
                string csv = headers + string.Join(Environment.NewLine,
                                                   WordFrequency.OrderByDescending(d => d.Value)
                                                                .Select(d => d.Key.Replace(',','_') + "," + d.Value));

                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), fileTitle), csv);
                Console.WriteLine($"{fileTitle} saved");
            }

        }

        #region Producer

        static void CommentFetcher(object channel)
        {
            Debug.WriteLine($"Producer thread {Thread.CurrentThread.ManagedThreadId} starting");

            var client = new TwitchClient(_timeToRun);

            client.CommentReceived += Client_CommentReceived;
            client.ErrorReceived += Client_ErrorReceived;

            client.Connect(channel.ToString());
            
            client.CommentReceived -= Client_CommentReceived;
            client.ErrorReceived -= Client_ErrorReceived;
            
            Debug.WriteLine($"Producer thread {Thread.CurrentThread.ManagedThreadId} ending");
        }


        static void Client_CommentReceived(object sender, string comment)
        {
            Debug.WriteLine("Comment received");

            empty.WaitOne();
            mutex.WaitOne();
            
            // *** START CRITICAL SECTION ***
            
            ++count;

            Buffer[count] = comment;

            // *** END CRITICAL SECTON ***

            mutex.ReleaseMutex();
            full.Release();
            
            // Uncomment to observe comments being received in real-time
            //Console.WriteLine($"{comment} produced");
        }

        static void Client_ErrorReceived(object sender, string error)
        {
            Console.WriteLine(error);
            Console.WriteLine("Aborting...");
        }

        #endregion

        #region Consumer

        static void CommentConsumer()
        {
            Debug.WriteLine($"Consumer thread {Thread.CurrentThread.ManagedThreadId} starting");

            do
            {
                full.WaitOne();
                mutex.WaitOne();

                Debug.WriteLine("Comment being processed");

                // *** START CRITICAL SECTION ***

                string item = Buffer[count];
                
                --count;

                // *** END CRITICAL SECTON ***

                mutex.ReleaseMutex();
                empty.Release();


                // Split comment into words
                string[] words = item.Split(' ');


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

            }
            while (!_cancel);

            Debug.WriteLine($"Consumer thread {Thread.CurrentThread.ManagedThreadId} ending");
        }

        #endregion  
    }
}

