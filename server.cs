using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections.Generic;

// State object for reading client data asynchronously
public class StateObject{
    public Socket workSocket = null; 				// Client socket.
    public const int BufferSize = 1024; 			// Size of receive buffer.
    public byte[] buffer = new byte[BufferSize];  	// Receive buffer.
    public StringBuilder sb = new StringBuilder();	// Received data string.
}

public class Stock
{
    public string name {get;set;}
    public int numShares {get;set;}
    public double previousPrice {get;set;}
    public List<double> priceList;      
    public Stock()
    {
        name = "";
        previousPrice = 0.00;
        priceList = new List<double>();
    }
}

public class Data
{
	public string name {get;set;}
	public int numShares {get;set;}
    public Data() { name = String.Empty; numShares = 0; }
}

public class AsynchronousSocketListener {
    public static ManualResetEvent allDone = new ManualResetEvent(false);					  //use as a signal for main thread to continue 
    private static Dictionary<EndPoint,double> balances =  new Dictionary<EndPoint,double>(); //store each clients' balance 
    private static Dictionary<string, Stock> stockInfo = new Dictionary<string, Stock>();	  //store the stocks' information
    private static Dictionary<string, Data> userData = new Dictionary<string,Data>();		  //store all the clients' number of shares or each Stock they have
    public static Mutex mut = new Mutex();													 //use to safely update balances and/or userData and/or stockInfo

	//get IP4 address of computer 
    public static string GetLocalIPAddress() {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) { return ip.ToString();}
        }
        return "";
      }
	  
    public AsynchronousSocketListener() {
    }

	//read data from Data.txt and initialize stockInfo Dictionary. 
    public static void LoadData(string fileName) {
      try {
          string line;
          StreamReader sr =  new StreamReader(fileName); //open file
          line = sr.ReadLine();

          //Continue to read until you reach end of file
          while (line != null)
          {
          //write the lie to console window
          Stock temp = new Stock();
          temp.name = line.Trim(); //init name of stock, trim removes whitespace from begining and end of string

          line = sr.ReadLine();
          String[] prices = line.Split(','); // store values in pricelist

          foreach(string price in prices)
          {
            temp.priceList.Add(Convert.ToDouble(price));
          }

          stockInfo.Add(temp.name,temp);

          //Read the next line
          Console.WriteLine("processed {0}", temp.name);
          line = sr.ReadLine();
          }
          sr.Close(); //close file
      }
      catch(Exception e) //catch errors opening file
      {
        Console.WriteLine("Exception: {0}", e.Message);
      }
    }

    public static void StartListening() {
        // Establish the local endpoint for the socket.
        IPAddress ipAddress = IPAddress.Parse(GetLocalIPAddress());
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 17000);
        Console.WriteLine("Starting server at {0}" , localEndPoint.ToString());

        // Create a TCP/IP socket.
        Socket listener = new Socket(ipAddress.AddressFamily,
            SocketType.Stream, ProtocolType.Tcp );

        // Bind the socket to the local endpoint and listen for incoming connections.
        try {
            listener.Bind(localEndPoint);
            listener.Listen(100); //100 = the maximum length of the pending connections queue.

            while (true) {
                // Set the event to nonsignaled state.
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.
                Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener );

                // Wait until a connection is made before continuing.
                allDone.WaitOne();
            }

        } catch (Exception e) {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public static void AcceptCallback(IAsyncResult ar) {
        // Signal the main thread to continue.
        allDone.Set();

        // Get the socket that handles the client request.
        Socket listener = (Socket) ar.AsyncState;
        Socket handler = listener.EndAccept(ar);


        if (!balances.ContainsKey(handler.RemoteEndPoint)){ // add connection to dictionary if it doesnot already exist
                  mut.WaitOne();
                  balances.Add(handler.RemoteEndPoint,100000000000);
                  Console.WriteLine(balances.Count);
                  mut.ReleaseMutex();
        }

        // Create the state object.
        StateObject state = new StateObject(); // one state used for single connection
        state.workSocket = handler;
        handler.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }

    public static void ReadCallback(IAsyncResult ar) {
        String content = String.Empty;

        // Retrieve the state object and the handler socket
        // from the asynchronous state object.
        StateObject state = (StateObject) ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket.
        int bytesRead = handler.EndReceive(ar);

        if (bytesRead > 0) {
            // There  might be more data, so store the data received so far.
            state.sb.Append(Encoding.ASCII.GetString(
                state.buffer,0,bytesRead));

            // Check for end-of-file tag. If it is not there, read
            // more data.
            content = state.sb.ToString();
            if (content.IndexOf("Close") > -1 && content.IndexOf("<EOF>") > -1) {
                // All the data has been read from the
                // client. Display it on the console.
                Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                    content.Length, content );
                // Echo the data back to the client.
                SendClose(handler, content);

            }else if(content.IndexOf("<EOF>") > -1){

                if (content.IndexOf("BALANCE") > -1)
                { // retrieve balance
                    mut.WaitOne();
                    double balance = balances[handler.RemoteEndPoint];
                    content = balance.ToString();
                    mut.ReleaseMutex();
                }
                else if (content.IndexOf("Give me") > -1)
                {
                    content = currentPrices();
                }
                else
                {
                    Random tempran = new Random();
                    int num = tempran.Next(1,101);
                   if (num > 10){ // 10 percent failure 
                        
                    string[] message = content.Split(' ');
                    double totalPrice = Convert.ToDouble(message[2]) * Convert.ToDouble(message[3]);
                    StringBuilder temp = new StringBuilder();
                    temp.Append(handler.RemoteEndPoint); temp.Append(message[1]);
                    int numberofshares = Int32.Parse(message[2]);
                    if (message[0] == "BUY")
                    {
                        balances[handler.RemoteEndPoint] -= totalPrice;
                        if (!userData.ContainsKey(temp.ToString()))
                        {
                            userData.Add(temp.ToString(), new Data());
                            userData[temp.ToString()].name = message[1];
                        }
                        userData[temp.ToString()].numShares += numberofshares;
                        stockInfo[message[1]].priceList.RemoveAt(0); //update pricelist
                        content = "Success<EOF>";
                    }
                    else //sell
                    {
                        if (!userData.ContainsKey(temp.ToString()) || numberofshares > userData[temp.ToString()].numShares)
                        {
                            content = "Failure<EOF>";
                        }
                        else
                        {
                            mut.WaitOne();
                            balances[handler.RemoteEndPoint] += totalPrice;
                            userData[temp.ToString()].numShares -= numberofshares;
                            stockInfo[message[1]].priceList.RemoveAt(0);
                            mut.ReleaseMutex();
                            content = "Success<EOF>";
                        }
                    }//sell end
                }// failure end
               else { content = "10percentFAIL<EOF>"; }
               }//end else (received transaction message) 
              Send(handler,content);
              state.sb.Clear();  //  empty string builder,  new message comming
			  
			  //get new message from client 
              handler.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0,
                  new AsyncCallback(ReadCallback), state);
            }
            else {
                //The entire message was not receive (needs to end in "<EOF>"). So, receive more of the message.
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
            }
        }
    }

	//create stock price list to send to client 
	//example: "APPL 20.20 MSFT 32.30"
    private static string currentPrices()
    {
      StringBuilder currentStockPrices = new StringBuilder();

      foreach( var key in stockInfo.Keys) // build price string
      {
      currentStockPrices.Append(key + " "); //get key
      currentStockPrices.Append(stockInfo[key].priceList[0]); //retrieve current price
      currentStockPrices.Append(" ");
      }
      return currentStockPrices.ToString();
    }
	
    private static void Send(Socket handler, String data) {
        // Convert the string data to byte data using ASCII encoding.
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.
        Console.WriteLine("Sending Data to {0}", handler.RemoteEndPoint);
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }

    private static void SendCallback(IAsyncResult ar) {
        try {
            // Retrieve the socket from the state object.
            Socket handler = (Socket) ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client./", bytesSent);
        } catch (Exception e) {
            Console.WriteLine(e.ToString());
        }
    }

    private static void SendClose(Socket handler, String data) {
        // Convert the string data to byte data using ASCII encoding.
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallbackClose), handler);
    }

    private static void SendCallbackClose(IAsyncResult ar) {
        try {
            // Retrieve the socket from the state object.
            Socket handler = (Socket) ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

        } catch (Exception e) {
            Console.WriteLine(e.ToString());
        }
    }

    public static int Main(String[] args) {
        LoadData("Data.txt");
        StartListening();
        return 0;
    }
}
