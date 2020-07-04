using RegisterApp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

public static class Utils
{
    public static void Logging(string message)
    {
        Console.WriteLine(message);
        if (MainConfig.DEBUG)
        {   
            try
            {
                if (!File.Exists(MainConfig.DEBUG_FILE))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = File.CreateText(MainConfig.DEBUG_FILE))
                    {
                        sw.WriteLine("Debug log for RegisterApp");
                    }
                }

                // This text is always added, making the file longer over time
                // if it is not deleted.
                using (StreamWriter sw = File.AppendText(MainConfig.DEBUG_FILE))
                {
                    sw.WriteLine(DateTime.Now + "     " + message);
                }
            }
            catch
            {
                //Console.WriteLine("Error writing log");
            }
        }
    }
}