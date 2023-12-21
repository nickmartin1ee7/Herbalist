using Godot;
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
	private const long StartingPoints = 300;

	private VBoxContainer _vbox;
	private Button _buyButton;
	private Panel _costPanel;
	private Label _costLabel;
	private Label _pointsLabel;
	private Dictionary<Seed, UpgradableSection> _sections = new();
	private int _lastSeedIndex;
	private readonly Seed[] _seeds = Enum.GetValues<Seed>();
	private Task _pointRenderCycler;
	private Task _dataSaverCycler;

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
	public override async void _Ready()
	{
		_vbox = GetNode<ScrollContainer>("ScrollContainer")
			.GetNode<VBoxContainer>("VBoxContainer");

		_buyButton = GetNode<Button>("BuyButton");
		_costPanel = GetNode<Panel>("CostPanel");
		_costLabel = GetNode<Panel>("CostPanel")
			.GetNode<Label>("CostLabel");


		_pointsLabel = GetNode<OverviewSection>("OverviewSection")
			.GetNode<Label>("Label");

		_pointRenderCycler ??= Task.Run(PointRenderCyclerJob);
		_dataSaverCycler ??= Task.Run(DataSaverCyclerJob);

		await ReloadDataFromStorage();

		UpdateBuyButton(PeekNextSeed(out _));
	}

	private async Task ReloadDataFromStorage()
	{
		var data = await DataStorage.Read();

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
				var newPos = new Vector2(section.Position.X, _vbox.GetChildCount() * (SectionHeight + SectionSpacing));
				section.Position = newPos;

				section.Scale = new Vector2(2.5f, 2.5f);

				var preparedSection = PrepareNextSection(section);

				if (preparedSection is not null)
				{
					_vbox.AddChild(preparedSection);

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

		_buyButton.Text = $"Buy {seed} ({(int)seed}/cycle)";
		_costLabel.Text = $"Costs {(int)seed * SeedCosts.Multiplier} seeds)";
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
				await TrySaveData();
				GD.Print($"{nameof(DataSaverCyclerJob)} data saved!");

			}
			catch (Exception ex)
			{
				GD.PrintErr($"{nameof(DataSaverCyclerJob)} failed to save data: {ex}", ex);
			}

			await Task.Delay(DataSaverCyclerDelayMs);
		}
	}

	private async Task TrySaveData()
	{
		var sectionsStr = JsonSerializer.Serialize(_sections.Select(s => s.Key).ToArray());
		var lastSeedIndexStr = JsonSerializer.Serialize(_lastSeedIndex);
		var pointsStr = JsonSerializer.Serialize(Points);

		var data = new Dictionary<string, string>
		{
			{ nameof(_sections), sectionsStr },
			{ nameof(_lastSeedIndex), lastSeedIndexStr },
			{ nameof(Points), pointsStr }
		};

		await DataStorage.Write(data);
	}

	private void UpdatePointsLabel()
	{
		_pointsLabel.Text = $"{Points} seeds";
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_buy_button_pressed()
	{
		HandleNewUpgrade();
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
		var newPos = new Vector2(section.Position.X, _vbox.GetChildCount() * (SectionHeight + SectionSpacing));
		section.Position = newPos;

		section.Scale = new Vector2(2.5f, 2.5f);

		var preparedSection = PrepareNextSection(section);

		if (preparedSection is not null)
		{
			GD.Print($"Bought {preparedSection.SeedType}!");

			_vbox.AddChild(preparedSection);

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
