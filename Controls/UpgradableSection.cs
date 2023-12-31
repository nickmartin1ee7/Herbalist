using System;
using System.Threading.Tasks;

using Godot;

public partial class UpgradableSection : Node2D
{
    // 100ms = 10 iterations in 1 second
    // 10ms = 100 iterations in 1 second
    // 1ms = 1000 iterations in 1 second
    private const int CycleDelay = 5;

    private Button _upgradeButton;
    private Sprite2D[] _upgradeIcons;
    private ProgressBar _seedProgressBar;
    private TextureProgressBar _seedProgressBar2;
    private Label _itemNameLabel;
    private Label _itemRateLabel;
    private Label _seedCostLabel;
    private Panel _maxedOutPanel;
    private WorldScene _world;
    private Seed _seed;
    private int _multiplier = 1;
    private Task _progressJob;
    bool _isUpgradeButtonDown = false;
    DateTime? _upgradeButtonDownTime = null;

    private readonly TimeSpan OneSecondTimeSpan = TimeSpan.FromSeconds(1);
    private readonly double MultiplierProgressionFactor = 10d;

    private double MultiplierProgressionRate
        => Multiplier / MultiplierProgressionFactor;

    private double MaxMultiplier
        => MultiplierProgressionFactor * _seedProgressBar.MaxValue;

    private double ProgressBarCutOffMultiplier
        => MaxMultiplier * .5;

    private bool IsMultiplierOverCutOff
        => Multiplier >= ProgressBarCutOffMultiplier;

    public Seed SeedType
    {
        get => _seed;

        set
        {
            if (_seed == value)
            {
                return;
            }

            _seed = value;
            NotifyStateChanged();
        }
    }
    public int Multiplier
    {
        get => _multiplier;
        set
        {
            if (_multiplier == value)
            {
                return;
            }

            _multiplier = value;
            NotifyStateChanged();
        }
    }

    public int Rate
        => (int)SeedType * Multiplier;

    public const string ResourcePath = "res://Controls/upgradable_section.tscn";

    public EventHandler<long> PointCreated { get; set; }
    public int UpgradeCost =>
        (int)SeedType * (int)(SeedCosts.Multiplier / 10d) * Multiplier;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        var upgradePanel = GetNode<PanelContainer>("UpgradePanel");
        _upgradeButton = upgradePanel.GetNode<Button>("UpgradeButton");
        _upgradeButton.Pressed += Upgrade;
        _upgradeButton.ButtonDown += () =>
        {
            _isUpgradeButtonDown = true;
            _upgradeButtonDownTime = DateTime.Now;
        };

        _upgradeButton.ButtonUp += () =>
        {
            _isUpgradeButtonDown = false;
            _upgradeButtonDownTime = null;
        };


        _upgradeIcons = new[]
        {
            upgradePanel.GetNode<Sprite2D>("UpgradeIcon"),
            upgradePanel.GetNode<Sprite2D>("UpgradeIcon2")
        };

        SetUpgradeIconVisibility(visible: false);

        _seedProgressBar = GetNode<ProgressBar>("SeedProgressBar");
        _seedProgressBar2 = GetNode<TextureProgressBar>("SeedProgressBar2");
        _itemNameLabel = GetNode<Label>("ItemNameLabel");
        _itemRateLabel = GetNode<Label>("ItemRateLabel");
        _seedCostLabel = GetNode<Panel>("SeedCostPanel").GetNode<Label>("SeedCostLabel");

        _maxedOutPanel = GetNode<Panel>("MaxedOutPanel");

        _world = (WorldScene)GetTree().CurrentScene;

        _progressJob ??= Task.Run(ProgressCycle);

        NotifyStateChanged();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (_isUpgradeButtonDown && _upgradeButtonDownTime.HasValue)
        {
            var timeHeld = (DateTime.Now - _upgradeButtonDownTime.Value);
            if (timeHeld > OneSecondTimeSpan)
            {
                Upgrade();
            }
        }

        if (_seedProgressBar.Value == 100d)
        {
            if (Multiplier >= MaxMultiplier && !_maxedOutPanel.Visible)
            {
                _seedProgressBar.Visible = false;
                _seedProgressBar2.Visible = false;

                _maxedOutPanel.Visible = true;
            }

            _seedProgressBar2.Value = 0d;
            _seedProgressBar.Value = 0d;
            PointCreated?.Invoke(this, (int)SeedType);
        }

        SetUpgradeIconVisibility(CanPurchaseMultiplier());
    }


    private void SetUpgradeIconVisibility(bool visible)
    {
        foreach (var upgradeIcon in _upgradeIcons)
        {
            upgradeIcon.Visible = visible;

        }
    }

    private async Task ProgressCycle()
    {
        GD.Print($"Progress bar min value: {_seedProgressBar.MinValue}, Max Value: {_seedProgressBar.MaxValue}");

        while (true)
        {
            var progress = _seedProgressBar.Value;

            // Ensure the progress value is within the valid range
            var newProgress = Mathf.Clamp(progress + MultiplierProgressionRate,
                _seedProgressBar.MinValue,
                _seedProgressBar.MaxValue);

            CallThreadSafe(nameof(SetProgressBarValue), newProgress);

            await Task.Delay(CycleDelay);
        }
    }

    private void SetProgressBarValue(double value)
    {
        _seedProgressBar.Value = value;
        _seedProgressBar2.Value = value;
    }

    private void BuyMultiplier()
    {
        _world.Points -= UpgradeCost;
        Multiplier++;
        NotifyStateChanged();
    }

    private void Upgrade()
    {
        if (!CanPurchaseMultiplier())
        {
            GD.Print($"Cannot afford to purchase upgrade for {SeedType}. {_world.Points} < {UpgradeCost}!");
            return;
        }

        GD.Print($"Purchasing upgrade for {SeedType}. {_world.Points} < {UpgradeCost}!");
        BuyMultiplier();
    }

    private bool CanPurchaseMultiplier() =>
        _world.Points >= UpgradeCost && Multiplier < MaxMultiplier;

    private void NotifyStateChanged()
    {
        if (_itemNameLabel is not null)
        {
            _itemNameLabel.Text = $"{SeedType}";
        }

        if (_itemRateLabel is not null)
        {
            _itemRateLabel.Text = $"{((long)SeedType).FormatLargeNumber()}";
        }

        if (_upgradeButton is not null)
        {
            _upgradeButton.Text = $"x{((long)Multiplier).FormatLargeNumber()}";
        }

        if (_seedCostLabel is not null)
        {
            _seedCostLabel.Text = $"{((long)UpgradeCost).FormatLargeNumber()}";
        }
    }
}
