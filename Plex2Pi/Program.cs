using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Plex2Pi
{
    internal class Program
    {
        static string CleanUpUnits(string units)
        {
            // Replace common unit abbreviations with their full forms or symbols
            units = units.Replace("deg", "\xB0"); // Degree symbol
            if (units == "g") units = "G";
            if (units == "C") units = "\xB0\x43"; // degrees Celsius symbol
            return units;
        }

        static void Main(string[] args)
        {
            //Check argumants and open files
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: Aim2PiToolbox <data_file>");
                return;
            }
            if (!File.Exists(args[0]))
            {
                Console.WriteLine("File not found: " + args[0]);
                return;
            }
                        
            string infile = args[0];
            string outfile = args[0] + ".txt";

            // Read all lines from the CSV file
            var lines = File.ReadAllLines(infile);

            // Split each line into fields
            var rows = lines.Select(line => line.Split(',')).ToList();

            // Transpose rows to columns
            int columnCount = rows[0].Length;
            var columns = new List<List<string>>();

            for (int col = 0; col < columnCount; col++)
            {
                var column = new List<string>();
                foreach (var row in rows)
                {
                    column.Add(row[col]);
                }
                columns.Add(column);
            }

            //use the LAP column to determine how many laps there are in the data set
            // Find the index of the "LAP" column header
            int lapColIndex = columns.FindIndex(col => col[0].Trim().Equals("LAP", StringComparison.OrdinalIgnoreCase));
            int numLaps = 0;
            if (lapColIndex != -1)
            {
                // Skip header row, get distinct values
                numLaps = columns[lapColIndex].Skip(1).Distinct().Count();
            }

            TimeSpan[] lapMarkers = new TimeSpan[numLaps];
            lapMarkers[0] = TimeSpan.Zero; // Start with the first lap at zero
            int lapCounter = 0;

            // Write the Pi file header
            var writer = new StreamWriter(outfile, false, Encoding.GetEncoding(1252));      //Important to use the 1252 encoding to match Pi Toolbox ASCII format      
            // File header

            writer.WriteLine("PiToolboxVersionedASCIIDataSet");
            writer.WriteLine("Version\t2");
            writer.WriteLine();
            writer.WriteLine("{OutingInformation}");
            writer.WriteLine($"CarName\tPlex");
            writer.WriteLine("FirstLapNumber\t0");

            // Cycle through and create channel blocks
            for (int i = 1; i < columns.Count; i++)
            {
                //extract units and name from string
                
                string fullChannelName = columns[i][0];
                int startIdx = fullChannelName.IndexOf('[');
                int endIdx = fullChannelName.IndexOf(']', startIdx + 1);
                string units = (startIdx != -1 && endIdx != -1 && endIdx > startIdx)
                    ? fullChannelName.Substring(startIdx, endIdx - startIdx + 1)
                    : "[user]";

                //fix units representation
                units = CleanUpUnits(units);

                string channelName = fullChannelName.Split('[')[0].Trim();

                writer.WriteLine();
                writer.WriteLine("{ChannelBlock}");
                writer.WriteLine($"Time\t{channelName}{units}");

                TimeSpan correctedTime = TimeSpan.Zero;

                for (int j = 1; j < columns[i].Count; j++)
                {
                    if (double.TryParse(columns[i][j], out double value))
                    {
                        // Subtract time from starting time (as TimeSpan)
                        DateTime.TryParse(columns[0][j], out DateTime current);
                        DateTime.TryParse(columns[0][1], out DateTime start);
                        correctedTime = current - start;
                        
                        writer.WriteLine($"{correctedTime.TotalSeconds}\t{columns[i][j]}");
                    }
                    else
                    {
                        // Handle non-numeric data (optional)
                        writer.WriteLine($"{columns[0][j]}\t{columns[i][j]}");
                    }
                    // Check for lap markers
                    int lapValue = int.TryParse(columns[lapColIndex][j], out lapValue) ? lapValue : lapCounter;

                    if (lapValue > lapCounter)
                    {
                        // Increment lap counter and store the time for this lap
                        lapCounter = lapValue;
                        lapMarkers[lapValue] = TimeSpan.FromSeconds(correctedTime.TotalSeconds);
                    }
                }
            }

            // Event block for lap breakpoints
            writer.WriteLine();
            writer.WriteLine("{EventBlock}");
            writer.WriteLine("Time\tName\tCategory\tSource\tMessage");

            for (int idx = 0; idx <= lapCounter; ++idx)
            {
                writer.WriteLine($"{lapMarkers[idx].TotalSeconds}\tEnd of lap\tToolbox Added\tDRV\tEnd of lap");
            }

            writer.Close();
        }
    }
}
