using Content.Shared._NF.PlantAnalyzer;
using JetBrains.Annotations;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Client._NF.PlantAnalyzer.UI;

[UsedImplicitly]
public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PlantAnalyzerWindow? _window;
    private CancellationTokenSource? _refreshCts;
    private NetEntity? _currentTarget;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new PlantAnalyzerWindow(this)
        {
            Title = Loc.GetString("plant-analyzer-interface-title"),
        };
        _window.OnClose += Close;
        _window.OpenCenteredLeft();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window == null)
            return;

        if (message is PlantAnalyzerScannedSeedPlantInformation cast)
        {
            _window.Populate(cast);
            _currentTarget = cast.TargetEntity;
            if (cast.IsTray)
                StartRefreshLoop();
            else
                StopRefreshLoop();
            return;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is not PlantAnalyzerUserInterfaceState s)
            return;

        // Convert state to message-like object for reuse of Populate.
        var msg = new PlantAnalyzerScannedSeedPlantInformation()
        {
            TargetEntity = s.TargetEntity,
            IsTray = s.IsTray,
            IsSwab = s.IsSwab,
            SeedName = s.SeedName,
            SeedChem = s.SeedChem,
            HarvestType = s.HarvestType,
            ExudeGases = s.ExudeGases,
            ConsumeGases = s.ConsumeGases,
            Endurance = s.Endurance,
            SeedYield = s.SeedYield,
            Lifespan = s.Lifespan,
            Maturation = s.Maturation,
            Production = s.Production,
            GrowthStages = s.GrowthStages,
            SeedPotency = s.SeedPotency,
            Speciation = s.Speciation,
            AdvancedInfo = s.AdvancedInfo
        };

        _window.Populate(msg);

        _currentTarget = s.TargetEntity;
        if (s.IsTray)
            StartRefreshLoop();
        else
            StopRefreshLoop();
    }

    public void AdvPressed(bool scanMode)
    {
        SendMessage(new PlantAnalyzerSetMode(scanMode));
    }

    private void StartRefreshLoop()
    {
        StopRefreshLoop();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;
        Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    SendMessage(new PlantAnalyzerRequestRefresh());
                    await Task.Delay(2000, ct);
                }
            }
            catch (TaskCanceledException)
            {
            }
        }, ct);
    }

    private void StopRefreshLoop()
    {
        try
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _refreshCts = null;
        }
    }

    [Obsolete]
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window != null)
            _window.OnClose -= Close;

        StopRefreshLoop();
        _window?.Dispose();
    }
}
