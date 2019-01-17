using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

//Stock class for holding information about a Stock 
public class Stock
{
    public string name;
    public int numShares;
    public double currentPrice; //get from server 
    public double previousPrice;

    public Stock()
    {
        name = "";
        numShares = 0;
        previousPrice = 0.00;
        currentPrice = 0.00;
    }
}

//Globals class for holding "global" variables 
public static class Globals
{
    public const double Z = 80;                 //buying rate, Z%
    public const double X = 1;                  //if price increased by X%, then sell
    public const double Y = -2;                 //if price decreased by Y%, then sell 
 
    public const int maxAttemptedTransactions = 2100;    //set limitations for while loop in connecting with server for transactions

    public static double totalSold = 0.00;      //variables for calculating overall yield
    public static double totalSpent = 0.00;

    public static double balance;              //receive from server 
    public static double yield = 0.00;         //yield = ((totalsold-totalspent)/totalspent) * 100;

    public static bool isFirst = true;         //to only intialize the StockList once, if false then only update the currentPrice 
    public static bool isBuy = true;           //to alternate from buy and sell transaction 
    public static Dictionary<string, Stock> StockList = new Dictionary<string, Stock>();    //string is the stock's name
    public static bool isFinish = false;       //to stop the the other thread from printing balance & yield

    public static int totalBuys = 0;            //just to keep track of overall transactions 
    public static int totalSales = 0;

    public static Random random = new Random(); //guarantee seeding is different by having only 1 random
    public static Mutex mut = new Mutex();      //set mutex on send() and receive() since reporting thread also use them at the same time as the main thread
}

//synchronous socket 
public class Client
{
    //check if buying random Stock. 
    //if buying then return transaction message
    private static String Buy()
    {
        string command = "";
        string stockName;
        int stockNumber;
        int numShares;
        int numOfFavStocks;
        double pricePerShare;
        int randomNumber = Globals.random.Next(0, 101); //excludes 101
        if (randomNumber <= (Globals.Z))
        {
            //choose random stock from the StockList
            numOfFavStocks = Globals.StockList.Count;
            stockNumber = Globals.random.Next(0, numOfFavStocks);
            if ((stockNumber <= numOfFavStocks - 1) && (stockNumber >= 0))
            {
                stockName = Globals.StockList.Values.ElementAt(stockNumber).name;
                numShares = Globals.random.Next(1, 101);  //get random number of shares to buy 
                pricePerShare = Globals.StockList.Values.ElementAt(stockNumber).currentPrice;
                string p = string.Format("{0:N2}", pricePerShare);
                command = "BUY " + stockName + " " + numShares + " " + p;
            }
            else //just error checking
            {
                Console.WriteLine("error, got a random number more than numOfFavStocks-1 or less than 0");
                Console.WriteLine("numOfFavStocks = " + numOfFavStocks);
                Console.WriteLine("random number = " + stockNumber);
            }
        }
        return command; //command remains blank if not buying 
    }

    //check if selling random Stock. 
    //if selling then return transaction message
    private static string Sell()
    {
        string command = "";
        string stockName;
        int stockNumber;
        int numShares;
        double pricePerShare;
        double previousPrice;
        double percentage;
        List<string> stocksWithShares = new List<string>();
        int size = Globals.StockList.Count;
        if (size != 0)//if stock list is not empty
        {
            //get stocks that have shares > 0
            //put those stocks into a List
            //choose random stock from stocksWithShares List
            for (int i = 0; i < size; i++)
            {
                if (Globals.StockList.Values.ElementAt(i).numShares > 0)
                {
                    stocksWithShares.Add(Globals.StockList.Values.ElementAt(i).name);
                }
            }

            size = stocksWithShares.Count;
            if (size != 0)//if stockWithShares is not empty (so there exists Stocks with shares)
            {
                stockNumber = Globals.random.Next(0, size); //returned number will only be from 0 to size-1
                string n = stocksWithShares[stockNumber];

                numShares = Globals.StockList[n].numShares;
                pricePerShare = Globals.StockList[n].currentPrice;
                previousPrice = Globals.StockList[n].previousPrice;
                percentage = ((pricePerShare - previousPrice) / pricePerShare) * 100;
                stockName = Globals.StockList[n].name;

                string p = string.Format("{0:N2}", pricePerShare);

                if (percentage >= (Globals.X) || percentage <= (Globals.Y))//sell if fufills X or Y
                {
                    command = "SELL " + stockName + " " + numShares + " " + p;
                }
            }//end if stocksWithShares is not empty
        }//end if StockList is not empty
        return command;//command remains blank if not selling 
    }//end Sell()

    //create Transaction message
    private static String Transaction()
    {
        string command = "";
        if (Globals.isBuy)
        {
            command = Buy();
            Globals.isBuy = false;
        }
        else
        {
            command = Sell();
            Globals.isBuy = true;
        }
        return command;
    }

    //update the Stock's info after a successful transactions 
    public static void UpdateStockInfo(string transaction)
    {
        //Console.WriteLine(transaction);    //debugging purposes to show which transactions were successful
        string[] t = transaction.Split(' '); //length should be 4 (buy-name-numshares-price)
        string price = t[3];
        double p = Convert.ToDouble(price);
        string name = t[1];
        string numShares = t[2];
        double total_price = p * Convert.ToDouble(numShares);

        if (t[0] == "BUY")
        {
            Globals.totalSpent = Globals.totalSpent + total_price;
            Globals.yield = ((Globals.totalSold - Globals.totalSpent) / Globals.totalSpent) * 100;
            Globals.StockList[name].previousPrice = p;
            Globals.StockList[name].numShares = (Globals.StockList[name].numShares) + Convert.ToInt32(numShares);
            Globals.totalBuys++;
        }
        else //sell success 
        {
            Globals.totalSold = Globals.totalSold + total_price;
            Globals.yield = ((Globals.totalSold - Globals.totalSpent) / Globals.totalSpent) * 100;
            Globals.StockList[name].previousPrice = p;
            Globals.StockList[name].numShares = (Globals.StockList[name].numShares) - Convert.ToInt32(numShares); //numShares should be zero since the program sells all 
            Globals.totalSales++;
        }
    }

    //Initialize StockList with information about the Stocks
    //or update the currentPrice of the Stocks
    public static void UpdateStocks(string transaction)
    {
        string[] bb = transaction.Split(' ');
        for (int j = 0; j < bb.Length - 1; j += 2)
        {
            if (bb[j] != "<EOF>")
            {
                string stockName = bb[j];
                string price = bb[j + 1].ToString();
                double stockPrice = Convert.ToDouble(price);

                if (Globals.isFirst)
                { //if first time asking for stock list..then add to StockList
                    Stock newStock = new Stock
                    {
                        name = stockName,
                        currentPrice = stockPrice,
                        previousPrice = 0.00,
                        numShares = 0
                    };
                    Globals.StockList.Add(newStock.name, newStock);
                }
                else//not first time and StockList was already created 
                {
                    //update stock's new price in StockList dictionary 
                    Globals.StockList[stockName].currentPrice = stockPrice;
                }
            }//end if (check if string was not <EOF>)
        }//end for loop
    }

	//start connecting to server and request for stock information and for transactions 
    public static void StartClient()
    {
        // Data buffer for incoming data.
        byte[] bytes = new byte[1024];
        string buyOrSellTransaction = "";
        // Connect to a remote device.
        try
        {
            // Establish the remote endpoint for the socket.
            IPAddress ipAddress = IPAddress.Parse("192.168.1.236");
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 17000);

            // Create a TCP/IP socket.
            Socket clientSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Connect the socket to the remote endpoint. Catch any errors.
            try
            {
                clientSocket.Connect(remoteEP);
                Console.WriteLine("Socket connected to {0}", clientSocket.RemoteEndPoint.ToString());

                //start Thread for reporting balance and yield 
                Thread reportThread = new Thread(() => Report(clientSocket));
                reportThread.Start();

                for (int i = 0; i < Globals.maxAttemptedTransactions; i++)//ask for stocks Info and attempt transactions
                {
                    //Console.WriteLine("------" + i); //just to keep track of the number of loops
                    if (i == 1)//StockList was already created when i = 0, so don't need to add more
                    {
                        Globals.isFirst = false;
                    }
                    // Encode the data string into a byte array.
                    StringBuilder sendmsg = new StringBuilder();
                    sendmsg.Append("Give me the Stock List <EOF>");

                    byte[] msg = Encoding.ASCII.GetBytes(sendmsg.ToString());

                    // Send the data through the socket.
                    Globals.mut.WaitOne();
                    int bytesSent = clientSocket.Send(msg);

                    // Receive the response from the remote device.
                    int bytesRec = clientSocket.Receive(bytes);
                    Globals.mut.ReleaseMutex();
                    string serverStockList = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                    //convert response to appropriate Stocks in StockList...
                    UpdateStocks(serverStockList);

                    //get transaction
                    buyOrSellTransaction = Transaction();

                    if (buyOrSellTransaction != "")           //send Transaction request to server 
                    {
                        StringBuilder trans = new StringBuilder();
                        trans.Append(buyOrSellTransaction);
                        trans.Append(" <EOF>");
                        byte[] msg2 = Encoding.ASCII.GetBytes(trans.ToString());
                        Globals.mut.WaitOne();                 
                        bytesSent = clientSocket.Send(msg2);
                        Array.Clear(bytes, 0, bytes.Length);  
                        bytesRec = clientSocket.Receive(bytes);
                        Globals.mut.ReleaseMutex();
                        string serverSuccessOrNotResponse = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (serverSuccessOrNotResponse == "Success<EOF>")
                        {
                            UpdateStockInfo(buyOrSellTransaction); //update Stock info 
                            Thread.Sleep(1000); //sleep when transaction is a success
                        } 
                    }//end if (of sending transaction)
                    Array.Clear(bytes, 0, bytes.Length); 
                }//end for loop 

                // Release the socket.
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();

            }
            catch (ArgumentNullException ane)
            {
                Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                Console.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    //reports balance and yield every 10 seconds 
    private static void Report(Socket sender)
    {
        string yield;
        string balance;
        string balanceFromServer;
        byte[] bytes = new byte[1024];
        int bytesSent;
        int bytesRec;
        while (Globals.isFinish == false)
        {
            //request balance from server 
            StringBuilder sendmsg = new StringBuilder();
            sendmsg.Append("BALANCE<EOF>");
            byte[] msg = Encoding.ASCII.GetBytes(sendmsg.ToString());
            // Send the data through the socket.
            Globals.mut.WaitOne();
            bytesSent = sender.Send(msg);
            // Receive the response from the remote device.
            bytesRec = sender.Receive(bytes);
            Globals.mut.ReleaseMutex();
            balanceFromServer = Encoding.ASCII.GetString(bytes, 0, bytesRec);
            Globals.balance = Convert.ToDouble(balanceFromServer);
            balance = string.Format("{0:N2}", Globals.balance);
            yield = string.Format("{0:N2}%", Globals.yield);
            Console.WriteLine("Balance: {0}, Yield: {1}", balance, yield);
            Thread.Sleep(10000);
        }
    }

    public static int Main()
    {
        StartClient();           //start connection with server and continue sending and receiving messages to and from server until 2100 maxTransaction is reach.
        Globals.isFinish = true; //stop reporting thread 

        //---just for checking overall transactions 
        /*
        Console.WriteLine("******************************Final Check:");
        Console.WriteLine("Total Number of Buys: " + Globals.totalBuys);
        Console.WriteLine("Total Number of Sales: " + Globals.totalSales);
        Console.WriteLine("Yield: " + string.Format("{0:N2}%", Globals.yield));
        Console.WriteLine("Balance (not the latest since required asking server): " + string.Format("{0:N2}", Globals.balance));
        Console.WriteLine("Total sold: " + Globals.totalSold);
        Console.WriteLine("Total spent: " + Globals.totalSpent);
        */

        Console.WriteLine("Program is finish.");
        Console.ReadLine(); //necessary for Visual Studio so that the program does not close immediately after finishing. 
        return 0;
    }
}
