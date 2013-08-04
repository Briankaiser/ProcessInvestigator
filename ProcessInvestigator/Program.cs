using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;

namespace ProcessInvestigator
{
    class Program
    {
        static void Main(string[] args)
        {

            bool needsExit = false;

            while (!needsExit)
            {
                int currentPid = -1;
                Console.WriteLine("Enter Command (or ?): ");
                string wholeCommand = Console.ReadLine().ToLower();
                string[] commandSplit = wholeCommand.Split(' ');
                string firstPartCommand = commandSplit[0];
                string secondPartCommand = commandSplit.Length > 1 ? commandSplit[1] : null;

                if (!int.TryParse(secondPartCommand, out currentPid))
                {
                    //try to find the process by name
                    var curProcess = Process.GetProcessesByName(secondPartCommand).FirstOrDefault();
                    if (curProcess != null)
                    {
                        currentPid = curProcess.Id;
                    }
                }
                try
                {
                    switch (firstPartCommand)
                    {

                        case "?":
                        case "help":
                            {
                                Console.WriteLine("Commands:");
                                Console.WriteLine("List - list running processes");
                                Console.WriteLine("Stack <Pid|ProcessName> <optional search phase> - prints CLR stack traces");
                                Console.WriteLine("Heap <Pid|ProcessName> <optional search phase> - prints heap allocations");
                                Console.WriteLine("ThreadPool <Pid|ProcessName> - prints ThreadPool information");
                                Console.WriteLine("Quit");
                                Console.WriteLine();
                                break;
                            }
                        case "list":
                            {
                                Console.WriteLine("Processes:");
                                foreach (var p in Process.GetProcesses().OrderBy(p => p.ProcessName))
                                {
                                    Console.WriteLine("{0}\t{1}\t{2} {3}", p.Id, p.ProcessName, p.StartInfo.FileName, p.StartInfo.Arguments);
                                }
                                Console.WriteLine();
                                break;
                            }
                        case "stack":
                            {
                                if (currentPid == -1)
                                {
                                    Console.WriteLine("Could not find process by name");
                                    break;
                                }
                                string searchPhase = commandSplit.Length > 2 ? string.Join(" ", commandSplit.Skip(2)): null;
                                using (DataTarget dt = DataTarget.AttachToProcess(currentPid, 5000, AttachFlag.NonInvasive))
                                {
                                    if (dt.ClrVersions.Count == 0)
                                    {
                                        Console.WriteLine("Process is probably not running in the CLR");
                                        break;
                                    }
                                    string dacLocation = dt.ClrVersions[0].TryGetDacLocation();
                                    if (string.IsNullOrEmpty(dacLocation))
                                        dacLocation = dt.ClrVersions[0].DacInfo.FileName;


                                    ClrRuntime runtime = dt.CreateRuntime(dacLocation);
                                    foreach (ClrThread thread in runtime.Threads)
                                    {
                                        if (thread.StackTrace.Count == 0) continue;

                                        if (searchPhase != null)
                                        {
                                            //if search phase and none of the frames contain it - then skip
                                            if (!thread.StackTrace.Any(f =>f.DisplayString.IndexOf(searchPhase,
                                                                            StringComparison.OrdinalIgnoreCase) > -1))
                                            {
                                                continue;
                                            }
                                        }
                                        Console.WriteLine("ThreadID: {0:X}", thread.OSThreadId);
                                        Console.WriteLine("Exception?: {0}", thread.CurrentException);
                                        Console.WriteLine("Callstack:");

                                        foreach (ClrStackFrame frame in thread.StackTrace)
                                            Console.WriteLine("{0,12:X} {1,12:X} {2}", frame.InstructionPointer,
                                                              frame.StackPointer, frame.DisplayString);

                                        Console.WriteLine();
                                    }
                                }
                                break;
                            }
                        case "heap":
                            {
                                if (currentPid == -1)
                                {
                                    Console.WriteLine("Could not find process by name");
                                    break;
                                }
                                string searchPhase = commandSplit.Length > 2 ? string.Join(" ", commandSplit.Skip(2)) : null;
                                using (DataTarget dt = DataTarget.AttachToProcess(currentPid, 5000, AttachFlag.NonInvasive))
                                {
                                    if (dt.ClrVersions.Count == 0)
                                    {
                                        Console.WriteLine("Process is probably not running in the CLR");
                                        break;
                                    }
                                    string dacLocation = dt.ClrVersions[0].TryGetDacLocation();
                                    if (string.IsNullOrEmpty(dacLocation))
                                        dacLocation = dt.ClrVersions[0].DacInfo.FileName;


                                    ClrRuntime runtime = dt.CreateRuntime(dacLocation);
                                    ClrHeap heap = runtime.GetHeap();
                                    var stats = from o in heap.EnumerateObjects()
                                                let t = heap.GetObjectType(o)
                                                group o by t
                                                    into g
                                                    let size = g.Sum(o => (uint)g.Key.GetSize(o))
                                                    orderby size
                                                    select new
                                                    {
                                                        Name = g.Key.Name,
                                                        Size = size,
                                                        Count = g.Count()
                                                    };
                                    if (searchPhase != null)
                                    {
                                        stats = stats.Where(g => g.Name.IndexOf(searchPhase, StringComparison.OrdinalIgnoreCase) > -1);
                                    }
                                    foreach (var item in stats)
                                    {
                                        Console.WriteLine("{0,12:n0} {1,12:n0} {2}", item.Size, item.Count, item.Name);
                                    }



                                }
                                break;
                            }
                        case "quit":
                        case "exit":
                            {
                                needsExit = true;
                            }

                            break;

                    } //end switch
                }
                catch (ClrDiagnosticsException diagEx)
                {
                    Console.WriteLine("Exception running command:");
                    Console.WriteLine(diagEx.Message);
                }
                
            } //end while

            Console.ReadLine();
        }
    }
}
