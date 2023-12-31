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
	private VBoxContainer _upgradableSectionContainer;
	private HBoxContainer _flowerGardenContainer;
	private SwipeableControl _swipableControl;
	private Label _pointsLabel;
	private Label _playtimeAmountLabel;
	private Button _forageButton;
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

		LoadUiNodes();

		_forageButton.Pressed += ForageSeed;

		_swipableControl.TweenFactory = () => _swipableControl.CreateTween();

		PopulateAndHideFlowersDictionary();

		ReloadDataFromStorage();

		ResetCyclerToken();
		_dataSaverCycler ??= Task.Run(() => DataSaverCyclerJob(_cyclerJobsCts.Token));
	}

	private void LoadUiNodes()
	{
		// TODO: Gross... Maybe I need reflection or something that just makes it easier to find these notes.

		var overviewSectionNode =
			GetNode<OverviewSection>("OverviewSection")
			;

		_pointsLabel = overviewSectionNode
			.
			GetNode<HBoxContainer>("HBoxContainer")
			.
			GetNode<Label>("SeedsLabel")
			;

		_playtimeAmountLabel = overviewSectionNode
			.
			GetNode<VBoxContainer>("VBoxContainer")
			.
			GetNode<Label>("PlaytimeAmountLabel")
			;

		_forageButton = overviewSectionNode
			.
			GetNode<HBoxContainer>("HBoxContainer")
			.
			GetNode<Button>("ForageButton")
			;

		var centralControl =
			GetNode<Control>("CentralControl")
			;

		_upgradableSectionContainer =
			centralControl
			.
			GetNode<ScrollContainer>("ScrollContainer")
			.
			GetNode<VBoxContainer>("UpgradableSectionContainer")
			;

		_flowerGardenContainer =
			centralControl
			.
			GetNode<HBoxContainer>("FlowerGardenContainer")
			;

		_swipableControl = centralControl.GetNode<SwipeableControl>("SwipeableControl");
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

		foreach (var seed in Enum.GetValues<Seed>().Skip(1))
		{
			var node = _flowerGardenContainer.GetNode<Sprite2D>(seed.ToString());

			if (node is not null)
			{
				_flowerSprites.Add(seed, node);
			}
		}

		// Make all invisible initially
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


	// TODO: Implement a caller, back button/esc?
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
				DeductSeedCostAndShowFlower(seed, isFree: true);

				// TODO: Refactor duplication
				// Load the Section scene and instance it
				var upgradableSectionScene = GD.Load<PackedScene>(UpgradableSection.ResourcePath);
				var section = upgradableSectionScene.Instantiate<UpgradableSection>();

				// Set the Section's position based on its index
				var newPos = new Vector2(section.Position.X, _upgradableSectionContainer.GetChildCount() * (SectionHeight + SectionSpacing));
				section.Position = newPos;

				section.Scale = new Vector2(2.5f, 2.5f);

				var preparedSection = PrepareNextSection(section, RequestNextSeed());

				if (preparedSection is not null)
				{
					_upgradableSectionContainer.AddChild(preparedSection);
					CallThreadSafe(nameof(AppendNewSectionHeightToFlowerVBox));
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

	private void BuyNewSeed(Seed? nextSeed)
	{
		if (!CanPurchaseNextUpgrade(nextSeed))
		{
			GD.Print($"Cannot purchase: {nextSeed}");
			return;
		}

		DeductSeedCostAndShowFlower(nextSeed.Value);

		// Load the Section scene and instance it
		var upgradableSectionScene = GD.Load<PackedScene>(UpgradableSection.ResourcePath);
		var section = upgradableSectionScene.Instantiate<UpgradableSection>();

		// Set the Section's position based on its index
		var newPos = new Vector2(section.Position.X, _upgradableSectionContainer.GetChildCount() * (SectionHeight + SectionSpacing));
		section.Position = newPos;

		section.Scale = new Vector2(2.5f, 2.5f);

		var preparedSection = PrepareNextSection(section, nextSeed);

		if (preparedSection is not null)
		{
			GD.Print($"Bought {preparedSection.SeedType}!");

			_upgradableSectionContainer.AddChild(preparedSection);
			CallThreadSafe(nameof(AppendNewSectionHeightToFlowerVBox));
		}
	}

	private void AppendNewSectionHeightToFlowerVBox()
	{
		var lastSectionY = _upgradableSectionContainer.GetChildCount() * (SectionHeight + SectionSpacing);
		_upgradableSectionContainer.CustomMinimumSize = new Vector2(_upgradableSectionContainer.Size.X, lastSectionY + SectionHeight);
	}

	private void DeductSeedCostAndShowFlower(Seed nextUpgrade, bool isFree = false)
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

	private UpgradableSection PrepareNextSection(UpgradableSection section, Seed? nextSeed)
	{
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
	}
}
