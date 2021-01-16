/**
 *      FILE            :       MyWebServer.cs
 *      PROJECT         :       A-06: My Own Web Server
 *      PROGRAMMER      :       NGHIA NGUYEN 8616831
 *      DESCRIPTION     :       The purpose of this class is create a server to listen incoming requests and send responses back. in addition, every request to and response from the server
 *                                  will be recorded in a log file name myOwnWebServerLog. The server accept only GET requests, and will send response with file types
 *                                   that have extension of .txt, .html (and their various extension), and image files. The server will send reponse with approriate status code
 *                                    whether the requests are success or fail
 *      FIRST VERSION   :       2020-11-25
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Security;

namespace myOwnWebServer
{
    enum IndexOfCommandLineArgument
    {
        Command,
        ArgumentValue
    }

    enum eStatusCode
    {
        Success = 200,
        BadRequest= 400,
        NotFound = 404,
        MethodNotAccepted = 406,
        UnsupportedMediaType = 415,
        Fail = 500,
        HTTPVersionNotSupported = 505
    }

    /** \class      MyWebServer
   * 
   *   \brief      The purpose of this class is to create a server to listen incoming requests and send responses back
   * 
   *   \author     <i>Nghia Nguyen</i>
   */
    class MyWebServer
    {
        List<IPAddress> hostIPAddresses = new List<IPAddress>(); // list of local IP addresses
        private TcpListener webServer = null; // web server
        private TcpClient client = null; // client
        private string webRoot = null; // web root for data of the website
        private IPAddress ipaWebServer = null; // web server ip address
        private int iWebServerPort; // listening port
        private bool bProcessWebRoot = false; // indicate whether the web root is processed
        private bool bProcessWebIP = false; // indicate whether the web server IP address is processed
        private bool bProcessWebPort = false; // indicate whether the listening port is processed
        private bool bServerStarted = false; // indicate whether the web server is started
        private static bool bLogFileIsExisted = false; // indicate whether the log file is existed when the server started
        private string[] sProcessCommand = null; // array of command to be processed
        public const int SIZE_OF_BUFFER = 2048;
        public static string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location); // get the current location of the executable
        private string logFileName = "myOwnWebServer";
        private string sHTTPProtocolVersion = "HTTP/1.1";

        /*	-- METHOD
        *	Name	:	CreateMyOwnWebServer
        *	Purpose	:	to create a web server from the provided command-line arguments. if the provided command-line argument is not valid for
        *	                creating a server, the approriate message will be displayed.
        *	Inputs	:	string[] arrCommandLineArguments : an array of command-line arguments
        *	Return	:	Return 0 if it success. otherwise, 1
        */
        public int CreateMyOwnWebServer(string[] arrCommandLineArguments)
        {
            int retCode = 1;
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName()); // get ip info of the local machine
            
            // store available IPAddress of host into a list
            for(int i = 0; i < ipHostInfo.AddressList.Length; i++)
            {
                hostIPAddresses.Add(ipHostInfo.AddressList[i]);
            }

            /* Check whether the required command-line arguments are in the command
                and check whether those command-line arguments have not processed yet
            */
            foreach (string command in arrCommandLineArguments)
            {
                sProcessCommand = null;
                if ((command.IndexOf("-webRoot=") == 0) && (bProcessWebRoot == false))
                {
                    /* Split the command into 2 part:
                     * Index 0: the command
                     * Index 1: the web root
                     */
                    sProcessCommand = command.Split('=');

                    /* Check whether the webRoot or directory for server is existed
                     */
                    if (!Directory.Exists(sProcessCommand[(int)IndexOfCommandLineArgument.ArgumentValue]))
                    {
                        Console.WriteLine("Sorry, the provided webRoot is not existed!");
                        return retCode;
                    }

                    webRoot = sProcessCommand[(int)IndexOfCommandLineArgument.ArgumentValue];
                    bProcessWebRoot = true; // the command is processed
                }
                else if ((command.IndexOf("-webIP=") == 0) && (bProcessWebIP == false))
                {
                    /* Split the command into 2 part:
                     * Index 0: the command
                     * Index 1: the IPAddress
                     */
                    sProcessCommand = command.Split('=');

                    try
                    {
                        // check whether the provided IPAddress is valid
                        if (!IPAddress.TryParse(sProcessCommand[(int)IndexOfCommandLineArgument.ArgumentValue], out ipaWebServer))
                        {
                            Console.WriteLine("Sorry, the provided IPAddress is invalid!");
                            return retCode;
                        }

                        // check whether the provided IPAddress is available to local host
                        if(!hostIPAddresses.Contains(ipaWebServer))
                        {
                            Console.WriteLine("Sorry, the provided IPAddress is not available on the current machine!");
                            return retCode;
                        }

                    }
                    catch (ArgumentNullException e)
                    {
                        Console.WriteLine("ArgumentNullException: {0}", e.Message);
                        return retCode;
                    }
                    catch(FormatException e)
                    {
                        Console.WriteLine("FormatException: {0}", e);
                        return retCode;
                    }

                    bProcessWebIP = true; // the command is processed
                }
                else if ((command.IndexOf("-webPort=") == 0) && (bProcessWebPort == false))
                {
                    /* Split the command into 2 part:
                     * Index 0: the command
                     * Index 1: the server Port
                     */
                    sProcessCommand = command.Split('=');

                    // check whether the server port is valid
                    if(!int.TryParse(sProcessCommand[(int)IndexOfCommandLineArgument.ArgumentValue], out iWebServerPort))
                    {
                        Console.WriteLine("Sorry, the provided port is invalid!");
                        return retCode;
                    }

                    bProcessWebPort = true; // the command is processed
                }
                else
                {
                    Console.WriteLine("Please provide 3 mandatory command-line arguments. (-webRoot,-webIP,-webPort)");
                    return retCode;
                }
            }

            retCode = 0;
            return retCode;

        }

        /*	-- METHOD
        *	Name	:	StartWebServer
        *	Purpose	:	the purpose of this method is to start my web server, and listen incoming requests.
        *	                if the requests is valid, the approriate response will be generated and sent back. Otherwise, if the request is fail
        *	                    the approriate response with status code will be sent back
        *	Inputs	:	Not receive anything
        *	Return	:	Not return anything
        */
        public void StartWebServer()
        {
            string requestedResource = null;
            string sContentType = null;
            int statusCode = 0;
            Byte[] bytes = new Byte[SIZE_OF_BUFFER]; // buffer for storing requests
            byte[] msg = null; // buffer for storing response
            byte[] requestedImageResource = null; // buffer for storing response for image file
            string data = null; 

            
            try
            {
                webServer = new TcpListener(ipaWebServer, iWebServerPort);

                webServer.Start();
                bServerStarted = true;

                // check whether the log file is existed
                if (File.Exists(Path.Combine(exeDir, $"{logFileName}.txt")))
                {
                    bLogFileIsExisted = true; // indicate the log file is already existed when server started
                }
                Log(logFileName, $"[Server Started] - {webRoot} {ipaWebServer} {iWebServerPort}");
                

                while (true)
                {
                    // listening incoming requests
                    client = webServer.AcceptTcpClient();

                    // a stream object for reading and writing
                    NetworkStream nsStream = client.GetStream();

                    int i = 0;

                    while((i = nsStream.Read(bytes,0,bytes.Length)) != 0)
                    {
                        data = null; // clear buffer
                        msg = null; // clear buffer of the response
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i); // decode the message sent by the client

                        // Handle Incoming request
                        // check whether the incoming request is valid
                        if(HandleIncomingRequests(data,out requestedResource,out sContentType, out statusCode) != 0)
                        {
                            // HTTP handle bad request
                            data = FormatResponse(statusCode, sContentType, data);
                            msg = System.Text.Encoding.ASCII.GetBytes(data); /// encode the message

                            // Response 
                            nsStream.Write(msg, 0, msg.Length);
                            break;
                        }

                        // check whether the requested resource is image file
                        if(sContentType == "image/jpeg" || sContentType == "image/gif")
                        {
                            // Check whether the response can be generated
                            if (GenerateResponse(requestedResource, sContentType, out requestedImageResource, out statusCode) != 0)
                            {
                                // HTTP handle bad request
                                data = FormatResponse(statusCode, sContentType, requestedImageResource);
                                msg = System.Text.Encoding.ASCII.GetBytes(data); /// encode the message

                                // Response 
                                nsStream.Write(msg, 0, msg.Length);
                                break;
                            }

                            data = FormatResponse(statusCode, sContentType, requestedImageResource);
                            msg = System.Text.Encoding.ASCII.GetBytes(data); /// encode the message

                            // Response 
                            nsStream.Write(msg, 0, msg.Length);
                            nsStream.Write(requestedImageResource, 0, requestedImageResource.Length);
                            break;

                        }

                        // Check whether the response can be generated
                        if(GenerateResponse(requestedResource,sContentType,out data,out statusCode) != 0 )
                        {
                            // HTTP handle bad request
                            data = FormatResponse(statusCode, sContentType, data);
                            msg = System.Text.Encoding.ASCII.GetBytes(data); /// encode the message

                            // Response 
                            nsStream.Write(msg, 0, msg.Length);
                            break;
                        }
                        data = FormatResponse(statusCode, sContentType, data);
                        msg = System.Text.Encoding.ASCII.GetBytes(data); /// encode the message

                        // Response 
                        nsStream.Write(msg, 0, msg.Length);

                        
                    }

                    // Shutdown connection
                    client.Close();

                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                webServer.Stop();
            }

        }

        
        /*	-- METHOD
        *	Name	:	FormatResponse
        *	Purpose	:	To format the response for text or html file types
        *	Inputs	:	int statusCode : the status code
        *	            string sContentType : the content type
        *	            string sResourceToSentBack : the resource to be sent back
        *	Return	:	Return the response message
        */
        public string FormatResponse(int statusCode, string sContentType, string sResourceToSentBack)
        {
            string firstLineResponse = null;
            string sContentLength = null;
            string sDate = null;
            string serverAddress = null;
            string response = null;

            // check for status code
            switch(statusCode)
            {
                case (int)eStatusCode.Success:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} OK";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";
                    sContentType = $"Content-Type: {sContentType}";
                    sContentLength = $"Content-Length: {sResourceToSentBack.Length.ToString()}";

                    Log(logFileName, $"[Server Started] - {sContentType} {sContentLength} {serverAddress} {sDate}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n" + sContentType + "\r\n" + sContentLength + "\r\n" + "\r\n" + sResourceToSentBack + "\r\n";
                    break;
                case (int)eStatusCode.BadRequest:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Bad Request";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.NotFound:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Not Found";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.MethodNotAccepted:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Method Not Accepted";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.UnsupportedMediaType:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Unsupported Media Type";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.HTTPVersionNotSupported:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} HTTP Version Not Supported";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                default:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Server Error";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
            }

            return response;
        }

       
        /*	-- METHOD
        *	Name	:	FormatResponse
        *	Purpose	:	An overloaded method, the purpose of this method is to format the response for image file types
        *	Inputs	:	int statusCode : the status code
        *	            string sContentType : the content type
        *	            byte[] ImageResourceToSendBack : the resource image to be sent back
        *	Return	:	Return the response message
        */
        public string FormatResponse(int statusCode, string sContentType, byte[] ImageResourceToSendBack)
        {
            string firstLineResponse = null;
            string sContentLength = null;
            string sDate = null;
            string serverAddress = null;
            string response = null;

            // check for status code
            switch (statusCode)
            {
                case (int)eStatusCode.Success:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} OK";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";
                    sContentType = $"Content-Type: {sContentType}";
                    sContentLength = $"Content-Length: {ImageResourceToSendBack.Length.ToString()}";

                    Log(logFileName, $"[Server Started] - {sContentType} {sContentLength} {serverAddress} {sDate}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n" + sContentType + "\r\n" + sContentLength + "\r\n" + "\r\n";
                    break;
                case (int)eStatusCode.BadRequest:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Bad Request";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.NotFound:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Not Found";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.MethodNotAccepted:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Method Not Accepted";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.UnsupportedMediaType:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Unsupported Media Type";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                case (int)eStatusCode.HTTPVersionNotSupported:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} HTTP Version Not Supported";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
                default:
                    firstLineResponse = $"{sHTTPProtocolVersion} {statusCode} Server Error";
                    serverAddress = $"Server: {ipaWebServer}:{iWebServerPort}";
                    sDate = $"Date: {DateTime.Now.ToString("r")}";

                    Log(logFileName, $"[Server Started] - {statusCode.ToString()}");
                    response = firstLineResponse + "\r\n" + serverAddress + "\r\n" + sDate + "\r\n";
                    break;
            }

            return response;
        }


        /*	-- METHOD
        *	Name	:	HandleIncomingRequests
        *	Purpose	:	To handle incoming requests, check whether the incoming request is valid. If the request is valid, not only the returned code, but the path to the 
        *	                requested resource, its content-type, and the status code also will be returned
        *	Inputs	:	string sData : the GET request
        *	            string resourceRequested : The resource to be requested
        *	            string sContentType : The content type of The resource to be requested
        *	            int statusCode : the status code
        *	Return	:	  0, if it is success. In addition, the resourceRequested, the sContentType, the status code will be returned
        *                 1, if it is fail. In addition, the resourceRequested, the sContentType, the status code will be returned
        *                 2, if exception happened. In addition, the resourceRequested, the sContentType, the status code will be returned
        */
        public int HandleIncomingRequests(string sData, out string resourceRequested, out string sContentType, out int statusCode) 
        {
            string[] requestDelim = {"\r\n"};
            char singleLineDelim = ' ';
            string[] arrRequest = null;
            string[] arrGetRequest = null;
            string sGETResources = null;
            string sGETProtocol = null;
            string sResourceFileExtension = null;
            sContentType = null;
            int retCode = 0;

            // check whether the server is started
            if( bServerStarted == false)
            {
                retCode = 1;
                resourceRequested = "Not Found";
                statusCode = (int)eStatusCode.Fail;
                return retCode;
            }
            else
            {
                arrRequest = sData.Split(requestDelim, StringSplitOptions.None); // split the request into elments

                /*
                 * Split the first GET request message into elments
                 */
                arrGetRequest = arrRequest[0].Split(singleLineDelim);

                /* arrGetRequest elements at index:
                 * 0: The HTTP verb which is GET
                 * 1: the resource that is being requested
                 * 2: the HTTP Protocol
                 */
                sGETResources = arrGetRequest[1];
                sGETProtocol = arrGetRequest[2];

                // Record the request
                Log(logFileName, $"[Server Started] - {arrGetRequest[0]} {arrGetRequest[1]}");

                // check whether the requested resource Path is valid (i.e. start with /)
                
                if (sData.IndexOf("GET") == -1 || sData.IndexOf("GET") != 0) // check whether the request is GET request
                {
                    retCode = 1;
                    resourceRequested = "Not Found";
                    statusCode = (int)eStatusCode.MethodNotAccepted;
                    return retCode;
                }
                else if (sGETProtocol != sHTTPProtocolVersion) // check whether the HTTP protocol version is valid
                {
                    retCode = 1;
                    resourceRequested = "Not Found";
                    statusCode = (int)eStatusCode.HTTPVersionNotSupported;
                    return retCode;
                }
                else if (sGETResources.IndexOf("/") != 0)
                {
                    retCode = 1;
                    resourceRequested = "Not Found";
                    statusCode = (int)eStatusCode.BadRequest;
                    return retCode;
                }
                else
                {

                    sGETResources = Path.Combine(webRoot, Path.GetFileName(sGETResources)); // combine web root for the requested resources
                    resourceRequested = sGETResources;
                    sResourceFileExtension = Path.GetExtension(sGETResources).ToLower();

                    // check whether the file extension is valid
                    switch (sResourceFileExtension)
                    {
                        case ".txt":
                            sContentType = "text/plain";
                            break;
                        case ".html":
                            sContentType = "text/html";
                            break;
                        case ".htm":
                            sContentType = "text/html";
                            break;
                        case ".jpg":
                            sContentType = "image/jpeg";
                            break;
                        case ".jpeg":
                            sContentType = "image/jpeg";
                            break;
                        case ".gif":
                            sContentType = "image/gif";
                            break;
                        default:
                            retCode = 1;
                            resourceRequested = "Not Found";
                            statusCode = (int)eStatusCode.UnsupportedMediaType;
                            return retCode;
                    }


                    // check whether the resource is existed
                    if (File.Exists(sGETResources) == false)
                    {
                        retCode = 2;
                        statusCode = (int)eStatusCode.NotFound;
                        return retCode;
                    }

                    

                    statusCode = (int)eStatusCode.Success;
                    return retCode;
                }
 
            }
            
        }


        /*	-- METHOD
        *	Name	:	GenerateResponse
        *	Purpose	:	To generate response for text or html file types. If it is success,
        *	                not only the code that indicate whether it is successsfull or not, the sResourceToSentBack, and the status 
        *	                    code also will be returned.
        *	Inputs	:	string sRequestedResource : The requested resource
        *	            string sContentType : The content type of resource
        *	            int statusCode : the status code
        *	            string sResourceToSentBack : The resource to be sent back
        *	Return	:	  0, if it is success. In addition, the sResourceToSentBack, the statusCode will be returned
        *                 1, if it is fail. In addition, the sResourceToSentBack, the statusCode will be returned
        *                 2, if exception happened. In addition, the sResourceToSentBack, the statusCode will be returned
        */
        public int GenerateResponse(string sRequestedResource, string sContentType, out string sResourceToSentBack, out int statusCode)
        {
            
            sResourceToSentBack = null;
            int retCode = 0;
            statusCode = (int)eStatusCode.Success;

            // Check for content-type
            if(sContentType == "text/plain" || sContentType == "text/html")
            {
                try
                {
                    // open the requested resource and read
                    sResourceToSentBack = File.ReadAllText(sRequestedResource);
                    statusCode = (int)eStatusCode.Success;
                }
                catch (ArgumentException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = "Not found";
                    return retCode;
                }
                catch (PathTooLongException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = "Not found";
                    return retCode;
                }
                catch (DirectoryNotFoundException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = "Not found";
                    return retCode;
                }
                catch (UnauthorizedAccessException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = "Not found";
                    return retCode;
                }

                catch (NotSupportedException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = "Not found";
                    return retCode;
                }
                catch (SecurityException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = "Not found";
                    return retCode;
                }
                catch (IOException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = "Not found";
                    return retCode;
                }
            }
            else 
            {
                retCode = 2;
                statusCode = (int)eStatusCode.NotFound;
                sResourceToSentBack = "Not found";
            }
            

            return retCode;
        }


        /*	-- METHOD
        *	Name	:	GenerateResponse
        *	Purpose	:	An overloaded method, the purpose of this method is to generate response for image file types. If it is success,
        *	                not only the code that indicate whether it is successsfull or not, the sResourceToSentBack, and the status 
        *	                    code also will be returned
        *	Inputs	:	string sRequestedResource : The requested resource
        *	            string sContentType : The content type of the resource
        *	            byte[] sResourceToSentBack : The resource to be sent back
        *	            int statusCode : the status code
        *	Return	:	  0, if it is success. In addition, the sResourceToSentBack, the statusCode will be returned
        *                 1, if it is fail. In addition, the sResourceToSentBack, the statusCode will be returned
        *                 2, if exception happened. In addition, the sResourceToSentBack, the statusCode will be returned
        */
        public int GenerateResponse(string sRequestedResource, string sContentType, out byte[] sResourceToSentBack, out int statusCode)
        {
            int retCode = 0;

            // check whether the content-type is image file
            if(sContentType == "image/jpeg" || sContentType == "image/gif")
            {
                
                try
                {
                    // open the requested resource and read
                    sResourceToSentBack = File.ReadAllBytes(sRequestedResource);
                    statusCode = (int)eStatusCode.Success;
                }
                catch (ArgumentException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = null;
                    return retCode;
                }
                catch (PathTooLongException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = null;
                    return retCode;
                }
                catch (DirectoryNotFoundException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = null;
                    return retCode;
                }
                catch (UnauthorizedAccessException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = null;
                    return retCode;
                }

                catch (NotSupportedException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = null;
                    return retCode;
                }
                catch (SecurityException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = null;
                    return retCode;
                }
                catch (IOException)
                {
                    retCode = 2;
                    statusCode = (int)eStatusCode.NotFound;
                    sResourceToSentBack = null;
                    return retCode;
                }
            }
            else
            {
                retCode = 2;
                statusCode = (int)eStatusCode.NotFound;
                sResourceToSentBack = null;
            }

            return retCode;
        }

        /*	-- METHOD
        *	Name	:	Log
        *	Purpose	:	To log incoming requests, responses, and exceptions when server started to a log file
        *	Inputs	:	string logFileName : the log file name
        *	            string message : the message to be logged
        *	Return	:	Not return anything
        */

        public static void Log(string logFileName, string message)
        {
            StreamWriter logFiles = null;
            string localDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                // check whether the log file is existed when server started for the first time
                if (bLogFileIsExisted == true)
                {
                    logFiles = new StreamWriter($@"{exeDir}\{logFileName}.log", false);
                    bLogFileIsExisted = false;
                    logFiles.Close();
                }

                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\{logFileName}.log", true);
                logFiles.WriteLine("{0} {1}", localDate, message);
                logFiles.Close();
            }
            catch (FileNotFoundException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (DirectoryNotFoundException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (DriveNotFoundException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (PathTooLongException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (OperationCanceledException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (UnauthorizedAccessException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (ArgumentException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (NotSupportedException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (SecurityException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            catch (IOException e)
            {
                // Open log file and write to it
                logFiles = new StreamWriter($@"{exeDir}\myOwnWebServer.log", true);
                logFiles.WriteLine("{0} - {1}", localDate, e.Message);
                logFiles.Close();
            }
            finally
            {
                if (logFiles != null) // check whether the file is closed
                {
                    logFiles.Close();
                }
            }
        }


    }
}
