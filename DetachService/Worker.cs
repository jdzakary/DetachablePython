using System.Net;
using System.Net.Sockets;
using System.Text;
using Medallion.Shell;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using DetachLibrary;

namespace DetachService;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private uint _procId;
    private readonly Dictionary<uint, RunningInfo> _running;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _procId = 0;
        _running = new Dictionary<uint, RunningInfo>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Setup LocalHost Address and Socket Object
        var ipHost = await Dns.GetHostEntryAsync(Dns.GetHostName(), stoppingToken);
        var ipAddress = ipHost.AddressList[0];
        var ipEndPoint = new IPEndPoint(ipAddress, 2046);
        var listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        
        // Attempt to Bind Socket Object
        try
        {
            listener.Bind(ipEndPoint);
            listener.Listen(10);
        }
        catch (Exception e)
        {
            _logger.LogError("Error when Binding the Socket");
            _logger.LogError(e.Message);
            throw;
        }
        
        // Listen and Process Connections until Cancellation is requested
        while (!stoppingToken.IsCancellationRequested)
        {
            var handler = await listener.AcceptAsync(stoppingToken);
            var buffer = new byte[1024];
            var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
            var message = Encoding.UTF8.GetString(buffer, 0, received);
            while (!message.Contains("<|EOM|>"))
            {
                received = await handler.ReceiveAsync(buffer, SocketFlags.None);
                message += Encoding.UTF8.GetString(buffer, 0, received);
            }
            message = message[..^7];
            var response = "";
            
            // Decide Which Action to Execute
            var args = message.Split("<|s|>");
            if (!int.TryParse(args[0], out var command))
            {
                command = 0;
            }
            
            // Execute the Action
            switch (command)
            {
                case 0:
                    _logger.LogInformation("Did not Recognize Request... Aborting Task");
                    break;
                case 1:
                    _logger.LogInformation("Requested New Process be Launched");
                    var launchTime = DateTime.UtcNow;
                    var source = new CancellationTokenSource();
                    var outputDir = $"{AppDomain.CurrentDomain.BaseDirectory}\\logs\\processes";
                    var outputName = $"{launchTime:yyyy-MM-dd_H-mm}_{_procId}";
                    var runInfo = new RunningInfo(_procId, launchTime, source);
                    _running[_procId] = runInfo;
                    RunCommand(
                        executable:args[1],
                        scriptArgs: args[3..],
                        workingDirectory: args[2],
                        outputDir: outputDir,
                        outputName: outputName,
                        token: source.Token,
                        processId: _procId);
                    response += JsonConvert.SerializeObject(runInfo);
                    _procId++;
                    break;
                case 2:
                    _logger.LogInformation("Requested Process be Cancelled");
                    if (uint.TryParse(args[1], out var id))
                    {
                        try
                        {
                            _running[id].Source.Cancel();
                            _logger.LogInformation("Successfully Canceled Process");
                            await Task.Delay(500);
                            response += JsonConvert.SerializeObject(_running[id]);
                        }
                        catch (KeyNotFoundException e)
                        {
                            _logger.LogWarning("Process ID not found");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Error Converting String ID to uint ID");
                    }
                    break;
                case 3:
                    _logger.LogInformation("Requested Info on All Processes");
                    foreach (var kInfo in _running)
                    {
                        response += JsonConvert.SerializeObject(kInfo.Value);
                        response += "<|s|>";
                    }
                    break;
            }
            
            // Send Response to Client
            response += "<|EOM|>";
            await handler.SendAsync(Encoding.UTF8.GetBytes(response), 0);
            _logger.LogInformation("Sent Response to Client");
            handler.Shutdown(SocketShutdown.Both);
        }
        
        // Cleanup Actions
        foreach (var pInfo in _running)
        {
            pInfo.Value.Source.Cancel();
        }

        await Task.Delay(5000);
        var time = DateTime.UtcNow;
        _logger.LogInformation($"Closing Socket at: {time:yyyy-MM-dd_H-mm}");
        listener.Close();
    }
    
    private async void RunCommand(
        string executable,
        IEnumerable<string> scriptArgs,
        string workingDirectory,
        string outputDir,
        string outputName,
        CancellationToken token,
        uint processId)
    {
        // Setup the File Streams
        var stdOut = new FileInfo($@"{outputDir}\{outputName}_output.txt");
        var stdErr = new FileInfo($@"{outputDir}\{outputName}_error.txt");
        var outStream = new FileStream(stdOut.FullName, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, true);
        var errStream = new FileStream(stdErr.FullName, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, true);
        
        // Setup the Command and Run
        var command = Command.Run(
            executable: executable, 
            arguments: scriptArgs.ToArray<object>(),
            options => options.WorkingDirectory(workingDirectory));
        
        // Initiate File Pipes
        command.StandardOutput.PipeToAsync(outStream);
        command.StandardError.PipeToAsync(errStream);
        
        // Wait for End of Execution
        try
        {
            await command.Task.WaitAsync(token);
        }
        catch (TaskCanceledException e)
        {
            await command.TrySignalAsync(CommandSignal.ControlC);
        }
        finally
        {
            var runningInfo = _running[processId];
            runningInfo.StopTime = DateTime.UtcNow;
            _running[processId] = runningInfo;
        }
    }
}