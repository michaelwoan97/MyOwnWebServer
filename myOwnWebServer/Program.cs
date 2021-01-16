/**
 *      FILE            :       Program.cs
 *      PROJECT         :       A-06: My Own Web Server
 *      PROGRAMMER      :       NGHIA NGUYEN 8616831
 *      DESCRIPTION     :       The purpose of this class is create a server to listen incoming requests and send responses back
 *      FIRST VERSION   :       2020-11-25
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace myOwnWebServer
{
    /** \class      Program
   * 
   *   \brief      The purpose of this class is to create a server to listen incoming requests and send responses back
   * 
   *   \author     <i>Nghia Nguyen</i>
   */
    class Program
    {
        
        static void Main(string[] args)
        {
            MyWebServer webServer = new MyWebServer();
            // Check whether the command-line arguments are provided
            if(args.Length == 0 || args.Length != 3) 
            {
                Console.WriteLine("Please provide 3 command-line arguments.");
                return;
            }

            if(webServer.CreateMyOwnWebServer(args) != 0)
            {
                return;
            }
            webServer.StartWebServer();

        }
    }
}
