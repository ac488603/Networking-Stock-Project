Operating System: Windows 10 
Language: C# 
The programs are able to run with any of these two methods:
I) On command line with compiler Mono (http://www.mono-project.com/download/)
	compile: csc server.cs 
	execute: server.exe 
	
	compile: csc synnclient.cs 
	execute: syncclient.exe 
II) Visual Studio 2017 Community Edition 
	Since we did not supply the project solution, in order to run server program in Visual Studio:
	1. Create New Project (Console App C#)
	2. Replace content in file "Progam.cs" with content in file "server.cs"
	3. Place Data.txt in Debug folder. 
	4. Build Solution and Start Debugging to run progam.
	
	In order to run client program in Visual Studio:
	1. Create New Project (Console App C#)
	2. Replace content in file "Progam.cs" with content in file "syncclient.cs"
	3. Build Solution and Start Debugging to run progam

Note:
You need to change the IP Address in the file "syncclient.cs" at line 228. 
IP Address must be IPV4 address(Internetwork) 

If you want to run 2+ clients. Simply execute the syncclient.exe on two different windows of Mono. 

The two connections can be verified on windows command line (of the server or client computer) with: 
netstat -np TCP | find "17000"

