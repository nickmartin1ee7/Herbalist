using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Godot;

public partial class WorldScene : Node2D
{
    private const float SectionHeight = 120; // Height of each Section
    private const float SectionSpacing = 120; // Vertical spacing between Sections
    private const int DataSaverCyclerDelayMs = 500;
    private const int PointRenderCyclerDelayMs = 5;
    private const int PlaytimeAmountRenderCyclerDelayMs = 5;
    private const long StartingPoints = 50;

    private readonly Seed[] _seeds = Enum.GetValues<Seed>();
    private readonly Dictionary<Seed, UpgradableSection> _sections = new();

    private Dictionary<Seed, Sprite2D> _flowerSprites;
    private VBoxContainer _flowersVBoxContainer;
    private Label _pointsLabel;
    private Label _playtimeAmountLabel;
    private Button _forageButton;
    private Button _mainMenuButton;
    private Button _shopButton;
    private int _lastSeedIndex;
    private DateTime _startTime;

    private CancellationTokenSource _cyclerJobsCts;
    private Task _dataSaverCycler;

    private int GetSeedCost(Seed seed) => (int)seed * SeedCosts.Multiplier;

    public long Points { get; set; } = StartingPoints;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _startTime = DateTime.Now;

        _flowersVBoxContainer = GetNode<ScrollContainer>("FlowersScrollContainer")
            .GetNode<VBoxContainer>("FlowersVBoxContainer");

        var overviewSectionNode = GetNode<OverviewSection>("OverviewSection");

        _pointsLabel = overviewSectionNode
            .GetNode<HBoxContainer>("HBoxContainer")
            .GetNode<Label>("SeedsLabel");

        _playtimeAmountLabel = overviewSectionNode
            .GetNode<VBoxContainer>("VBoxContainer")
            .GetNode<Label>("PlaytimeAmountLabel");

        _forageButton = overviewSectionNode
            .GetNode<HBoxContainer>("HBoxContainer")
            .GetNode<Button>("ForageButton");

        _mainMenuButton = GetNode<HBoxContainer>("FooterHBoxContainer").GetNode<Button>("MainMenuButton");
        _shopButton = GetNode<HBoxContainer>("FooterHBoxContainer").GetNode<Button>("ShopButton");

        _forageButton.Pressed += ForageSeed;
        _mainMenuButton.Pressed += NavigateToMainMenu;
        _shopButton.Pressed += NavigateToShop;

        PopulateAndHideFlowersDictionary();

        ReloadDataFromStorage();

        UpdateBuyButton(PeekNextSeed(out _));

        ResetCyclerToken();
        _dataSaverCycler ??= Task.Run(() => DataSaverCyclerJob(_cyclerJobsCts.Token));
    }

    private void ResetCyclerToken()
    {
        _cyclerJobsCts?.Cancel();
        _cyclerJobsCts = new();
    }

    private void ForageSeed()
    {
        Points++;
    }

    private void PopulateAndHideFlowersDictionary()
    {
        _flowerSprites = new();
        var flowerBox = GetNode<HBoxContainer>("FlowerHBoxContainer");
        _flowerSprites.Add(Seed.Grass, flowerBox.GetNode<Sprite2D>(nameof(Seed.Grass)));
        _flowerSprites.Add(Seed.Marigold, flowerBox.GetNode<Sprite2D>(nameof(Seed.Marigold)));
        _flowerSprites.Add(Seed.Sunflower, flowerBox.GetNode<Sprite2D>(nameof(Seed.Sunflower)));
        _flowerSprites.Add(Seed.Rose, flowerBox.GetNode<Sprite2D>(nameof(Seed.Rose)));
        _flowerSprites.Add(Seed.Iris, flowerBox.GetNode<Sprite2D>(nameof(Seed.Iris)));
        _flowerSprites.Add(Seed.Daisy, flowerBox.GetNode<Sprite2D>(nameof(Seed.Daisy)));

        foreach (var flowerSprite in _flowerSprites)
        {
            flowerSprite.Value.Visible = false;
        }
    }

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


    private void NavigateToShop()
    {
        // TODO: Implement shop
        //GetTree().ChangeSceneToFile(Scenes.Shop);

        // TODO: For now, just buy next flower
        HandleNewUpgrade();
    }

    private void NavigateToMainMenu()
    {
        ResetCyclerToken();
        GetTree().ChangeSceneToFile(Scenes.MainMenu);
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
                BuyAndShowUpgrade(seed, isFree: true);

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
                    CallThreadSafe(nameof(AppendNewSectionHeightToFlowerVBox));

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

        if (data.TryGetValue(nameof(_startTime), out var startTimeStr))
        {
            _startTime = JsonSerializer.Deserialize<DateTime>(startTimeStr);
        }

        GD.Print("Data read!");
    }

    private void UpdateBuyButton(Seed? seed)
    {
        if (seed is null)
        {
            HidePurchasing();
            return;
        }

        _shopButton.Text = $"Buy {seed} ({(int)seed}/cycle){System.Environment.NewLine}Costs {(int)seed * SeedCosts.Multiplier} seeds)";
    }

    private async Task DataSaverCyclerJob(CancellationToken token)
    {
        GD.Print($"{nameof(DataSaverCyclerJob)} started!");

        try
        {
            while (!token.IsCancellationRequested)
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

                await Task.Delay(DataSaverCyclerDelayMs, token);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void TrySaveData()
    {
        var sectionsStr = JsonSerializer.Serialize(_sections.Select(s => s.Key).ToArray());
        var sectionsUpgrades = JsonSerializer.Serialize(_sections.Select(s => s.Value.Multiplier).ToArray());

        var lastSeedIndexStr = JsonSerializer.Serialize(_lastSeedIndex);
        var pointsStr = JsonSerializer.Serialize(Points);
        var startTimeStr = JsonSerializer.Serialize(_startTime);

        var data = new Dictionary<string, string>
        {
            { nameof(_sections), sectionsStr },
            { nameof(_lastSeedIndex), lastSeedIndexStr },
            { nameof(Points), pointsStr },
            { $"{nameof(_sections)}.{nameof(UpgradableSection.Multiplier)}", sectionsUpgrades},
            { nameof(_startTime), startTimeStr }
        };

        DataStorage.Write(data);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        _pointsLabel.Text = $"{Points.FormatLargeNumber()} seeds";
        _playtimeAmountLabel.Text = $"{DateTime.Now - _startTime:hh\\:mm\\:ss\\.fff}";
    }

    private void HandleNewUpgrade()
    {
        var nextSeed = PeekNextSeed(out _);

        if (!CanPurchaseNextUpgrade(nextSeed))
        {
            GD.Print($"Cannot purchase: {nextSeed}");
            return;
        }

        BuyAndShowUpgrade(nextSeed.Value);

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

            GD.Print($"DEBUG: UpgradableSection size = {preparedSection}");

            _flowersVBoxContainer.AddChild(preparedSection);
            CallThreadSafe(nameof(AppendNewSectionHeightToFlowerVBox));

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

    private void AppendNewSectionHeightToFlowerVBox()
    {
        var lastSectionY = _flowersVBoxContainer.GetChildCount() * (SectionHeight + SectionSpacing);

        _flowersVBoxContainer.CustomMinimumSize = new Vector2(_flowersVBoxContainer.Size.X, lastSectionY + SectionHeight);

        GD.Print($"DEBUG: New flower vbox size: {_flowersVBoxContainer.Size}");
    }

    private void BuyAndShowUpgrade(Seed nextUpgrade, bool isFree = false)
    {
        GD.Print($"Buying (Free = {isFree}: {nextUpgrade}");

        if (!isFree)
        {
            Points -= GetSeedCost(nextUpgrade);
        }

        ShowFlower(nextUpgrade);
    }

    private void ShowFlower(Seed nextUpgrade)
    {
        if (!_flowerSprites.TryGetValue(nextUpgrade, out var flowerSprite))
        {
            return;
        }

        flowerSprite.Visible = true;
    }

    private bool CanPurchaseNextUpgrade(Seed? nextSeed) =>
        nextSeed.HasValue && GetSeedCost(nextSeed.Value) <= Points;

    private void HidePurchasing()
    {
        _shopButton.Text = "Sold Out";
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
