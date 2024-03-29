using System.Net;
using System.Net.Sockets;
using System.Text;
using CommandLine;
using Newtonsoft.Json;
using DetachLibrary;

namespace DetachClient;

public static class Program
{
    [Verb("launch", HelpText = "Launch a new process")]
    private class LaunchProcess
    {
        [
            Option(
                shortName:'e',
                longName:"executable",
                Required = true,
                HelpText = "The target executable")
        ] public string? Executable { get; set; }
        
        [
            Option(
                shortName: 'd',
                longName: "workingDirectory",
                Required = false,
                HelpText = "Working directory the underlying process should use")
        ] public string? WorkingDirectory { get; set; }
        
        [
            Option(
                shortName:'a',
                longName:"arguments",
                Required = false,
                HelpText = "Arguments required by the underlying process")
        ] public IEnumerable<string>? Arguments { get; set; }
    }

    [Verb("close", HelpText = "Close a running process")]
    private class CloseProcess
    {
        [
            Option(
                shortName:'i',
                longName:"processId",
                Required = true,
                HelpText = "The integer id assigned to the process")
        ] public uint? Id { get; set; }
    }

    [Verb("fetch", HelpText = "Fetch info on all processes")]
    private class FetchInfo
    {
        
    }

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<LaunchProcess, CloseProcess, FetchInfo>(args)
            .WithParsed<LaunchProcess>(Launch)
            .WithParsed<CloseProcess>(Close)
            .WithParsed<FetchInfo>(Fetch);
    }

    private static void Launch(LaunchProcess options)
    {
        Console.WriteLine("Sending Launch Request...");
        _ = options.Executable ?? throw new Exception();
        var cwd = options.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var message = $"1<|s|>{options.Executable}<|s|>{cwd}";
        if (options.Arguments != null)
        {
            foreach (var arg in options.Arguments)
            {
                message += "<|s|>";
                message += arg;
            }
        }
        else
        {
            message += "<|s|>";
        }
        Connect(1, message);
    }

    private static void Close(CloseProcess options)
    {
        Console.WriteLine("Sending Cancel Request...");
        _ = options.Id ?? throw new Exception();
        var message = $"2<|s|>{options.Id}";
        Connect(2, message);
    }

    private static void Fetch(FetchInfo options)
    {
        Console.WriteLine("Fetching process info...");
        Connect(3,"3");
    }

    private static void Connect(int command, string message)
    {
        // Setup LocalHost Address and Socket Object
        var ipHost = Dns.GetHostEntry(Dns.GetHostName());
        var ipAddress = ipHost.AddressList[0];
        var ipEndPoint = new IPEndPoint(ipAddress, 2046);
        var sender = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        
        // Connect to Socket
        try
        {
            sender.Connect(ipEndPoint);
        }
        catch (SocketException e)
        {
            Console.WriteLine("Could Not Connect to Service: Exiting...");
            Environment.Exit(1);
        }
        
        // Send Message to Server
        message += "<|EOM|>";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _ = sender.Send(messageBytes, SocketFlags.None);
        Console.WriteLine("Sent Message to Service, waiting for response");
        
        // Receive Response from Server
        var buffer = new byte[1024];
        var received = sender.Receive(buffer, SocketFlags.None);
        var response = Encoding.UTF8.GetString(buffer, 0, received);
        while (!response.Contains("<|EOM|>"))
        {
            received = sender.Receive(buffer, SocketFlags.None);
            response += Encoding.UTF8.GetString(buffer, 0, received);
        }

        // Process the Response and Print to User
        response = response[..^7];
        switch (command)
        {
            case 1:
                try
                {
                    var launched = JsonConvert.DeserializeObject<RunningInfo>(response);
                    Console.WriteLine("Successfully Launched Process:");
                    DisplayProcess(launched);
                }
                catch (JsonSerializationException e)
                {
                    Console.WriteLine("Did not recognize info sent by server");
                    Console.WriteLine(response);
                }
                break;
            case 2:
                if (response.Length > 0)
                {
                    try
                    {
                        var canceled = JsonConvert.DeserializeObject<RunningInfo>(response);
                       Console.WriteLine("Successfully Canceled Process");
                       DisplayProcess(canceled);
                    }
                    catch (JsonSerializationException e)
                    {
                        Console.WriteLine("Did not recognize info sent by server");
                        Console.WriteLine(response);
                    }
                }
                else
                {
                    Console.WriteLine("Service could not identify process to cancel");
                }
                break;
            case 3:
                var runningInfo = response.Split("<|s|>");
                Console.WriteLine($"  ID{new string(' ', 23)}StartTime{new string(' ', 23)}StopTime");
                foreach (var info in runningInfo[..^1])
                {
                    try
                    {
                        var temp = JsonConvert.DeserializeObject<RunningInfo>(info);
                        Console.WriteLine($"{temp.Id, 3}{temp.StartTime, 33}{temp.StopTime, 32}");
                    }
                    catch (JsonSerializationException e)
                    {
                        Console.WriteLine("Did not recognize info sent by server");
                        Console.WriteLine(info);
                    }
                }
                break;
                
        }
        
        // Close the Socket so the Program can Terminate
        sender.Shutdown(SocketShutdown.Both);
        sender.Close();
    }

    private static void DisplayProcess(RunningInfo runInfo)
    {
        Console.WriteLine($"ID: {runInfo.Id}");
        Console.WriteLine($"Start Time: {runInfo.StartTime}");
        Console.WriteLine($"Stop Time: {runInfo.StopTime}");
    }
}