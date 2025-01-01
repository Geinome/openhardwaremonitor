using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OpenHardwareMonitor.GUI;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor.Cli;

internal class ComputerHardware : IDisposable
{
    private readonly PersistentSettings _settings;
    private readonly UnitManager _unitManager;
    private readonly UpdateVisitor _updateVisitor = new();
    private Computer _computer;
    private bool _disposedValue;

    public ComputerHardware()
    {
        _settings = new PersistentSettings();
        _unitManager = new UnitManager(_settings);
        var treeModel = new TreeModel();
        Root = new Node(Environment.MachineName);
        Root.Image = Utilities.EmbeddedResources.GetImage("computer.png");
        treeModel.Nodes.Add(Root);
        treeModel.ForceVisible = true;
    }

    public Node Root { get; set; }

    public Computer ComputerDiagnostics(CommandLineOptions.OptionsBase options)
    {
        if (_computer != null)
        {
            _computer.Close();
        }

        _computer = new Computer();

        _computer.CPUEnabled = !options.IgnoreMonitorCPU;
        _computer.FanControllerEnabled = !options.IgnoreMonitorFanController;
        _computer.GPUEnabled = !options.IgnoreMonitorGPU;
        _computer.HDDEnabled = !options.IgnoreMonitorHDD;
        _computer.MainboardEnabled = !options.IgnoreMonitorMainboard;
        _computer.RAMEnabled = !options.IgnoreMonitorRAM;
        _computer.NetworkEnabled = !options.IgnoreMonitorNetwork;

        _computer.HardwareAdded += HardwareAdded;
        _computer.HardwareRemoved += HardwareRemoved;

        // add platform dependent code
        var platForm = Environment.OSVersion.Platform;
        if (platForm == PlatformID.Win32NT)
        {
            // Windows
            // not sure if really required: gadget = new OpenHardwareMonitor.GUI.SensorGadget(computer, settings, unitManager);
            // wmiProvider = new OpenHardwareMonitor.WMI.WmiProvider(computer);
        }

        _computer.Open();

        _computer.Accept(_updateVisitor);

        return _computer;
    }

    public void RefreshData()
    {
        _computer.Accept(_updateVisitor);
    }

    private void InsertSorted(Collection<Node> nodes, HardwareNode node)
    {
        int i = 0;
        while (i < nodes.Count && nodes[i] is HardwareNode &&
               ((HardwareNode)nodes[i]).Hardware.HardwareType <
               node.Hardware.HardwareType)
            i++;
        nodes.Insert(i, node);
    }

    private void SubHardwareAdded(IHardware hardware, Node node)
    {
        HardwareNode hardwareNode =
            new HardwareNode(hardware, _settings, _unitManager);

        InsertSorted(node.Nodes, hardwareNode);

        foreach (IHardware subHardware in hardware.SubHardware)
            SubHardwareAdded(subHardware, hardwareNode);
    }

    private void HardwareAdded(IHardware hardware)
    {
        SubHardwareAdded(hardware, Root);
    }

    private void HardwareRemoved(IHardware hardware)
    {
        List<HardwareNode> nodesToRemove = new List<HardwareNode>();
        foreach (Node node in Root.Nodes)
        {
            HardwareNode hardwareNode = node as HardwareNode;
            if (hardwareNode != null && hardwareNode.Hardware == hardware)
                nodesToRemove.Add(hardwareNode);
        }

        foreach (HardwareNode hardwareNode in nodesToRemove)
        {
            Root.Nodes.Remove(hardwareNode);
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
            }

            if (_computer != null)
            {
                _computer.Close();
                _computer = null;
            }

            _disposedValue = true;
        }
    }

    ~ComputerHardware()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
