using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;


namespace TwitchIRC
{
    class Program
    {
        static bool _cancel = false;

        static volatile int _count = 0;
        static object _countLock = new object();

        static Mutex _commentMutex = new Mutex(false);
        static Mutex _wordFreqMutex = new Mutex(false);

        static List<string> CommentList { get; set; } = new List<string>();
        // Dictionary that holds the word frequencies
        static Dictionary<string, int> WordFrequency { get; set; } = new Dictionary<string, int>();

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
                    var timeToRun = options.TimeToFetch;

                    // Create one producer thread for each channel & ignore duplicate channels
                    foreach (var channel in options.Channels.Split(',').Distinct())
                        new Thread(new ParameterizedThreadStart(CommentFetcher)).Start(channel);

                    // Create two consumers
                    new Thread(CommentConsumer).Start();
                    new Thread(CommentConsumer).Start();

                    Console.WriteLine($"Word frequency will be displayed in {options.TimeToFetch / 1000}s");

                    // UI thread sleeps for user input time
                    Thread.Sleep((int)options.TimeToFetch);
                    // End threads using deferred cancellation
                    _cancel = true;

                    Console.Clear();
                    if (WordFrequency.Count == 0)
                        Console.WriteLine("No comments were fetched, please try another channel or a longer time period");
                    else
                    {
                        Console.WriteLine("***Word Frequency***");
                        
                        foreach (var pair in WordFrequency.OrderByDescending(i => i.Value).Take(15))
                            Console.WriteLine($"({pair.Value}) {pair.Key}");

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

        /// <summary>
        /// Asks user if he would like to export the word frequency dictionary to a CSV file
        /// </summary>
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
                string headers = "Word,Frequency" + Environment.NewLine;

                string csv = headers + string.Join(Environment.NewLine,
                                                   WordFrequency.OrderByDescending(d => d.Value)
                                                                .Select(d => d.Key.Replace(',','_') + "," + d.Value));

                string path = Path.Combine(Directory.GetCurrentDirectory(), fileTitle);
                File.WriteAllText(path, csv);

                // Open file
                try { Process.Start("notepad.exe", path); }
                catch (Win32Exception) { /* User doesn't have notepad installed */ }

                Console.WriteLine($"{fileTitle} saved");
            }

        }

        #region Producer

        static void CommentFetcher(object channel)
        {
            Debug.WriteLine($"Producer thread {Thread.CurrentThread.ManagedThreadId} starting");

            var producer = new Producer();

            producer.CommentReceived += Producer_CommentReceived;
            producer.ErrorReceived += Producer_ErrorReceived;

            producer.Connect(channel.ToString());

            while (!_cancel)
                ; // Do nothing

            producer.CommentReceived -= Producer_CommentReceived;
            producer.ErrorReceived -= Producer_ErrorReceived;

            Debug.WriteLine($"Producer thread {Thread.CurrentThread.ManagedThreadId} ending");
        }

        static void Producer_CommentReceived(object sender, string comment)
        {           
            _commentMutex.WaitOne();
            // *** START CRITICAL SECTION ***

            //Debug.WriteLine($"{comment} produced");
            CommentList.Add(comment);

            // *** END CRITICAL SECTON ***
            _commentMutex.ReleaseMutex();         
        }

        static void Producer_ErrorReceived(object sender, string error)
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
                string item = string.Empty;

                lock(_countLock)
                {
                    // Only take items which have not yet been taken
                    if (CommentList.Count > _count)
                    {
                        item = CommentList.ElementAt(_count++);
                    }
                }

                // Ignore null/empty strings
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                // Split comment into words
                string[] words = item.Split(' ');

                _wordFreqMutex.WaitOne();
                // *** START WORD FREQUENCY CRITICAL SECTION ***

                // Add words to word cloud
                foreach (var rawWord in words)
                {
                    string word = rawWord.Trim();
                    
                    // If dictionary does not contain the word, add a new entry
                    if (!WordFrequency.Keys.Any(s => s == word))
                        WordFrequency.Add(word, 1);
                    else // Dictionary already contains word, increment its count
                        ++WordFrequency[word];
                }

                // *** END WORD FREQUENCY CRITICAL SECTON ***
                _wordFreqMutex.ReleaseMutex();
            }
            while (!_cancel);

            Debug.WriteLine($"Consumer thread {Thread.CurrentThread.ManagedThreadId} ending");
        }

        #endregion  
    }
}

