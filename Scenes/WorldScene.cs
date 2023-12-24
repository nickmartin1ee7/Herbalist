using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class WorldScene : Node2D
{
    private const float SectionHeight = 120; // Height of each Section
    private const float SectionSpacing = 120; // Vertical spacing between Sections
    private const int DataSaverCyclerDelayMs = 5000;
    private const int PointRenderCyclerDelayMs = 5;
    private const int PlaytimeAmountRenderCyclerDelayMs = 5;
    private const long StartingPoints = 50;

    private readonly Seed[] _seeds = Enum.GetValues<Seed>();
    private readonly Dictionary<Seed, UpgradableSection> _sections = new();

    private VBoxContainer _flowersVBoxContainer;
    private Button _buyButton;
    private Panel _costPanel;
    private Label _costLabel;
    private Label _pointsLabel;
    private Label _playtimeAmountLabel;
    private Button _mainMenuButton;
    private Button _shopButton;
    private int _lastSeedIndex;
    private Task _pointRenderCycler;
    private Task _dataSaverCycler;
    private Task _playtimeAmountRenderCycler;
    private DateTime _startTime;

    private int GetSeedCost(Seed seed) => (int)seed * SeedCosts.Multiplier;

    private Seed? RequestNextSeed()
    {
        var nextSeed = PeekNextSeed(out var nextSeedIndex);
        _lastSeedIndex = nextSeedIndex;

        return nextSeed;
    }

    private Seed? PeekNextSeed(out int nextSeedIndex)
    {
        nextSeedIndex = _lastSeedIndex + 1;

        if (nextSeedIndex >= _seeds.Length)
            return null;

        var nextSeed = _seeds[nextSeedIndex];
        return nextSeed;
    }

    public long Points { get; set; } = StartingPoints;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _startTime = DateTime.Now;

        _flowersVBoxContainer = GetNode<ScrollContainer>("FlowersScrollContainer")
            .GetNode<VBoxContainer>("FlowersVBoxContainer");

        _pointsLabel = GetNode<OverviewSection>("OverviewSection")
            .GetNode<Label>("SeedsLabel");

        _playtimeAmountLabel = GetNode<OverviewSection>("OverviewSection")
            .GetNode<VBoxContainer>("VBoxContainer")
            .GetNode<Label>("PlaytimeAmountLabel");

        _mainMenuButton = GetNode<HBoxContainer>("FooterHBoxContainer").GetNode<Button>("MainMenuButton");
        _shopButton = GetNode<HBoxContainer>("FooterHBoxContainer").GetNode<Button>("ShopButton");

        _mainMenuButton.Pressed += NavigateToMainMenu;
        _shopButton.Pressed += NavigateToShop;

        _playtimeAmountRenderCycler ??= Task.Run(PlaytimeAmountRenderCyclerJob);
        _pointRenderCycler ??= Task.Run(PointRenderCyclerJob);
        _dataSaverCycler ??= Task.Run(DataSaverCyclerJob);

        ReloadDataFromStorage();

        UpdateBuyButton(PeekNextSeed(out _));
    }

    private void NavigateToShop()
    {
        // TODO: Implement shop
        //GetTree().ChangeSceneToFile(Scenes.Shop);

        // TODO: For now, just buy next flower
        HandleNewUpgrade();
    }

    private void NavigateToMainMenu()
    {
        GetTree().ChangeSceneToFile(Scenes.MainMenu);
    }

    private async Task PlaytimeAmountRenderCyclerJob()
    {
        GD.Print($"{nameof(PlaytimeAmountRenderCyclerJob)} started!");

        while (true)
        {
            if (_playtimeAmountLabel is not null)
            {
                CallThreadSafe(nameof(UpdatePlaytimeAmountLabel));
            }

            await Task.Delay(PlaytimeAmountRenderCyclerDelayMs);
        }
    }

    private void UpdatePlaytimeAmountLabel()
    {
        _playtimeAmountLabel.Text = $"{DateTime.Now - _startTime:mm\\:ss\\.fff}";
    }

    private void ReloadDataFromStorage()
    {
        var data = DataStorage.Read();

        if (data is null)
        {
            return;
        }

        if (data.TryGetValue(nameof(_sections), out var sectionsStr))
        {
            var seeds = JsonSerializer.Deserialize<Seed[]>(sectionsStr);
            foreach (var seed in seeds)
            {
                GD.Print($"Giving the user back previously purchased: {seed}");

                // TODO: Refactor duplication
                // Load the Section scene and instance it
                var upgradableSectionScene = GD.Load<PackedScene>(UpgradableSection.ResourcePath);
                var section = upgradableSectionScene.Instantiate<UpgradableSection>();

                // Set the Section's position based on its index
                var newPos = new Vector2(section.Position.X, _flowersVBoxContainer.GetChildCount() * (SectionHeight + SectionSpacing));
                section.Position = newPos;

                section.Scale = new Vector2(2.5f, 2.5f);

                var preparedSection = PrepareNextSection(section);

                if (preparedSection is not null)
                {
                    _flowersVBoxContainer.AddChild(preparedSection);

                    var nextUpgrade = PeekNextSeed(out _);
                    UpdateBuyButton(nextUpgrade);

                    GD.Print($"Next upgrade: {nextUpgrade}");

                    if (nextUpgrade is null)
                    {
                        // No more to buy!
                        GD.Print($"No more upgrades to buy!");

                        HidePurchasing();
                    }
                }
            }
        }

        if (data.TryGetValue($"{nameof(_sections)}.{nameof(UpgradableSection.Multiplier)}", out var sectionsMultiplierStr))
        {
            var sectionsMultipliers = JsonSerializer.Deserialize<int[]>(sectionsMultiplierStr);
            var sections = _sections.ToArray();

            for (int i = 0; i < _sections.Count; i++)
            {
                GD.Print($"Upgrading {sections[i]} to {sectionsMultipliers[i]} multiplier");
                sections[i].Value.Multiplier = sectionsMultipliers[i];
            }
        }

        if (data.TryGetValue(nameof(_lastSeedIndex), out var lastSeedIndexStr))
        {
            _lastSeedIndex = JsonSerializer.Deserialize<int>(lastSeedIndexStr);
        }

        if (data.TryGetValue(nameof(Points), out var pointsStr))
        {
            Points = JsonSerializer.Deserialize<long>(pointsStr);
        }

        GD.Print("Data read!");
    }

    private void UpdateBuyButton(Seed? seed)
    {
        if (seed is null)
        {
            return;
        }

        _shopButton.Text = $"Buy {seed} ({(int)seed}/cycle){System.Environment.NewLine}Costs {(int)seed * SeedCosts.Multiplier} seeds)";
    }

    private async Task PointRenderCyclerJob()
    {
        GD.Print($"{nameof(PointRenderCyclerJob)} started!");

        while (true)
        {
            if (_pointsLabel is not null)
            {
                CallThreadSafe(nameof(UpdatePointsLabel));
            }

            await Task.Delay(PointRenderCyclerDelayMs);
        }
    }

    private async Task DataSaverCyclerJob()
    {
        GD.Print($"{nameof(DataSaverCyclerJob)} started!");

        while (true)
        {
            try
            {
                TrySaveData();
                GD.Print($"{nameof(DataSaverCyclerJob)} data saved!");

            }
            catch (Exception ex)
            {
                GD.PrintErr($"{nameof(DataSaverCyclerJob)} failed to save data: {ex}", ex);
            }

            await Task.Delay(DataSaverCyclerDelayMs);
        }
    }

    private void TrySaveData()
    {
        var sectionsStr = JsonSerializer.Serialize(_sections.Select(s => s.Key).ToArray());
        var sectionsUpgrades = JsonSerializer.Serialize(_sections.Select(s => s.Value.Multiplier).ToArray());

        var lastSeedIndexStr = JsonSerializer.Serialize(_lastSeedIndex);
        var pointsStr = JsonSerializer.Serialize(Points);

        var data = new Dictionary<string, string>
        {
            { nameof(_sections), sectionsStr },
            { nameof(_lastSeedIndex), lastSeedIndexStr },
            { nameof(Points), pointsStr },
            { $"{nameof(_sections)}.{nameof(UpgradableSection.Multiplier)}", sectionsUpgrades}
        };

        DataStorage.Write(data);
    }

    private void UpdatePointsLabel()
    {
        _pointsLabel.Text = $"{Points} seeds";
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    private void HandleNewUpgrade()
    {
        var nextSeed = PeekNextSeed(out _);

        if (!CanPurchaseNextUpgrade(nextSeed))
        {
            return;
        }

        BuyUpgrade(nextSeed.Value);

        // Load the Section scene and instance it
        var upgradableSectionScene = GD.Load<PackedScene>(UpgradableSection.ResourcePath);
        var section = upgradableSectionScene.Instantiate<UpgradableSection>();

        // Set the Section's position based on its index
        var newPos = new Vector2(section.Position.X, _flowersVBoxContainer.GetChildCount() * (SectionHeight + SectionSpacing));
        section.Position = newPos;

        section.Scale = new Vector2(2.5f, 2.5f);

        var preparedSection = PrepareNextSection(section);

        if (preparedSection is not null)
        {
            GD.Print($"Bought {preparedSection.SeedType}!");

            _flowersVBoxContainer.AddChild(preparedSection);

            var nextUpgrade = PeekNextSeed(out _);
            UpdateBuyButton(nextUpgrade);

            GD.Print($"Next upgrade: {nextUpgrade}");

            if (nextUpgrade is null)
            {
                // No more to buy!
                GD.Print($"No more upgrades to buy!");

                HidePurchasing();
            }
        }
    }

    private void BuyUpgrade(Seed nextUpgrade)
    {
        Points -= GetSeedCost(nextUpgrade);
    }

    private bool CanPurchaseNextUpgrade(Seed? nextSeed) =>
        !nextSeed.HasValue || GetSeedCost(nextSeed.Value) <= Points;

    private void HidePurchasing()
    {
        _buyButton.Text = "Sold Out";
        _costPanel.Visible = false;
    }

    private UpgradableSection PrepareNextSection(UpgradableSection section, Seed? nextSeed = null)
    {
        nextSeed = nextSeed ?? RequestNextSeed();

        if (!nextSeed.HasValue)
        {
            return null;
        }

        section.SeedType = nextSeed.Value;
        section.PointCreated += OnPointCreated;

        _sections.Add(nextSeed.Value, section);

        return section;
    }

    private void OnPointCreated(object sender, long earnedPoints)
    {
        Points += earnedPoints;

        GD.Print($"{earnedPoints} points earned from {((UpgradableSection)sender).SeedType}! Points: {Points}");
    }
}
