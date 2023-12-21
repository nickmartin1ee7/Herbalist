using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class WorldScene : Node2D
{
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
		_vbox = GetNode<ScrollContainer>("ScrollContainer")
			.GetNode<VBoxContainer>("VBoxContainer");

		_buyButton = GetNode<Button>("BuyButton");
		_costPanel = GetNode<Panel>("CostPanel");
		_costLabel = GetNode<Panel>("CostPanel")
			.GetNode<Label>("CostLabel");


		_pointsLabel = GetNode<OverviewSection>("OverviewSection")
			.GetNode<Label>("Label");

		_pointRenderCycler ??= Task.Run(PointRenderCyclerJob);

		UpdateBuyButton(PeekNextSeed(out _));
	}

	private void UpdateBuyButton(Seed? seed)
	{
		if (seed is null)
		{
			return;
		}

		_buyButton.Text = $"Buy {seed} ({(int)seed}/second)";
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
		const float sectionHeight = 120; // Height of each Section
		const float spacing = 80; // Vertical spacing between Sections

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
		var newPos = new Vector2(section.Position.X, _vbox.GetChildCount() * (sectionHeight + spacing));
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

	private UpgradableSection PrepareNextSection(UpgradableSection section)
	{
		var nextSeed = RequestNextSeed();

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
