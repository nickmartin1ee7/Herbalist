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
	private object _progressJob;

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

	public EventHandler<long> PointCreated { get; set; }
	public int UpgradeCost =>
		(int)SeedType * (int)(SeedCosts.Multiplier / 10d) * Multiplier;

	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		_button = GetNode<Button>("Button");
		_progressBar = GetNode<ProgressBar>("ProgressBar");
		_label = GetNode<Label>("Label");

		_world = GetParent().GetParent().GetParent().GetParent().GetNode<WorldScene>("WorldScene");

		_progressJob ??= Task.Run(ProgressCycle);

		NotifyStateChanged();
	}

	private async Task ProgressCycle()
	{
		GD.Print($"Progress bar min value: {_progressBar.MinValue}, Max Value: {_progressBar.MaxValue}");

		while (true)
		{
			var progress = _progressBar.Value;
			// Ensure the progress value is within the valid range
			var newProgress = Mathf.Clamp(progress + (double)Multiplier / 10,
				_progressBar.MinValue,
				_progressBar.MaxValue);

			CallThreadSafe(nameof(SetProgressBarValue), newProgress);

			// 100ms = 10 iterations in 1 second
			// 10ms = 100 iterations in 1 second
			// 1ms = 1000 iterations in 1 second
			await Task.Delay(10);
		}
	}

	private void SetProgressBarValue(double value) =>
		_progressBar.Value = value;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_progressBar.Value == 100d)
		{
			_progressBar.Value = 0d;
			PointCreated?.Invoke(this, (int)SeedType);
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
		if (!CanPurchaseMultiplier())
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
			_label.Text = $"{SeedType} ({(int)SeedType}/cycle)";
		}

		if (_button is not null)
		{
			_button.Text = $"x{Multiplier}{System.Environment.NewLine}Upgrade: {UpgradeCost}";
		}
	}
}
