using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WildfireSimulator.Client.ViewModels;
using WildfireSimulator.Client.Views.Controls;

namespace WildfireSimulator.Client.Views;

public partial class MainWindow : Window
{
    private GraphVisualization? _gridVisualization;
    private NodeGraphVisualization? _clusteredGraphVisualization;
    private NodeGraphVisualization? _regionGraphVisualization;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        WireEvents();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void WireEvents()
    {
        _gridVisualization = this.FindControl<GraphVisualization>("GridVisualization");
        _clusteredGraphVisualization = this.FindControl<NodeGraphVisualization>("ClusteredGraphVisualization");
        _regionGraphVisualization = this.FindControl<NodeGraphVisualization>("RegionGraphVisualization");

        if (_gridVisualization != null)
            _gridVisualization.CellClicked += OnGridCellClicked;

        if (_clusteredGraphVisualization != null)
            _clusteredGraphVisualization.NodeClicked += OnGraphNodeClicked;

        if (_regionGraphVisualization != null)
            _regionGraphVisualization.NodeClicked += OnGraphNodeClicked;
    }

    private void OnGridCellClicked(object? sender, WildfireSimulator.Client.Models.GraphCellDto cell)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ToggleIgnitionCellSelection(cell);
    }

    private void OnGraphNodeClicked(object? sender, WildfireSimulator.Client.Models.SimulationGraphNodeDto node)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ToggleIgnitionNodeSelection(node);
    }
}