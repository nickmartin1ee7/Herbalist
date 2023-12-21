using Godot;
using System;
using System.Threading.Tasks;

public partial class UpgradableSection : Node2D
{
	private Button _button;
	private ProgressBar _progressBar;
	private Label _label;
	private WorldScene _world;
	private Seed _seed;

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
	public int Multiplier { get; private set; } = 1;
	public int Rate => (int)SeedType * Multiplier;

	public const string ResourcePath = "res://Controls/upgradable_section.tscn";

	public EventHandler PointCreated { get; set; }
	public int UpgradeCost =>
		(int)SeedType * (int)(SeedCosts.Multiplier / 2d) * Multiplier;

	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		_button = GetNode<Button>("Button");
		_progressBar = GetNode<ProgressBar>("ProgressBar");
		_label = GetNode<Label>("Label");

		_world = GetParent().GetParent().GetParent().GetParent().GetNode<WorldScene>("WorldScene");

		NotifyStateChanged();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Ensure the progress value is within the valid range
		var newProgress = Mathf.Clamp(_progressBar.Value + delta * Rate, _progressBar.MinValue, _progressBar.MaxValue);

		// Update the progress value
		_progressBar.Value = newProgress;

		if (_progressBar.Value == 100d)
		{
			_progressBar.Value = 0d;
			PointCreated?.Invoke(this, EventArgs.Empty);
		}
	}

	private void BuyMultiplier()
	{
		_world.Points -= UpgradeCost;
		Multiplier++;
		NotifyStateChanged();
	}

	private void _on_button_pressed()
	{
		if (CanPurchaseMultiplier())
		{
			GD.Print($"Cannot afford to purchase upgrade for {SeedType}. {_world.Points} < {UpgradeCost}!");
			return;
		}

		GD.Print($"Purchasing upgrade for {SeedType}. {_world.Points} < {UpgradeCost}!");
		BuyMultiplier();
	}

	private bool CanPurchaseMultiplier() =>
		_world.Points >= UpgradeCost;

	private void NotifyStateChanged()
	{
		if (_label is not null)
		{
			_label.Text = SeedType.ToString();
		}

		if (_button is not null)
		{
			_button.Text = $"x{Multiplier}{System.Environment.NewLine}Upgrade: {UpgradeCost}";
		}
	}
}
